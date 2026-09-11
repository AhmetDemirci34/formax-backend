using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Services.Seasons;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// MEVCUT SEZON FORMU — tek hesap noktası.
    ///
    /// Girdi: sezon kapsamı (lig + sezon + pencere) içinde TAMAMLANMIŞ maçlar.
    /// Çıktı: <see cref="TeamSeasonFormDto"/> + backend'in yazdığı deterministik cümle.
    ///
    /// KURALLAR (ürün kararı 30.08.2026):
    ///  • Eksik maç önceki sezondan TAMAMLANMAZ. 2 maç oynandıysa cümle 2 maçtan söz eder.
    ///  • "Son 5 maç" ifadesi yalnız aynı sezon+ligden 5 tamamlanmış maç varken kurulur.
    ///  • Hiç maç yoksa "henüz tamamlanmış bir maç oynamadı" denir — 0 puan/0 performans DEĞİL.
    ///  • Cümle kesin başarı/başarısızlık iddiası taşımaz; yalnız sayılan gerçeği söyler.
    /// </summary>
    public static class TeamSeasonFormService
    {
        /// <summary>Anlatının "sınırlı veri" uyarısı vermesi gereken eşik (bu sayının ALTI).</summary>
        public const int LimitedSampleThreshold = 3;

        /// <summary>"Son 5 maç" ifadesi için gereken maç sayısı.</summary>
        public const int LastFiveWindow = 5;

        public static TeamSeasonFormDto Build(
            int teamId,
            string teamName,
            string leagueName,
            LeagueSeasonScope scope,
            DateTime windowEndUtc,
            IReadOnlyList<Match> settledMatches,
            Standings.SeasonDataCompleteness.Result? completeness = null,
            IReadOnlyList<int>? teamMissingResultMatchIds = null)
        {
            var dto = new TeamSeasonFormDto
            {
                TeamId = teamId,
                TeamName = teamName,
                LeagueId = scope.LeagueId,
                LeagueName = leagueName,
                SeasonYear = scope.SeasonYear,
                SeasonLabel = scope.Label,
                SeasonStartUtc = scope.StartUtc,
                WindowEndUtc = windowEndUtc,

                // LİG tamlığı TEŞHİS olarak taşınır — form cümlesinin kapısı DEĞİLDİR
                // (06.09.2026 kararı; bkz. TeamFormSampleQuality).
                SeasonExpectedFixtures = completeness?.Expected ?? 0,
                SeasonMissingFixtures = completeness?.Missing ?? 0,
                IsSeasonDataComplete = completeness?.IsComplete ?? true,

                // TAKIMI ETKİLEYEN eksik sonuç — değerlendirmeyi sınırlar, kapatmaz.
                TeamMissingResultMatchIds = (teamMissingResultMatchIds ?? Array.Empty<int>()).ToList()
            };
            dto.TeamMissingResultCount = dto.TeamMissingResultMatchIds.Count;

            // En yeni → en eski (kaynak sorgu da böyle sıralar; yine de garanti altına alınır).
            var ordered = settledMatches
                .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId)
                .OrderByDescending(m => m.MatchDate)
                .ToList();

            var sequence = new List<char>();

            foreach (var m in ordered)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;
                var split = isHome ? dto.Home : dto.Away;

                dto.Played++;
                dto.GoalsFor += gf;
                dto.GoalsAgainst += ga;
                split.Played++;
                split.GoalsFor += gf;
                split.GoalsAgainst += ga;
                dto.MatchIds.Add(m.Id);

                if (gf > ga)      { dto.Won++;   split.Won++;   sequence.Add('G'); }
                else if (gf == ga){ dto.Drawn++; split.Drawn++; sequence.Add('B'); }
                else              { dto.Lost++;  split.Lost++;  sequence.Add('M'); }
            }

            if (ordered.Count > 0)
            {
                dto.LastMatchUtc = ordered[0].MatchDate;
                dto.FirstMatchUtc = ordered[^1].MatchDate;
            }

            dto.ResultSequence = string.Join(" ", sequence);
            dto.Sentence = ComposeSentence(dto, ordered);
            dto.LimitationNote = LimitationNote(dto);
            return dto;
        }

        /// <summary>
        /// Boş kapsam (sezon çözülemedi / lig bilinmiyor) için "veri yok" kaydı.
        /// Sıfır performans DEĞİL, bilgisizliktir: <see cref="TeamSeasonFormDto.HasNoData"/> true.
        /// </summary>
        public static TeamSeasonFormDto Empty(int teamId, string teamName, string leagueName)
            => new()
            {
                TeamId = teamId,
                TeamName = teamName,
                LeagueName = leagueName,
                Sentence = string.Empty
            };

        /// <summary>
        /// DETERMİNİSTİK TÜRKÇE CÜMLE — anlatının kaynağı. Model bu cümleyi yeniden
        /// hesaplamaz; kopyalar veya bağlamına yerleştirir.
        /// </summary>
        private static string ComposeSentence(TeamSeasonFormDto dto, IReadOnlyList<Match> ordered)
        {
            var lig = LeagueLocative(dto.LeagueName);

            // ÖRNEKLEM KALİTESİNE GÖRE DİL (06.09.2026 kararı).
            //
            // Kapı artık LİG verisinin tamlığı DEĞİL, bu takımın kendi örneklemidir.
            // Ligdeki ilgisiz bir maçın eksik sonucu, bu takımın oynayıp bitirdiği
            // maçların gerçekliğini değiştirmez ve o maçları gizleyemez.
            //
            // Hiçbir dalda teknik sistem terimi geçmez: "IsComplete", "settle window",
            // "AllowsGeneralization", "N lig maçı bekliyor" kullanıcı metnine GİRMEZ.

            // 0 MAÇ — genelleme yok, sıfır performans da yok: bilgisizlik.
            if (dto.Played == 0)
                return $"{dto.TeamName} için bu sezon {lig} tamamlanmış lig maçı bulunmuyor.";

            // 5+ MAÇ — anlatı SON 5'i konuşabilir (ifade ancak o zaman doğrudur).
            if (dto.Played >= LastFiveWindow)
            {
                var last5 = ordered.Take(LastFiveWindow).ToList();
                var (w, d, l) = Tally(dto.TeamId, last5);
                return $"{dto.TeamName} bu sezon {lig} son {LastFiveWindow} maçında {Breakdown(w, d, l)} aldı.";
            }

            // 3–4 MAÇ — sınırlılık AÇIKÇA söylenir, ölçülü değerlendirme yapılır.
            //
            // İLGİ HÂLİ EKİ KULLANILMAZ (ölçüldü 06.09.2026, gerçek /detail çıktısı).
            // Kalıp önce "Trabzonspor'un …" biçimindeydi ve ek kurala göre üretiliyordu.
            // Türkçe adlarda doğru çalıştı, ama depo Türkçe adlardan ibaret değil:
            //   • "Gençlerbirliği S.K."  → "S.K.'in"        (kısaltma harf harf okunur)
            //   • "Manchester City"      → "City'in"        (okunuşu ünlüyle biter → 'nin)
            //   • "Manchester United"    → "United'in"      (okunuşa göre 'ın)
            // Yabancı adlarda ek YAZILIŞA değil OKUNUŞA bağlıdır; harf temelli hiçbir
            // kural bunu güvenilir üretemez ve her yanlış ek kullanıcının gördüğü metni
            // bozar. Bu yüzden ek GEREKTİRMEYEN tek bir kalıp kullanılır: her ad için
            // dilbilgisel olarak doğrudur ve spec'in istediği iki şeyi aynen söyler —
            // örneklemin SINIRLI olduğunu ve GERÇEK sayıları.
            if (dto.Played >= LimitedSampleThreshold)
                return $"{dto.TeamName} bu sezon tamamlanan {dto.Played} lig maçlık sınırlı örneklemde " +
                       $"{Breakdown(dto.Won, dto.Drawn, dto.Lost)} elde etti.";

            // 1–2 MAÇ — YALNIZ gerçek sayılar. "Formda / düşüşte / favori / üstün /
            // momentum" türü hiçbir genelleme kurulmaz.
            var kapsam = dto.Played == 1 ? "tamamlanan 1 lig maçında" : $"tamamlanan {dto.Played} lig maçında";
            return $"{dto.TeamName} bu sezon {kapsam} {Breakdown(dto.Won, dto.Drawn, dto.Lost)} aldı.";
        }

        /// <summary>
        /// TAKIMI ETKİLEYEN EKSİK SONUÇ NOTU — kullanıcı diliyle, teknik terim yok.
        ///
        /// Yalnız incelenen takımın kendi maçlarından biri kesinleşmemişse döner;
        /// ligin geri kalanı için HİÇBİR not üretilmez. Not, form değerlendirmesini
        /// KAPATMAZ, yalnız kapsamının neden sınırlı olduğunu söyler.
        /// Boşsa arayüz hiçbir şey göstermez.
        /// </summary>
        public static string LimitationNote(TeamSeasonFormDto dto)
            => dto is { TeamMissingResultCount: > 0 }
                ? "Takımın yakın tarihli bir maç sonucu henüz doğrulanmadığı için " +
                  "değerlendirme mevcut kesinleşmiş maçlarla sınırlandırıldı."
                : string.Empty;

        private static (int W, int D, int L) Tally(int teamId, IReadOnlyList<Match> matches)
        {
            int w = 0, d = 0, l = 0;
            foreach (var m in matches)
            {
                var isHome = m.HomeTeamId == teamId;
                var gf = isHome ? m.HomeScore : m.AwayScore;
                var ga = isHome ? m.AwayScore : m.HomeScore;
                if (gf > ga) w++; else if (gf == ga) d++; else l++;
            }
            return (w, d, l);
        }

        /// <summary>"1 galibiyet ve 1 beraberlik" — sıfır olan sonuç YAZILMAZ.</summary>
        private static string Breakdown(int w, int d, int l)
        {
            var parts = new List<string>(3);
            if (w > 0) parts.Add($"{w} galibiyet");
            if (d > 0) parts.Add($"{d} beraberlik");
            if (l > 0) parts.Add($"{l} mağlubiyet");
            if (parts.Count == 0) return "sonuç";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " ve " + parts[^1];
        }

        /// <summary>
        /// "La Liga" → "La Liga'da", "Süper Lig" → "Süper Lig'de". Türkçe ünlü uyumu +
        /// sert ünsüz benzeşmesi. Lig adı yoksa "ligde" denir (uydurma ad üretilmez).
        /// </summary>
        public static string LeagueLocative(string? leagueName)
        {
            var name = (leagueName ?? "").Trim();
            if (name.Length == 0) return "ligde";

            const string back = "aıouAIOU";   // kalın ünlüler
            const string front = "eiöüEİÖÜ";  // ince ünlüler
            const string hard = "pçtkfhsşPÇTKFHSŞ";

            char? lastVowel = null;
            foreach (var ch in name)
            {
                if (back.Contains(ch) || front.Contains(ch)) lastVowel = ch;
            }

            var thick = lastVowel == null || back.Contains(lastVowel.Value);
            var lastLetter = name[^1];
            var devoiced = hard.Contains(lastLetter);

            var suffix = (thick, devoiced) switch
            {
                (true, true)   => "'ta",
                (true, false)  => "'da",
                (false, true)  => "'te",
                (false, false) => "'de"
            };

            return name + suffix;
        }
    }
}
