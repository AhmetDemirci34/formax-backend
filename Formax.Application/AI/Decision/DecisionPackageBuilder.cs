using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Formax.Application.AI.Context;
using Formax.Application.AI.Decision.Modules;

namespace Formax.Application.AI.Decision
{
    /// <summary>
    /// MODÜL 11 — Decision Package Builder (orkestratör).
    ///
    /// FORMAX beyninin boru hattı: Unified AI Context → (Understand → Weigh → Resolve → Field) →
    /// Goal Model → Match DNA → Confidence → Probabilities → Scenarios → Risks → Live →
    /// Explainability → AiDecisionPackage. Deterministik: aynı context → aynı paket (hash ile
    /// doğrulanabilir). Modüller saf/stateless olduğu için builder onları içeride kompoze eder
    /// (DI'a dokunmaz; motorun parametresiz ctor'u korunur — OCP/SOLID).
    /// </summary>
    public sealed class DecisionPackageBuilder
    {
        private readonly SignalUnderstandingEngine _understanding = new();
        private readonly DynamicWeightEngine _weighting = new();
        private readonly ConflictResolver _conflict = new();
        private readonly ContextIntelligenceEngine _context = new();
        private readonly MatchDnaEngine _dna = new();
        private readonly ConfidenceEngine _confidence = new();
        private readonly ProbabilityEngine _probability = new();
        private readonly ScenarioEngine _scenario = new();
        private readonly RiskEngine _risk = new();
        private readonly LiveProjectionEngine _live = new();
        private readonly ExplainabilityEngine _explain = new();
        // v3 Football Intelligence
        private readonly MatchImportanceEngine _importance = new();
        private readonly MatchArchetypeEngine _archetype = new();
        // v3 Advanced Intelligence
        private readonly PsychologicalEngine _psychology = new();
        private readonly InteractionEngine _interaction = new();
        private readonly ContradictionEngine _contradiction = new();
        private readonly SurpriseEngine _surprise = new();
        private readonly TacticalEngine _tactical = new();
        // v3 Live Intelligence
        private readonly LiveMomentumEngine _momentum = new();
        // v1.1 Quality/Consistency
        private readonly ReasoningConsistencyValidator _validator = new();
        private readonly DecisionQualityEngine _quality = new();
        // Football Intelligence v4 — oyuncu/kadro/çapraz-sentez/haber-etki (context okur; hash'e dokunmaz)
        private readonly FootballIntelligenceEngine _football = new();
        // AI Brain vNext — TEK anlatı beyni: her blok bir kez okunur → MatchReading (Editorial+Story projeksiyon)
        private readonly MatchReadingEngine _reading = new();

