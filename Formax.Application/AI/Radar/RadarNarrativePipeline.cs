using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.AI.Radar.Reasoning;
using Microsoft.Extensions.Logging;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v3 — anlatı hattı (pipeline):
    ///   Context → ReasoningEngine → Intelligence Pack → Prompt → LLM → Guard → Snapshot.
    ///
    /// LLM artık ham context değil, Reasoning Layer'ın hazırladığı Intelligence Pack'i
    /// okur. LLM boş/geçersiz/kapalı olduğunda deterministik fallback üretir → çıktı asla
    /// boş kalmaz. Aynı context için sonuç cache'lenir.
    /// </summary>
    public sealed class RadarNarrativePipeline
    {
        private readonly ILLMClient _llm;
        private readonly ReasoningEngine _reasoning;
        private readonly RadarPromptComposer _composer;
        private readonly RadarOutputGuard _guard;
        private readonly IRadarNarrativeStore _store;
        private readonly ILogger<RadarNarrativePipeline> _logger;

        public RadarNarrativePipeline(
            ILLMClient llm,
            ReasoningEngine reasoning,
            RadarPromptComposer composer,
            RadarOutputGuard guard,
            IRadarNarrativeStore store,
            ILogger<RadarNarrativePipeline> logger)
        {
            _llm = llm;
            _reasoning = reasoning;
            _composer = composer;
            _guard = guard;
            _store = store;
            _logger = logger;
        }

        public async Task<RadarNarrativeResult> GenerateAsync(
            MatchIntelligenceContext ctx,
            RadarSurface surface,
            bool aiAllowed,
            CancellationToken ct = default)
        {
            var key = $"{surface}:{ctx.MatchId}:{Hash(ctx.ToPromptJson())}";

            if (_store.TryGet(key, out var cached))
                return cached;

            // Reasoning Layer: ham context'ten Intelligence Pack üret (FORMAX'ın beyni).
            var pack = _reasoning.Build(ctx);

            RadarNarrativeResult? result = null;

            if (aiAllowed)
            {
                try
                {
                    var raw = await _llm.GenerateAsync(_composer.System(surface), _composer.User(surface, pack), ct);
                    if (!string.IsNullOrWhiteSpace(raw))
                        result = ParseAndGuard(raw, surface);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RADAR] LLM anlatısı başarısız — fallback");
                }
            }

            result ??= Fallback(ctx, pack, surface);
            result.ReasoningConfidence = pack.ReasoningConfidence; // her iki yolda da Reasoning skoru
            _store.Set(key, result);
            return result;
        }

        // ── LLM JSON çıktısını parse et + guard uygula ─────────────────────────
        private RadarNarrativeResult? ParseAndGuard(string raw, RadarSurface surface)
        {
            var json = ExtractJson(raw);
            if (json == null) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var r = new RadarNarrativeResult { IsAiGenerated = true };

                if (surface == RadarSurface.Discover)
                {
                    r.RadarSummary = Clean(Str(root, "radarSummary"), 320);
                    // Radarın Öne Çıkardıkları — 2-4 dinamik bulgu (kısa cümleler).
                    r.Highlights = CleanList(Arr(root, "highlights"), 72, 4);
                    r.ScenarioReasons = Reasons(root, "scenarioReasons");
                    if (!_guard.IsAcceptable(r.RadarSummary)) return null;
                }
                else
                {
                    r.MatchReport = Clean(Str(root, "matchReport"), 700);
                    r.WhyThisMatch = Clean(Str(root, "whyThisMatch"), 280);
                    r.ReasoningSummary = Clean(Str(root, "reasoningSummary"), 320);
                    r.NewsSummary = Clean(Str(root, "newsSummary"), 320);
                    r.SocialSummary = Clean(Str(root, "socialSummary"), 280);
                    r.StatisticalSummary = Clean(Str(root, "statisticalSummary"), 320);
                    r.KeyInsights = CleanList(Arr(root, "keyInsights"), 90, 3);
                    r.ScenarioExplanations = Reasons(root, "scenarioExplanations");
                    r.EvidenceSummary = Clean(Str(root, "evidenceSummary"), 320);
                    if (!_guard.IsAcceptable(r.MatchReport)) return null;
                }

                return r;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[RADAR] LLM JSON parse hatası");
                return null;
            }
        }

        private string Clean(string? s, int max)
        {
            if (_guard.ContainsHardBan(s)) return string.Empty;
            return _guard.Sanitize(s, max);
        }

        private List<string> CleanList(IEnumerable<string> items, int maxLen, int maxCount) =>
            items.Select(x => Clean(x, maxLen))
                 .Where(x => _guard.IsAcceptable(x))
                 .Take(maxCount)
                 .ToList();

        private List<RadarScenarioReason> Reasons(JsonElement root, string prop)
        {
            var list = new List<RadarScenarioReason>();
            if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var el in arr.EnumerateArray())
            {
                var market = Clean(el.TryGetProperty("market", out var m) ? m.GetString() : null, 40);
                var reason = Clean(el.TryGetProperty("reason", out var rs) ? rs.GetString() : null, 160);
                if (!string.IsNullOrWhiteSpace(market) && _guard.IsAcceptable(reason))
                    list.Add(new RadarScenarioReason { Market = market, Reason = reason });
            }
            return list;
        }

        // ── Deterministik fallback (LLM yokken) — Reasoning Pack'ten güvenli üretim ──
        private static RadarNarrativeResult Fallback(
            MatchIntelligenceContext ctx, IntelligencePack pack, RadarSurface surface)
        {
            var r = new RadarNarrativeResult { IsAiGenerated = false };
            var stronger = ctx.Form.HomeFormScore >= ctx.Form.AwayFormScore ? ctx.HomeTeam : ctx.AwayTeam;

            var summary =
                $"{ctx.HomeTeam} – {ctx.AwayTeam} karşılaşması güncel veri akışında öne çıkıyor; " +
                $"form göstergeleri ve {(ctx.News.Volume24h > 0 ? "haber hareketliliği" : "istatistiksel tablo")} birlikte değerlendirildiğinde takip edilmeye değer bir eşleşme.";

            var findings = DeriveFindings(ctx, stronger);

            var scenarioReasons = ctx.Scenarios
                .Select(s => new RadarScenarioReason
                {
                    Market = s.Market,
                    Reason = s.EvidenceTags.Count > 0
                        ? $"{string.Join(", ", s.EvidenceTags.Take(2))} bu senaryoyu öne çıkarıyor."
                        : "İstatistik ve form verisi bu senaryoyu destekliyor."
                }).ToList();

            if (surface == RadarSurface.Discover)
            {
                r.RadarSummary = summary;
                r.Highlights = findings.Take(4).ToList();
                r.ScenarioReasons = scenarioReasons;
            }
            else
            {
                r.MatchReport = summary +
                    " İki takımın güncel formu ve geçmiş karşılaşma dengesi maçın seyri hakkında ipucu veriyor.";
                r.WhyThisMatch = $"Veri akışı, {stronger} öne çıksa da dengenin belirleyici olabileceğini gösteriyor.";
                r.ReasoningSummary = BuildReasoningFallback(pack);
                r.NewsSummary = ctx.News.Volume24h > 0
                    ? "Haber gündemi bu karşılaşma çevresinde belirgin biçimde hareketli."
                    : "Şu an öne çıkan yoğun bir haber akışı görünmüyor.";
                // Dürüst: yalnız FORMAX iç etkileşimine dayanır; sinyal yoksa üretme.
                r.SocialSummary = ctx.Social.CommunityInterest <= 0
                    ? ""
                    : ctx.Social.Level == "Yüksek"
                        ? "FORMAX kullanıcılarının bu maça ilgisi belirgin biçimde yüksek."
                        : ctx.Social.Level == "Orta"
                            ? "FORMAX kullanıcılarının ilgisi ortalama seviyede."
                            : "FORMAX kullanıcılarından sınırlı bir ilgi var.";
                r.StatisticalSummary =
                    $"Gol üretimi ve savunma verileri, {stronger} lehine sınırlı bir üstünlüğe işaret ediyor.";
                r.KeyInsights = findings.Take(3).ToList();
                r.ScenarioExplanations = scenarioReasons;
                r.EvidenceSummary = BuildEvidenceFallback(pack);
            }

            return r;
        }

        // Reasoning özeti (fallback) — pack'teki en güçlü sinyaller + ilk çelişki.
        private static string BuildReasoningFallback(IntelligencePack pack)
        {
            var basis = pack.Signals.Count == 0
                ? "Mevcut veriler sınırlı bir tabloya işaret ediyor."
                : string.Join("; ", pack.Signals.Take(2).Select(s => s.Evidence)) + ".";
            if (pack.Contradictions.Count > 0)
                basis += " " + pack.Contradictions[0];
            return basis;
        }

        // Kanıt özeti (fallback) — Evidence Pack'in derli toplu hâli.
        private static string BuildEvidenceFallback(IntelligencePack pack)
        {
            if (pack.Evidence.Count == 0) return "";
            return "Öne çıkan kanıtlar — " +
                   string.Join(", ", pack.Evidence.Take(4).Select(e => $"{e.Label}: {e.Value}")) + ".";
        }

        // "Radarın Öne Çıkardıkları" — context sinyallerinden türeyen dinamik bulgular.
        private static List<string> DeriveFindings(MatchIntelligenceContext ctx, string stronger)
        {
            var f = new List<string>();

            if (ctx.News.Volume24h >= 8)
                f.Add("Haber hacmi son 24 saatte belirgin şekilde arttı");
            else if (ctx.News.Volume24h > 0)
                f.Add("Haber akışı bu maç çevresinde canlı");

            var attack = (ctx.Stats.HomeGoalScoringRate + ctx.Stats.AwayGoalScoringRate) / 2;
            var defense = (ctx.Stats.HomeCleanSheetRate + ctx.Stats.AwayCleanSheetRate) / 2;
            if (attack > defense + 5)
                f.Add("Hücum sinyalleri savunma sinyallerinden güçlü");

            if (ctx.Form.HomeFormScore >= 60 && ctx.Form.AwayFormScore >= 60)
                f.Add("İki takımın form grafikleri aynı yönde yükseliyor");
            else
                f.Add($"{stronger} form göstergesinde önde");

            if (ctx.Social.Level == "Yüksek")
                f.Add("Taraftar ilgisi lig ortalamasının üzerinde");

            return f;
        }

        // ── Yardımcılar ────────────────────────────────────────────────────────
        private static string? Str(JsonElement root, string prop) =>
            root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

        private static IEnumerable<string> Arr(JsonElement root, string prop)
        {
            if (!root.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
                yield break;
            foreach (var el in arr.EnumerateArray())
                if (el.ValueKind == JsonValueKind.String)
                    yield return el.GetString() ?? "";
        }

        private static string? ExtractJson(string raw)
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            return (start >= 0 && end > start) ? raw[start..(end + 1)] : null;
        }

        private static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes, 0, 6);
        }
    }
}
