using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Formax.Application.Services.MatchAnalysis;
using Formax.Domain.Constants;

namespace Formax.Application.Services.Outcomes
{
    public sealed record AnalysisViolation(string Kind, string Section, string Text);

    public sealed record AnalysisConsistencyResult(MatchAnalysisDto Analysis, IReadOnlyList<AnalysisViolation> Violations);

    /// <summary>
    /// AI ANALİZİ ↔ OLASILIK KARTI ÇELİŞKİ KAPISI — analiz ve kartlar aynı snapshot'tan okunur; kullanıcıya giden her analiz cümlesi
    /// güncel snapshot'ın ana kartları, uygunluk durumu ve gerekçe kodlarıyla karşılaştırılır. Çelişen cümle GÖNDERİLMEZ (deterministik
    /// olarak düşürülür) ve ihlal döndürülür (arka plan işi teşhis kaydına yazar). Saf fonksiyon: I/O, LLM, dış istek yok.
    ///
    /// Yasak çelişkiler:
    ///  • gol kartı "x.5 Alt" iken "yüksek skorlu / gollü" iddiası; "x.5 Üst" iken "düşük skorlu" iddiası;
    ///  • "Karşılıklı Gol Yok" iken "iki takım da gol buluyor/üretiyor" vurgusu;
    ///  • sonuç kartı bir tarafı öne çıkarırken diğer tarafı favori gösteren cümle;
    ///  • Limited/Disabled tahminde kesinlik/favori dili ve market senaryoları;
    ///  • her maça uyan genel şablon ve bahis dili.
    /// </summary>
    public static class AnalysisConsistencyValidator
    {
        private static readonly Regex HighScoring = new(@"yüksek skor|gollü|bol gol|gol ortalaması iki tarafta da yüksek|gol yağ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LowScoring = new(@"düşük skorlu|az gollü|golsüz|gol çıkmayan", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex BothScore = new(@"iki takım da gol bul|iki takımın da gol|iki taraf da gol|karşılıklı gol", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Certainty = new(@"\bfavori|açık ara|net üstün|kesin|garanti|mutlaka|rahat kazan|kazanması bekleniyor|banko", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Generic = new(@"maç çevresinde konuşulacak|konuşulacak gelişmeler|hücum tarafı savunmadan", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Betting = new(@"\bkupon|\boyna(yın|nmalı)?\b|\bbahis\b|\boran(ı|lar)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static AnalysisConsistencyResult Validate(MatchAnalysisDto analysis, OutcomeSnapshotDto snapshot, string homeName, string awayName)
        {
            var violations = new List<AnalysisViolation>();
            if (analysis.Status != "Ready") return new(analysis, violations);

            var enabled = snapshot.PredictionEligibility == PredictionEligibilities.Enabled && snapshot.Status == "Available" && snapshot.MainCards.Count > 0;
            var goals = enabled ? snapshot.MainCards.FirstOrDefault(c => c.Family == OutcomeFamilies.Goals) : null;
            var btts = enabled ? snapshot.MainCards.FirstOrDefault(c => c.Family == OutcomeFamilies.Btts) : null;
            var result = enabled ? snapshot.MainCards.FirstOrDefault(c => c.Family == OutcomeFamilies.Result) : null;

            string? Check(string section, string text, bool narrative)
            {
                string? kind = null;
                if (Generic.IsMatch(text)) kind = "GENERIC_TEMPLATE";
                else if (Betting.IsMatch(text)) kind = "BETTING_LANGUAGE";
                else if (!enabled && Certainty.IsMatch(text)) kind = "NOT_ELIGIBLE_CERTAIN_LANGUAGE";
                else if (narrative && goals?.Market.EndsWith("Alt", StringComparison.Ordinal) == true && HighScoring.IsMatch(text)) kind = "GOALS_UNDER_VS_HIGH_SCORING_TEXT";
                else if (narrative && goals?.Market.EndsWith("Üst", StringComparison.Ordinal) == true && LowScoring.IsMatch(text)) kind = "GOALS_OVER_VS_LOW_SCORING_TEXT";
                else if (narrative && btts?.MarketKey == OddsMarketKeys.BttsNo && BothScore.IsMatch(text)) kind = "BTTS_NO_VS_BOTH_SCORE_TEXT";
                else if (result != null && SideContradiction(result, text, homeName, awayName)) kind = "RESULT_SIDE_CONTRADICTION";
                if (kind == null) return text;
                violations.Add(new AnalysisViolation(kind, section, text));
                return null;
            }

            var filtered = new MatchAnalysisDto
            {
                Status = analysis.Status,
                GeneratedAtUtc = analysis.GeneratedAtUtc,
                WhyWatch = analysis.WhyWatch.Select(t => Check("WhyWatch", t, true)).OfType<string>().ToList(),
                KeyBattle = analysis.KeyBattle.Select(t => Check("KeyBattle", t, true)).OfType<string>().ToList(),
                LineupImpact = analysis.LineupImpact.Select(t => Check("LineupImpact", t, true)).OfType<string>().ToList(),
                Uncertainty = analysis.Uncertainty == null ? null : Check("Uncertainty", analysis.Uncertainty, false)
            };

            // Senaryo gerekçeleri yalnız GÖSTERİLEN ana kartların marketleri için ve yalnız Enabled tahminde taşınır. Risk cümlesi
            // bilerek karşı kanıttır (etiketli), çelişki sayılmaz; destek cümlesi aynı kontrollerden geçer.
            if (enabled)
            {
                var markets = snapshot.MainCards.Select(c => c.Market).ToHashSet(StringComparer.Ordinal);
                filtered.Scenarios = analysis.Scenarios.Where(s => markets.Contains(s.Market)).Select(s => new MatchAnalysisScenarioDto
                {
                    Market = s.Market,
                    Support = s.Support == null ? null : Check("Scenario:" + s.Market, s.Support, false),
                    Risk = s.Risk
                }).Where(s => s.Support != null || s.Risk != null).ToList();
            }

            var empty = filtered.WhyWatch.Count == 0 && filtered.KeyBattle.Count == 0 && filtered.LineupImpact.Count == 0
                        && filtered.Uncertainty == null && filtered.Scenarios.Count == 0;
            if (empty && violations.Count > 0) filtered.Status = "Unavailable";
            return new(filtered, violations);
        }

        /// <summary>Sonuç kartı ev sahibini öne çıkarırken deplasmanı favori/üstün gösteren (ya da tersi) cümle.</summary>
        private static bool SideContradiction(OutcomeCandidateDto result, string text, string home, string away)
        {
            if (!Regex.IsMatch(text, @"favori|üstün|önde görünüyor", RegexOptions.IgnoreCase)) return false;
            bool Mentions(string name) => !string.IsNullOrWhiteSpace(name) && text.Contains(name, StringComparison.OrdinalIgnoreCase);
            return result.MarketKey switch
            {
                OddsMarketKeys.Ms1 => Mentions(away) && !Mentions(home) || Regex.IsMatch(text, @"deplasman (takımı )?(favori|üstün)", RegexOptions.IgnoreCase),
                OddsMarketKeys.Ms2 => Mentions(home) && !Mentions(away) || Regex.IsMatch(text, @"ev sahibi (favori|üstün)", RegexOptions.IgnoreCase),
                _ => false
            };
        }

        /// <summary>
        /// KART GEREKÇESİ ↔ GEREKÇE KODU — sonuç kartı metni "reyting üstünlüğü" diyorsa ilgili tarafın RESULT_ kodu bulunmalı;
        /// gol kartı "yüksek/düşük" diyorsa GOALS_ kodu. Uymayan gerekçe null yapılır ve ihlal döner.
        /// </summary>
        public static IReadOnlyList<AnalysisViolation> ValidateCardReasons(OutcomeSnapshotDto snapshot)
        {
            var list = new List<AnalysisViolation>();
            foreach (var c in snapshot.MainCards)
            {
                if (c.Reason == null) continue;
                var codes = c.ReasonCodes;
                var bad =
                    (c.Reason.Contains("ev sahibinin reyting üstünlüğü") && !codes.Any(x => x is "RESULT_HOME_STRONGER" or "RESULT_HOME_CLEAR_FAVOURITE")) ||
                    (c.Reason.Contains("deplasman takımının reyting üstünlüğü") && !codes.Any(x => x is "RESULT_AWAY_STRONGER" or "RESULT_AWAY_CLEAR_FAVOURITE")) ||
                    (c.Reason.Contains("birbirine yakın") && !codes.Any(x => x is "RESULT_BALANCED"));
                if (!bad) continue;
                list.Add(new AnalysisViolation("REASON_WITHOUT_CODE", "Card:" + c.Family, c.Reason));
                c.Reason = null;
            }
            return list;
        }
    }
}