        public AiDecisionPackage Build(UnifiedMatchAiContext ctx)
        {
            if (ctx == null) return new AiDecisionPackage();

            // 1-4) Sinyal boru hattı → agregat alan.
            var understood = _understanding.Understand(ctx);
            var weighted   = _weighting.Weigh(understood);
            var resolved   = _conflict.Resolve(weighted);
            var field      = _conflict.BuildField(resolved);

            // v2 — Maçın bağlamı (Standings/Motivation/Derby/Pressure). Yalnız gerçek ctx bloklarından;
            // veri yoksa tüm alt bloklar HasData=false → model/DNA/risk v1 ile birebir aynı kalır.
            var context = _context.Analyze(ctx);

            // 5) Beklenen-gol modeli (DNA + olasılıkların paylaştığı kaynak; derbi bağlamı yansır).
            var model = GoalModelFactory.Build(ctx, field, context);

            // 6) Maç DNA (bağlam: derbi → fiziksel/kaos/denge; baskı → kaos/sürpriz).
            var dna = _dna.Profile(ctx, field, model, context);
            // v3 — DNA arketipleri (Açık/Tempolu/Fiziksel/Kaotik/Dengeli...).
            dna.Archetypes = _archetype.Classify(dna);

            // v3 — Maçın önemi (tek skor; aktif bağlam bileşenlerinden).
            var importance = _importance.Compute(context);

            // v3 — Çelişki (yapı vs bağlam/sentiment). Güveni DÜŞÜRÜR → olasılıklardan ÖNCE uygulanır.
            var contradiction = _contradiction.Analyze(ctx, field);

            // 7) Genel güven (çelişki cezası uygulanır).
            var confidence = _confidence.Assess(ctx, field);
            if (contradiction.HasContradiction && contradiction.ConfidencePenalty > 0)
            {
                var s = Math.Clamp(confidence.Score - contradiction.ConfidencePenalty, 0, 99);
                confidence = new DecisionConfidence
                {
                    Score = s,
                    Level = s >= 68 ? "YÜKSEK" : s >= 50 ? "ORTA" : "DÜŞÜK",
                    Basis = confidence.Basis + $" Çelişki cezası -{contradiction.ConfidencePenalty}."
                };
            }

            // 8) Olasılıklar (13+ market).
            var probabilities = _probability.Compute(ctx, model, field, confidence.Score);

            // v1.1 — İÇ TUTARLILIK DENETİMİ: olasılık↔DNA↔güven çapraz kontrol. Tutarsız marketlerin
            // GÜVENİ düşürülür (olasılık DEĞİŞMEZ → determinizm/hash korunur).
            var consistency = _validator.Validate(probabilities, dna, model, confidence, contradiction);
            if (consistency.FlaggedMarkets.Count > 0)
                probabilities = DowngradeFlagged(probabilities, consistency.FlaggedMarkets);

            // 9) Senaryolar (düzeltilmiş olasılıklardan; top-3 + alternatif).
            var (scenarios, alternatives) = _scenario.Generate(probabilities, dna, 3);

            // 10) Riskler (bağlam: derbi/baskı riskleri eklenir).
            var risks = _risk.Assess(ctx, dna, field, context);

            // 11) Canlı projeksiyon.
            var live = _live.Project(ctx, model, dna, confidence.Score);

            // v3 Advanced Intelligence — taktik/psikoloji/etkileşim/sürpriz.
            var tactical = _tactical.Analyze(ctx, model);
            var psychology = _psychology.Analyze(ctx, context);
            var interactions = _interaction.Analyze(ctx, dna, context);
            var surprise = _surprise.Analyze(dna, field, contradiction);
            var liveMomentum = _momentum.Analyze(ctx);

            // Top signals (şeffaflık) — dinamik ağırlığa göre normalize.
            var topSignals = BuildTopSignals(resolved);
            // v3 — Gizli sinyaller: aktif ama düşük-ağırlıklı (top dışı) sinyaller.
            var hiddenSignals = BuildHiddenSignals(resolved);

            // 12) Açıklanabilirlik (bağlam + önem + arketip + upset + çelişki + güven dahil).
            var explain = _explain.Explain(ctx, topSignals, dna, scenarios, risks, field, context,
                importance, surprise, dna.Archetypes, contradiction, confidence);

            // v1.1 — Decision Package v2 anatomisi (additive; olasılıkları değiştirmez).
            var personality = BuildPersonality(dna);
            var (primary, alternative, surpriseScenario) = BuildScenarioTrio(scenarios, alternatives, probabilities, surprise, field, dna);
            var criticalFactors = BuildCriticalFactors(dna, importance, context, topSignals);
            var decisionDrivers = topSignals.Take(5).Select(t =>
                $"{t.Name} — {(t.Direction > 0.05 ? "ev" : t.Direction < -0.05 ? "deplasman" : "yönsüz")} (ağırlık {t.Weight}, güven {t.Confidence}).").ToList();
            var unknownFactors = BuildUnknownFactors(ctx);
            var qualityScore = _quality.Score(confidence, explain, ctx, consistency);

            // Football Intelligence v4 — BİRLEŞİK yapısal futbol zekâsı (oyuncu/kadro/çapraz-sentez/haber-etkisi).
            // YALNIZ context okur; olasılık/gol modeline dokunmaz → determinizm/hash korunur.
            var football = _football.Build(ctx);

            // AI Brain vNext — TEK ANLATI BEYNİ: her ham blok BİR kez okunur; oyuncu/haber/sentez Football'dan
            // ödünç alınır (tekrar okuma YOK). Editorial ve Story artık bu okumanın PROJEKSİYONU (tek gerçek).
            // Olasılık/gol modeline DOKUNMAZ (determinizm/hash korunur); canlı okuma yalnız in-play LiveState'ten.
            var reading = _reading.Read(ctx, football, dna, context, importance, interactions, contradiction, surprise, field, model);
            var editorial = MatchReadingEngine.ToEditorial(reading);
            var story = MatchReadingEngine.ToStory(reading);

            // Meta + determinizm imzası.
            var q = ctx.Quality ?? new UnifiedContextQuality();
            var meta = new DecisionMeta
            {
                ContextVersion = ctx.Version,
                TotalSignalCount = q.TotalSignalCount,
                ActiveSignalCount = q.ActiveSignalCount,
                OverallDataQuality = q.ActiveSignalCount > 0 ? q.OverallDataQuality : ctx.DataQuality,
                ConflictSummary = q.ConflictSummary ?? new Dictionary<string, int>(),
                ExpectedGoalsHome = Math.Round(model.ExpHome, 3),
                ExpectedGoalsAway = Math.Round(model.ExpAway, 3),
                NetHomeEdge = field.NetHomeEdge,
                DeterminismHash = ComputeHash(ctx, model, probabilities)
            };

            return new AiDecisionPackage
            {
                MatchId = ctx.MatchId,
                HomeName = ctx.HomeName,
                AwayName = ctx.AwayName,
                Dna = dna,
                Probabilities = probabilities,
                Scenarios = scenarios,
                AlternativeScenarios = alternatives,
                Live = live,
                Confidence = confidence,
                Risks = risks,
                Explainability = explain,
                Context = context,
                Importance = importance,
                Tactical = tactical,
                Psychology = psychology,
                Interactions = interactions,
                Contradiction = contradiction,
                Surprise = surprise,
                LiveMomentum = liveMomentum,
                TopSignals = topSignals,
                HiddenSignals = hiddenSignals,
                // v1.1 anatomi
                PrimaryScenario = primary,
                AlternativeScenario = alternative,
                SurpriseScenario = surpriseScenario,
                CriticalFactors = criticalFactors,
                DecisionDrivers = decisionDrivers,
                UnknownFactors = unknownFactors,
                Personality = personality,
                Consistency = consistency,
                DecisionQualityScore = qualityScore,
                Editorial = editorial,
                FootballIntelligence = football,
                Story = story,
                Reading = reading,
                Meta = meta
            };
        }

