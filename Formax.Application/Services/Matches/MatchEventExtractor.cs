using System;
using System.Text.RegularExpressions;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// CANLI MAÇ OLAYI ÇIKARIMI — global kaynak metninden KANONİK event üretir.
    ///
    /// TAMAMEN DETERMİNİSTİK: LLM/Gemma YOKTUR. Yalnız kaynağın kendi kelimeleri okunur.
    ///
    /// TANINMAYAN İÇERİK = OLAY DEĞİL (null döner) → Canlı Takip'e girmez. "Maçta ne oluyor?"
    /// sorusunun cevabı olmayan bir haber (kadro yorumu, transfer, yayın bilgisi) canlı olay
    /// olarak gösterilmez.
    ///
    /// UYDURMA YOK: dakika yalnız <see cref="MatchMinuteExtractor"/> ile metinden okunur,
    /// takım yalnız açık bir kalıpta ("X öne geçti") atanır, oyuncu yalnız kaynağın kendi
    /// "Gol: Ad Soyad" biçiminden alınır. Hiçbiri yoksa alan boş kalır.
    /// </summary>
    public static class MatchEventExtractor
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // ── Kanonik olay türleri (Canlı Takip sözlüğü) ───────────────────────────
        public const string Kickoff        = "KICKOFF";
        public const string Goal           = "GOAL";
        public const string PenaltyGoal    = "PENALTY_GOAL";
        public const string MissedPenalty  = "MISSED_PENALTY";
        public const string PenaltyAwarded = "PENALTY_AWARDED";
        public const string YellowCard     = "YELLOW_CARD";
        public const string SecondYellow   = "SECOND_YELLOW";
        public const string RedCard        = "RED_CARD";
        public const string Substitution   = "SUBSTITUTION";
        public const string Var            = "VAR";
        public const string VarDisallowed  = "VAR_GOAL_DISALLOWED";
        public const string HalfTime       = "HALF_TIME";
        public const string SecondHalf     = "SECOND_HALF";
        public const string ExtraTime      = "EXTRA_TIME";
        public const string FullTime       = "FULL_TIME";
        public const string ScoreUpdate    = "SCORE_UPDATE";

        public sealed class ExtractedEvent
        {
            public string Type { get; init; } = "";
            public int? Minute { get; init; }
            public string? MinuteLabel { get; init; }
            public string? Team { get; init; }
            public string? Player { get; init; }
            public int? HomeScore { get; init; }
            public int? AwayScore { get; init; }
        }

        /// <summary>
        /// Metinden kanonik olay. Olay tanınmazsa null — içerik canlı akışa ALINMAZ.
        /// </summary>
        public static ExtractedEvent? Extract(
            string? headline, string? summary, string? homeTeam, string? awayTeam)
        {
            var raw = ((headline ?? "") + " " + (summary ?? "")).Trim();
            if (raw.Length == 0) return null;

            var folded = NewsTextNormalizer.Fold(raw);
            if (folded.Length == 0) return null;

            var type = ResolveType(folded);
            var (home, away) = ExtractScore(raw, folded, homeTeam, awayTeam);

            // Olay türü yok ama kaynak GERÇEK skor yazmışsa bu da bir canlı gelişmedir.
            if (type == null)
            {
                if (home == null) return null;
                type = ScoreUpdate;
            }

            var (minute, minuteLabel) = MatchMinuteExtractor.Extract(headline, summary);

            return new ExtractedEvent
            {
                Type        = type,
                Minute      = minute,
                MinuteLabel = minuteLabel,
                Team        = ResolveTeam(folded, homeTeam, awayTeam),
                Player      = ResolvePlayer(raw),
                HomeScore   = home,
                AwayScore   = away
            };
        }

        // ── Olay türü ────────────────────────────────────────────────────────────

        /// <summary>
        /// SIRA ÖNEMLİ: en özgül kalıp önce. "penaltı golü" hem "penalti" hem "gol" içerir;
        /// "VAR ile iptal" hem "var" hem "gol" içerir.
        /// </summary>
        private static string? ResolveType(string f)
        {
            if (Has(f, "mac sona erdi", "mac bitti", "bitis dudugu", "full time", "fulltime", "final whistle"))
                return FullTime;

            if (Has(f, "uzatma dakikalari", "uzatmalara", "uzatma bolumu", "extra time"))
                return ExtraTime;

            if (Has(f, "ikinci yari basladi", "2 yari basladi", "second half started", "ikinci yarinin baslama"))
                return SecondHalf;

            if (Has(f, "devre arasi", "ilk yari sona erdi", "ilk yari bitti", "half time", "halftime"))
                return HalfTime;

            if (Has(f, "mac basladi", "ilk duduk", "kick off", "kickoff", "hakem baslama vurusu"))
                return Kickoff;

            // VAR ile iptal — golden ÖNCE gelmeli.
            if ((Has(f, "var") || Has(f, "video hakem")) && Has(f, "iptal", "disallowed", "gecersiz"))
                return VarDisallowed;
            if (Has(f, "gol iptal", "golu iptal", "iptal edilen gol"))
                return VarDisallowed;
            if (Has(f, "var incelemesi", "var kontrolu", "video hakem", "var review"))
                return Var;

            if (Has(f, "ikinci sari", "2 sari", "second yellow"))
                return SecondYellow;
            if (Has(f, "kirmizi kart", "red card", "kirmizi gordu"))
                return RedCard;
            if (Has(f, "sari kart", "yellow card"))
                return YellowCard;

            if (Has(f, "penalti kacirdi", "penalti kacti", "kacan penalti", "penaltiyi kacirdi", "missed penalty"))
                return MissedPenalty;
            if (Has(f, "penalti golu", "penaltidan gol", "penalty goal", "penaltiyi gole"))
                return PenaltyGoal;
            if (Has(f, "penalti kazandi", "penalti verildi", "penalti karari", "penalty awarded", "penalti noktasi"))
                return PenaltyAwarded;

            if (Has(f, "oyuncu degisikligi", "substitution", "oyundan cikti", "oyuna girdi"))
                return Substitution;

            // GOL en sonda: yukarıdaki özgül dallar elendikten sonra.
            // OLUMSUZ KALIPLAR: "golsüz", "gol yok", "gol sesi çıkmadı" gol DEĞİLDİR.
            if (Has(f, "golsuz", "gol yok", "gol sesi cikmadi", "gol atamadi", "goalless"))
                return null;
            if (Has(f, "gol", "goal", "agları havalandirdi", "one gecti", "esitligi buldu", "beraberligi buldu"))
                return Goal;

            return null;
        }

        private static bool Has(string folded, params string[] needles)
        {
            foreach (var n in needles)
                if (folded.Contains(n, StringComparison.Ordinal)) return true;
            return false;
        }

        // ── Skor ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// GERÇEK SKOR — yalnız iki takım adının YANINDA yazan sayılar. "Samsunspor: 0 -
        /// Göztepe: 1" ya da "Samsunspor 0-1 Göztepe". Takım adları eşleşmiyorsa skor
        /// ALINMAZ (başka maçın skoru bu maça yazılamaz).
        /// </summary>
        private static (int? Home, int? Away) ExtractScore(
            string raw, string folded, string? homeTeam, string? awayTeam)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam))
                return (null, null);

            var h = NewsTextNormalizer.Fold(homeTeam);
            var a = NewsTextNormalizer.Fold(awayTeam);
            if (h.Length < 3 || a.Length < 3) return (null, null);

            var pattern = Regex.Escape(h) + @"\s*:?\s*(\d{1,2})\s*[-–—]\s*" +
                          Regex.Escape(a) + @"\s*:?\s*(\d{1,2})";
            var m = Regex.Match(folded, pattern, Opts);
            if (!m.Success)
            {
                // "Samsunspor 0-1 Göztepe" biçimi.
                pattern = Regex.Escape(h) + @"\s*(\d{1,2})\s*[-–—]\s*(\d{1,2})\s*" + Regex.Escape(a);
                m = Regex.Match(folded, pattern, Opts);
            }
            if (!m.Success) return (null, null);

            if (int.TryParse(m.Groups[1].Value, out var hs) && int.TryParse(m.Groups[2].Value, out var as_)
                && hs <= 20 && as_ <= 20)
                return (hs, as_);

            return (null, null);
        }

        // ── Takım ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Takım YALNIZ açık bir eylem kalıbında atanır ("Göztepe öne geçti"). Her iki takım
        /// adı zaten başlıkta geçtiği için (katı maç kapısı) yakınlık tahmini YAPILMAZ.
        /// </summary>
        private static string? ResolveTeam(string folded, string? homeTeam, string? awayTeam)
        {
            foreach (var team in new[] { homeTeam, awayTeam })
            {
                if (string.IsNullOrWhiteSpace(team)) continue;
                var t = Regex.Escape(NewsTextNormalizer.Fold(team));
                if (t.Length < 3) continue;

                var rx = new Regex(t + @"\s*(?:one gecti|onde|farki|golu buldu|esitligi buldu|" +
                                        @"agları havalandirdi|skoru|kazandi)", Opts);
                if (rx.IsMatch(folded)) return team.Trim();
            }
            return null;
        }

        // ── Oyuncu ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Kaynağın KENDİ "Gol: Ad Soyad" biçimi. Serbest metinden isim TAHMİN EDİLMEZ.
        /// Ölçüldü: "Samsunspor - Goztepe Maçında Gol: Marius Mouandilmadji (30. dakika)".
        /// </summary>
        private static readonly Regex NamedEvent = new(
            @"(?:gol|goal|kırmızı kart|kirmizi kart|sarı kart|sari kart|penaltı|penalti)\s*[:\-–]\s*" +
            @"([\p{L}\.\s'’-]{3,40})", Opts);

        private static string? ResolvePlayer(string raw)
        {
            var m = NamedEvent.Match(raw);
            if (!m.Success) return null;

            var name = m.Groups[1].Value.Trim(' ', '-', '–', '.', '\'', '’');
            if (name.Length < 3 || name.Length > 40) return null;
            // İsim en az iki harf grubundan oluşmalı; tek kelimelik genel sözcükler elenir.
            if (!name.Contains(' ')) return null;
            return name;
        }
    }
}
