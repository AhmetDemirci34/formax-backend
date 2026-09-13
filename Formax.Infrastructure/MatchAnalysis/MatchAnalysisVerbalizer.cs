using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.Services.MatchAnalysis;
using Microsoft.Extensions.Logging;

namespace Formax.Infrastructure.MatchAnalysis
{
    /// <summary>
    /// LLM ÇEVİRMENİ — yalnız DOĞRULANMIŞ cümleleri doğal Türkçeye çevirir. Karar mercii değildir:
    /// her çeviri aynı kanıt anahtarlarıyla doğrulayıcıdan geçer; sayı/ad/yasak kalıp tutmazsa
    /// o cümle için deterministik metin kalır. Yalnız arka planda çağrılır.
    /// </summary>
    public static class MatchAnalysisVerbalizer
    {
        // ── LLM: yalnız doğrulanmış cümleleri doğal dile çevirir ─────────────────

        private static readonly System.Text.RegularExpressions.Regex Numbers = new(@"\d+(?:,\d+)?");

        /// <summary>
        /// SADAKAT KAPISI — çeviri yalnız dil değiştirir: orijinaldeki her sayı AYNI adetle geçmeli
        /// (iki takımın ayrı değerleri tek iddiaya birleştirilemez, sayı düşürülemez) ve orijinalde
        /// geçen takım adları aynen korunmalıdır. Ölçüldü: LLM "Le Mans maçlarında ortalama 3,7,
        /// Lens maçlarında 3" cümlesini "iki takımın maçlarında ortalama 3,7" diye birleştirdi.
        /// </summary>
        public static string? Fidelity(string original, string candidate, string home, string away)
        {
            static List<string> Nums(string t, string h, string a)
                => Numbers.Matches(t.Replace(h, " ").Replace(a, " ")).Select(m => m.Value).OrderBy(x => x).ToList();
            if (!Nums(original, home, away).SequenceEqual(Nums(candidate, home, away))) return "NumbersChanged";
            foreach (var name in new[] { home, away })
                if (original.Contains(name, StringComparison.Ordinal) && !candidate.Contains(name, StringComparison.Ordinal))
                    return "TeamNameDropped";
            return null;
        }

        public const string VerbalizeSystem =
            "Sen bir Türkçe futbol editörüsün. Sana kanıta dayalı kısa cümleler verilecek. " +
            "Her cümleyi daha akıcı Türkçeyle YENİDEN YAZ. KURALLAR: takım adlarını harfi harfine AYNEN yaz; " +
            "cümledeki bütün sayıları AYNEN koru, yeni sayı EKLEME, sayı hesaplama ya da birleştirme YAPMA; " +
            "yeni bilgi, oyuncu, sakatlık, tahmin, favori ya da üstünlük iddiası EKLEME; kesinlik/garanti " +
            "bildiren ya da oynama tavsiyesi veren söz kullanma; anlamı değiştirme. " +
            "YALNIZ şu JSON'u döndür: {\"items\":[{\"id\":\"...\",\"text\":\"...\"}]}";

        public static async Task<MatchAnalysisDocument?> VerbalizeAsync(
            ILLMClient llm, MatchAnalysisDocument doc, IReadOnlyList<EvidenceItem> evidence, string home, string away,
            List<(string, string)> rejections, TimeSpan timeoutAfter, ILogger log, CancellationToken ct)
        {
            var all = doc.AllSentences().ToList();
            var items = all.Select((s, i) => new { id = "s" + i, text = s.Text }).ToList();
            string raw;
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(timeoutAfter);
                raw = await llm.GenerateAsync(VerbalizeSystem, JsonSerializer.Serialize(new { items }), timeout.Token);
            }
            if (string.IsNullOrWhiteSpace(raw)) return null;

            Dictionary<string, string> rewritten;
            try
            {
                var start = raw.IndexOf('{'); var end = raw.LastIndexOf('}');
                if (start < 0 || end <= start) return null;
                using var parsed = JsonDocument.Parse(raw[start..(end + 1)]);
                rewritten = parsed.RootElement.GetProperty("items").EnumerateArray()
                    .Where(e => e.TryGetProperty("id", out _) && e.TryGetProperty("text", out _))
                    .ToDictionary(e => e.GetProperty("id").GetString() ?? "", e => e.GetProperty("text").GetString() ?? "");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                log.LogWarning("[ANALYSIS] LLM cevabı ayrıştırılamadı: {Msg}", ex.Message);
                return null;
            }

            var byKey = evidence.ToDictionary(e => e.Key, StringComparer.Ordinal);
            AnalysisSentence Pick(AnalysisSentence original, bool bothTeams, bool anyTeam)
            {
                var idx = all.IndexOf(original);
                if (!rewritten.TryGetValue("s" + idx, out var text) || string.IsNullOrWhiteSpace(text)) return original;
                var candidate = new AnalysisSentence(text.Trim(), original.EvidenceKeys);
                var reason = MatchAnalysisValidator.Reason(candidate, byKey, home, away, bothTeams, anyTeam)
                             ?? Fidelity(original.Text, candidate.Text, home, away);
                if (reason == null) return candidate;
                rejections.Add((candidate.Text, "Llm:" + reason));
                return original;
            }

            return new MatchAnalysisDocument
            {
                WhyWatch = doc.WhyWatch.Select(s => Pick(s, false, true)).ToList(),
                KeyBattle = doc.KeyBattle.Select(s => Pick(s, true, true)).ToList(),
                LineupImpact = doc.LineupImpact.Select(s => Pick(s, false, true)).ToList(),
                Uncertainty = doc.Uncertainty == null ? null : Pick(doc.Uncertainty, false, true),
                Scenarios = doc.Scenarios.Select(r => new ScenarioReason(r.Market,
                    r.Support == null ? null : Pick(r.Support, false, true),
                    r.Risk == null ? null : Pick(r.Risk, false, false))).ToList()
            };
        }
    }
}