        // ══════════════════════════════ v1.1 yardımcılar ══════════════════════════════

        /// <summary>Tutarsız işaretlenen marketlerin GÜVEN etiketini bir kademe düşürür (olasılık DEĞİŞMEZ).</summary>
        private static IReadOnlyList<AiProbability> DowngradeFlagged(
            IReadOnlyList<AiProbability> probs, IReadOnlyList<string> flagged)
        {
            string Down(string level) => level == "YÜKSEK" ? "ORTA" : level == "ORTA" ? "DÜŞÜK" : "DÜŞÜK";
            var set = new HashSet<string>(flagged);
            var list = new List<AiProbability>(probs.Count);
            foreach (var p in probs)
            {
                if (set.Contains(p.Market))
                    list.Add(new AiProbability
                    {
                        Market = p.Market, Probability = p.Probability, Family = p.Family,
                        Confidence = Down(p.Confidence),
                        Reason = p.Reason + " (iç tutarlılık: güven düşürüldü)."
                    });
                else list.Add(p);
            }
            return list;
        }

        /// <summary>Maç karakteri (personality) — DNA arketipleri + oyun yönelimi.</summary>
        private static MatchPersonality BuildPersonality(MatchDna dna)
        {
            var traits = dna.Archetypes ?? new List<string>();
            var primary = traits.Count > 0 ? traits[0]
                : dna.All().OrderByDescending(d => d.Score).First().Label;
            var playStyle = (dna.GoalPotential.Score >= 60 && dna.Openness.Score >= 55) ? "Hücum ağırlıklı"
                          : (dna.GoalPotential.Score < 42 && dna.Balance.Score >= 60) ? "Savunma ağırlıklı"
                          : "Dengeli";
            var summary = $"{primary} karakter, {playStyle.ToLowerInvariant()} eğilim " +
                          $"(tempo {dna.Tempo.Score}, açıklık {dna.Openness.Score}, denge {dna.Balance.Score}).";
            return new MatchPersonality
            {
                Primary = primary,
                Traits = traits,
                PlayStyle = playStyle,
                Summary = summary
            };
        }

