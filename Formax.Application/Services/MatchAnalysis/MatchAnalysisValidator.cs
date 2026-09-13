using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.MatchAnalysis
{
    /// <summary>Doğrulama sonucu: kabul edilen belge + reddedilen cümleler (gerekçeli).</summary>
    public sealed record AnalysisValidationResult(
        MatchAnalysisDocument Accepted,
        IReadOnlyList<(string Text, string Reason)> Rejected);

    /// <summary>
    /// KANIT DOĞRULAYICI — kullanıcıya giden her cümle buradan geçer (deterministik ya da LLM).
    ///
    /// Bir cümle REDDEDİLİR:
    ///  • kanıt anahtarı yoksa ya da anahtar kanıt kümesinde değilse ("NoEvidence"/"UnknownEvidence");
    ///  • içindeki bir sayı, dayandığı kanıtların değerlerinde yoksa ("NumberNotInEvidence") —
    ///    LLM ya da şablon sayı UYDURAMAZ, hesaplayamaz;
    ///  • yasak boş kalıp içeriyorsa ("ForbiddenPhrase");
    ///  • teknik/debug metni sızdırıyorsa ("TechnicalText": O4, G2, AG, YG, AV, null, {…});
    ///  • bölüm için gereken takım adı yoksa ("MissingTeamName").
    /// Kesinlik/garanti ve "şunu oyna" dili de yasaktır.
    /// </summary>
    public static class MatchAnalysisValidator
    {
        public static readonly IReadOnlyList<string> ForbiddenPhrases = new[]
        {
            "ev sahibi olarak",
            "ağırlıyor",
            "maç çevresinde konuşulacak",
            "konuşulacak gelişmeler",
            "hücum tarafı savunmadan",
            "savunma istikrarı düşük",
            "bu yönü destekliyor",
            "geçmiş maçlar gollü",
            "güncel tablo",
            "oyna", "oynayın", "oynanmalı", "kupon",
            "kesin", "garanti", "banko", "mutlaka"
        };

        private static readonly Regex Technical = new(
            @"\b[OGBM]\s?\d+\b|\bAG\b|\bYG\b|\bAV\b|\bnull\b|\bNaN\b|\bundefined\b|completeness|[{}_<>]|\bform:|\bvenue:|\bstanding:",
            RegexOptions.Compiled);

        private static readonly Regex Numbers = new(@"\d+(?:,\d+)?", RegexOptions.Compiled);

        public static AnalysisValidationResult Validate(
            MatchAnalysisDocument doc, IReadOnlyList<EvidenceItem> evidence, string homeName, string awayName)
        {
            var byKey = evidence.ToDictionary(e => e.Key, StringComparer.Ordinal);
            var rejected = new List<(string, string)>();

            AnalysisSentence? Check(AnalysisSentence? s, bool needBothTeams, bool needAnyTeam)
            {
                if (s == null) return null;
                var reason = Reason(s, byKey, homeName, awayName, needBothTeams, needAnyTeam);
                if (reason == null) return s;
                rejected.Add((s.Text, reason));
                return null;
            }

            var result = new MatchAnalysisDocument
            {
                WhyWatch = doc.WhyWatch.Select(s => Check(s, false, true)).OfType<AnalysisSentence>().Take(3).ToList(),
                KeyBattle = doc.KeyBattle.Select(s => Check(s, true, true)).OfType<AnalysisSentence>().ToList(),
                LineupImpact = doc.LineupImpact.Select(s => Check(s, false, true)).OfType<AnalysisSentence>().ToList(),
                Uncertainty = Check(doc.Uncertainty, false, true),
                Scenarios = doc.Scenarios
                    .Select(r => new ScenarioReason(r.Market, Check(r.Support, false, true), Check(r.Risk, false, false)))
                    .Where(r => r.Support != null || r.Risk != null)
                    .ToList()
            };
            return new AnalysisValidationResult(result, rejected);
        }

        public static string? Reason(
            AnalysisSentence s, IReadOnlyDictionary<string, EvidenceItem> byKey, string home, string away,
            bool needBothTeams, bool needAnyTeam)
        {
            if (string.IsNullOrWhiteSpace(s.Text)) return "Empty";
            if (s.EvidenceKeys == null || s.EvidenceKeys.Count == 0) return "NoEvidence";
            if (s.EvidenceKeys.Any(k => !byKey.ContainsKey(k))) return "UnknownEvidence";

            var lower = s.Text.ToLower(new CultureInfo("tr-TR"));
            foreach (var phrase in ForbiddenPhrases)
                if (Regex.IsMatch(lower, $@"(?<![\p{{L}}]){Regex.Escape(phrase)}(?![\p{{L}}])")) return "ForbiddenPhrase";

            if (Technical.IsMatch(s.Text)) return "TechnicalText";

            var allowed = AllowedNumbers(s.EvidenceKeys.Select(k => byKey[k]));
            // Takım adındaki rakamlar (ör. "Schalke 04", "Mainz 05") sayı sayılmaz.
            var textWithoutNames = s.Text.Replace(home, " ").Replace(away, " ");
            foreach (Match m in Numbers.Matches(textWithoutNames))
                if (!allowed.Contains(m.Value)) return "NumberNotInEvidence";

            var hasHome = s.Text.Contains(home, StringComparison.Ordinal);
            var hasAway = s.Text.Contains(away, StringComparison.Ordinal);
            if (needBothTeams && !(hasHome && hasAway)) return "MissingTeamName";
            if (needAnyTeam && !(hasHome || hasAway)) return "MissingTeamName";
            return null;
        }

        public static HashSet<string> AllowedNumbers(IEnumerable<EvidenceItem> items)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in items)
            {
                foreach (var v in e.Values.Values) set.Add(MatchEvidenceBuilder.Num(v));
                if (e.Texts != null)
                    foreach (var t in e.Texts.Values)
                        foreach (Match m in Numbers.Matches(t)) set.Add(m.Value);
            }
            return set;
        }

        /// <summary>Kullanıcı metninde teknik/debug izi var mı? (rapor sayacı için)</summary>
        public static bool HasTechnicalText(string text) => Technical.IsMatch(text);

        /// <summary>Metin yasak boş kalıp içeriyor mu? (rapor sayacı için)</summary>
        public static bool HasForbiddenPhrase(string text)
        {
            var lower = text.ToLower(new CultureInfo("tr-TR"));
            return ForbiddenPhrases.Any(p => Regex.IsMatch(lower, $@"(?<![\p{{L}}]){Regex.Escape(p)}(?![\p{{L}}])"));
        }
    }

    /// <summary>
    /// METİN BENZERLİĞİ — normalize edilmiş kelime 3-gramlarında Jaccard (yerel, harici bağımlılık yok).
    /// </summary>
    public static class AnalysisSimilarity
    {
        /// <summary>Bu eşiğin üstündeki analiz önceki bir maça "aşırı benzer" sayılır.</summary>
        public const double MaxAllowed = 0.55;

        public static string Normalize(string text)
        {
            var decomposed = text.ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            }
            return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static HashSet<string> Shingles(string text, int size = 3)
        {
            var words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (words.Length < size) { if (words.Length > 0) set.Add(string.Join(' ', words)); return set; }
            for (var i = 0; i + size <= words.Length; i++) set.Add(string.Join(' ', words, i, size));
            return set;
        }

        public static double Jaccard(string a, string b)
        {
            var sa = Shingles(a); var sb = Shingles(b);
            if (sa.Count == 0 || sb.Count == 0) return 0;
            var inter = sa.Count(sb.Contains);
            return (double)inter / (sa.Count + sb.Count - inter);
        }

        /// <summary>Belgenin tek metin hali (benzerlik ve tekrar ölçümü için).</summary>
        public static string Flatten(MatchAnalysisDocument doc)
            => string.Join(" ", doc.AllSentences().Select(s => s.Text));
    }
}
