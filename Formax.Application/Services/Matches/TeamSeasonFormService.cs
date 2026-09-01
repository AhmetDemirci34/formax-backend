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
            Standings.SeasonDataCompleteness.Result? completeness = null)
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

                // Ligin bu sezonki verisi tam mı? Eksikse aşağıda GENEL DEĞERLENDİRME
                // yapılmaz; cümle yalnız elimizdeki doğrulanmış maç sayısını söyler.
                SeasonExpectedFixtures = completeness?.Expected ?? 0,
                SeasonMissingFixtures = completeness?.Missing ?? 0,
                IsSeasonDataComplete = completeness?.IsComplete ?? true
            };

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

            // VERİ EKSİKSE GENELLEME YOK (30.08.2026 kararı). Ligin bu sezonki sonuçlarının
            // bir kısmı depoda kesinleşmemişken "form" cümlesi kurmak, eksik veriden
            // başarı/başarısızlık üretmek olurdu. Elimizdeki doğrulanmış maç sayısı söylenir,
            // değerlendirme YAPILMAZ.
            if (!dto.IsSeasonDataComplete)
            {
                return dto.Played == 0
                    ? $"FORMAX veritabanında {dto.TeamName} için bu sezon doğrulanmış lig maçı bulunmuyor. " +
                      "Sezon verileri henüz tamamlanmadığı için genel form değerlendirmesi yapılmıyor."
                    : $"FORMAX veritabanında bu sezon için doğrulanmış {dto.Played} lig maçı bulunuyor. " +
                      "Sezon verileri henüz tamamlanmadığı için genel form değerlendirmesi yapılmıyor.";
            }

            if (dto.Played == 0)
                return $"{dto.TeamName} bu sezon {lig} henüz tamamlanmış bir maç oynamadı.";

            // 5+ maç varsa anlatı SON 5'i konuşur (ifade ancak o zaman doğrudur).
            if (dto.Played >= LastFiveWindow)
            {
                var last5 = ordered.Take(LastFiveWindow).ToList();
                var (w, d, l) = Tally(dto.TeamId, last5);
                return $"{dto.TeamName} bu sezon {lig} son {LastFiveWindow} maçında {Breakdown(w, d, l)} aldı.";
            }

            var kapsam = dto.Played == 1 ? "tamamlanan 1 maçta" : $"tamamlanan {dto.Played} maçta";
            return $"{dto.TeamName} bu sezon {lig} {kapsam} {Breakdown(dto.Won, dto.Drawn, dto.Lost)} aldı.";
        }

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