        /// <summary>Ana / Alternatif / Sürpriz senaryo üçlüsü.</summary>
        private static (AiScenario primary, AiScenario alternative, AiScenario surprise) BuildScenarioTrio(
            IReadOnlyList<AiScenario> scenarios, IReadOnlyList<AiScenario> alternatives,
            IReadOnlyList<AiProbability> probabilities, SurpriseAlert surprise, SignalField field, MatchDna dna)
        {
            var primary = scenarios.Count > 0 ? scenarios[0] : new AiScenario();
            var alternative = scenarios.Count > 1 ? scenarios[1]
                            : alternatives.Count > 0 ? alternatives[0] : new AiScenario();

            // Sürpriz senaryo: favori KARŞITI 1X2 sonucu + sürpriz gerekçesi.
            var favorHome = field.NetHomeEdge >= 0;
            var underdogMarket = favorHome ? "Deplasman Kazanır" : "Ev Sahibi Kazanır";
            var up = probabilities.FirstOrDefault(p => p.Market == underdogMarket);
            var surpriseScenario = up == null ? new AiScenario() : new AiScenario
            {
                Title = up.Market,
                Probability = up.Probability,
                Confidence = up.Confidence,
                Family = up.Family,
                Reason = surprise != null && surprise.HasAlert
                    ? $"Upset uyarısı aktif (potansiyel {surprise.Potential}/100): {up.Reason}"
                    : $"Düşük olasılıklı ama sürpriz potansiyeli {dna.SurprisePotential.Score}/100: {up.Reason}",
                Risk = $"Sürpriz potansiyeli {dna.SurprisePotential.Score}, kaos {dna.ChaosRisk.Score}."
            };
            return (primary, alternative, surpriseScenario);
        }

        /// <summary>Kararı belirleyen kritik faktörler (önem + DNA + en güçlü sürücü + bağlam).</summary>
        private static IReadOnlyList<string> BuildCriticalFactors(
            MatchDna dna, MatchImportance importance, ContextIntelligence context, IReadOnlyList<TopSignal> topSignals)
        {
            var list = new List<string>();
            if (importance != null && importance.Score >= 55)
                list.Add($"Yüksek maç önemi ({importance.Score}/100, {importance.Level}).");
            var topDim = dna.All().OrderByDescending(d => d.Score).First();
            list.Add($"Baskın karakter: {topDim.Name} — {topDim.Label} ({topDim.Score}/100).");
            var driver = topSignals.FirstOrDefault();
            if (driver != null)
                list.Add($"En güçlü sürücü: {driver.Name} (ağırlık {driver.Weight}).");
            if (context?.Derby?.HasData == true)
                list.Add($"Derbi bağlamı (şiddet {(int)(context.Derby.Intensity * 100)}).");
            if (dna.ChaosRisk.Score >= 60)
                list.Add($"Yüksek kaos riski ({dna.ChaosRisk.Score}/100).");
            return list;
        }

        /// <summary>Verisi olmayan (HasData=false) karar-kritik bloklar — dürüstlük (bilinmeyen faktörler).</summary>
        private static IReadOnlyList<string> BuildUnknownFactors(UnifiedMatchAiContext ctx)
        {
            var list = new List<string>();
            if (!ctx.Availability.HasData) list.Add("Kadro/sakatlık uygunluğu verisi yok.");
            if (!ctx.Standings.HasData && !ctx.StandingsContext.HasData) list.Add("Lig sıralaması verisi yok.");
            if (!ctx.TeamStats.HasData) list.Add("Takım sezon istatistiği verisi yok.");
            if (!ctx.News.HasData) list.Add("Haber/kanıt sinyali yok.");
            if (!ctx.Competition.HasData) list.Add("Müsabaka bağlamı (tür/aşama) verisi yok.");
            if (!ctx.Referee.HasData) list.Add("Hakem verisi yok (kart-eğilim zaten kapsam dışı).");
            return list;
        }

        /// <summary>
        /// Gizli sinyaller: aktif (HasData) ama düşük ağırlıklı — top sinyallerin dışında kalan,
        /// yine de dikkate değer sinyaller. Baskılanmış (çelişki) sinyaller de dahil (şeffaflık).
        /// </summary>
        private static IReadOnlyList<string> BuildHiddenSignals(IReadOnlyList<WeightedSignal> resolved)
        {
            if (resolved == null || resolved.Count == 0) return new List<string>();
            var maxW = resolved.Max(w => w.Weight);
            if (maxW <= 0) maxW = 1;

            return resolved
                .Where(w => w.Signal.HasData)
                .OrderByDescending(w => w.Weight)
                .Skip(8) // top-8 dışındakiler
                .Where(w => w.Weight / maxW < 0.5) // düşük göreli ağırlık
                .Take(5)
                .Select(w => $"{w.Signal.Name}{(w.Suppressed ? " (baskılanmış)" : "")}: {w.Signal.Source.Reason}")
                .ToList();
        }

        /// <summary>Ağırlıklı sinyalleri max-ağırlığa göre 0-100 normalize edip en güçlü ~8'ini döndürür.</summary>
        private static IReadOnlyList<TopSignal> BuildTopSignals(IReadOnlyList<WeightedSignal> resolved)
        {
            if (resolved == null || resolved.Count == 0) return new List<TopSignal>();
            var maxW = resolved.Max(w => w.Weight);
            if (maxW <= 0) maxW = 1;

            return resolved
                .OrderByDescending(w => w.Weight)
                .Take(8)
                .Select(w => new TopSignal
                {
                    Name = w.Signal.Name,
                    Category = w.Signal.Category,
                    Weight = (int)Math.Round(w.Weight / maxW * 100),
                    Direction = Math.Round(w.Signal.Impact, 3),
                    Confidence = w.Signal.Source.Confidence,
                    Reason = w.Signal.Source.Reason
                })
                .ToList();
        }

        /// <summary>
        /// Determinizm imzası: modelin ve tüm olasılıkların kanonik string'inden SHA-256 (ilk 16 hex).
        /// Aynı context aynı hash'i üretir → "Aynı Context = Aynı Sonuç" doğrulanabilir.
        /// </summary>
        private static string ComputeHash(UnifiedMatchAiContext ctx, PoissonGoalModel model,
            IReadOnlyList<AiProbability> probabilities)
        {
            var sb = new StringBuilder();
            sb.Append(ctx.MatchId).Append('|')
              .Append(model.ExpHome.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append('|')
              .Append(model.ExpAway.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            foreach (var p in probabilities)
                sb.Append(p.Market).Append('=').Append(p.Probability).Append(';');

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
        }
    }
}
