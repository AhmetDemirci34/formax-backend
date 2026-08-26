using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Application.AI.Context;

namespace Formax.Application.AI.Signals
{
    /// <summary>
    /// FORMAX GDP — AI Signal Factory. GDP'nin son katmanı: doğrulanmış/ilişkilendirilmiş verileri
    /// (UnifiedMatchAiContext alt-sinyalleri) FUTBOL ANLAMINA dönüştürür → standart zarflı AiSignal listesi.
    ///
    /// - Ham veri (JSON/haber/tweet/RSS/HTML) motora GİTMEZ; hepsi burada işlenir.
    /// - SIGNAL FUSION: aynı konu farklı kaynaklardan gelirse (API + Resmi Sosyal + Haber) TEK sinyal.
    /// - ÇELİŞKİ ÇÖZÜMÜ: kaynaklar çelişirse SourceTrust + Freshness + Official Source ile karar verilir.
    /// - Çıktı YALNIZ UnifiedMatchAiContext.Signals'a yazılır; başka katmana veri göndermez.
    /// - Veri yoksa HasData=false (fake YOK). Motor/LLM bu fazda değişmez (sinyaller hazır bulunur).
    /// </summary>
    public sealed class AiSignalFactory
    {
        // ── Fusion katkısı (bir kaynağın bir konuya katkısı) ──────────────────────
        private readonly struct Contributor
        {
            public readonly string Source;
            public readonly double Magnitude;   // 0..1 severity/strength
            public readonly int Trust;          // 0..100
            public readonly int Evidence;       // 0..100
            public readonly double Freshness;   // 0..1
            public readonly string? Timestamp;  // ISO-8601 UTC
            public readonly string Detail;

            public Contributor(string source, double magnitude, int trust, int evidence,
                double freshness, string? timestamp, string detail)
            {
                Source = source; Magnitude = Math.Clamp(magnitude, 0, 1); Trust = trust;
                Evidence = evidence; Freshness = Math.Clamp(freshness, 0, 1);
                Timestamp = timestamp; Detail = detail;
            }
        }

        public IReadOnlyList<AiSignal> Produce(UnifiedMatchAiContext ctx)
        {
            var signals = new List<AiSignal>();
            var related = new List<string> { ctx.HomeName, ctx.AwayName };

            // ── ApiFootball yapısal güç sinyalleri (Strength/TeamStats/Standings) ──
            signals.Add(BuildAttackStrength(ctx, related));
            signals.Add(BuildDefenceStrength(ctx, related));
            signals.Add(BuildFormStrength(ctx, related));
            signals.Add(BuildStandingsStrength(ctx, related));
            signals.Add(BuildHomeAdvantage(ctx, related));
            signals.Add(BuildTeamStrength(ctx, related));
            signals.Add(BuildPlayerAvailability(ctx, related));
            signals.Add(BuildHistoricalStrength(ctx, related));
            signals.Add(BuildCoachStability(ctx, related));
            signals.Add(BuildVenueImpact(ctx, related));
            signals.Add(BuildRefereeImpact(ctx, related));

            // ── FÜZYON sinyalleri (API + Haber + Resmi Sosyal aynı konuda) ────────
            signals.Add(BuildInjuryImpact(ctx, related));
            signals.Add(BuildTransferImpact(ctx, related));
            signals.Add(BuildCoachImpact(ctx, related));
            signals.Add(BuildOfficialAnnouncement(ctx, related));
            signals.Add(BuildBreakingNewsImpact(ctx, related));

            // ── Haber/Sosyal türev sinyalleri ────────────────────────────────────
            signals.Add(BuildNewsConfidence(ctx, related));
            signals.Add(BuildOfficialSocialImpact(ctx, related));
            signals.Add(BuildCompetitionImpact(ctx, related));
            signals.Add(BuildProviderPrediction(ctx, related));

            // ── Kapsam-dışı (bu fazda veri yok) — DÜRÜST HasData=false ────────────
            signals.Add(NoData("WeatherImpact", "ApiFootball", related, "Hava verisi context'te yok."));
            signals.Add(NoData("Motivation", "Derived", related, "Motivasyon türetimi için yeterli sinyal yok."));
            signals.Add(NoData("FixtureCongestion", "ApiFootball", related, "Fikstür yoğunluğu verisi yok."));
            signals.Add(NoData("TravelFatigue", "ApiFootball", related, "Seyahat/konum verisi yok."));

            // ── Kalite son-geçişi: her sinyal için ZORUNLU DataQuality türet (envelope tutarlılığı).
            return signals.Select(s => s with { DataQuality = ComputeDataQuality(s) }).ToList();
        }

        /// <summary>
        /// Context-düzeyi kalite/özet paketini (agregat + Conflict Management + reasoning hints)
        /// üretilen sinyallerden deterministik türetir. Builder Signals'tan sonra çağırır.
        /// </summary>
        public UnifiedContextQuality BuildQuality(IReadOnlyList<AiSignal> signals, List<string> related)
        {
            signals ??= new List<AiSignal>();
            var active = signals.Where(s => s.HasData).ToList();

            var conflictSummary = signals
                .GroupBy(s => s.ConflictStatus.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // Reasoning hints: en güçlü (impact büyüklüğü × güven) aktif sinyallerden kısa açıklama.
            var hints = active
                .OrderByDescending(s => Math.Abs(s.Impact) * s.Confidence)
                .ThenByDescending(s => s.Confidence)
                .Take(5)
                .Select(s => $"{s.Name}: {s.Reason}")
                .ToList();

            return new UnifiedContextQuality
            {
                TotalSignalCount = signals.Count,
                ActiveSignalCount = active.Count,
                OverallConfidence = active.Count == 0 ? 0 : (int)Math.Round(active.Average(s => s.Confidence)),
                OverallDataQuality = active.Count == 0 ? 0 : Math.Round(active.Average(s => s.DataQuality), 3),
                OverallFreshness = active.Count == 0 ? 0 : Math.Round(active.Average(s => s.Freshness), 3),
                OverallSourceTrust = active.Count == 0 ? 0 : active.Max(s => s.SourceTrust),
                OverallEvidenceScore = active.Count == 0 ? 0 : (int)Math.Round(active.Average(s => s.EvidenceScore)),
                ConflictSummary = conflictSummary,
                ReasoningHints = hints,
                RelatedEntities = related ?? new List<string>()
            };
        }

        /// <summary>DataQuality (0..1) = güven/kaynak/kanıt/tazelik harmanı. Veri yoksa 0.</summary>
        private static double ComputeDataQuality(AiSignal s)
        {
            if (!s.HasData) return 0.0;
            var dq = s.Confidence / 100.0 * 0.40
                   + s.SourceTrust / 100.0 * 0.30
                   + s.EvidenceScore / 100.0 * 0.20
                   + s.Freshness * 0.10;
            return Math.Round(Math.Clamp(dq, 0, 1), 3);
        }

        private static AiSignal BuildCompetitionImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var compNews = ctx.News.HasData ? ctx.News.CompetitionNews : 0;
            var hasStandings = ctx.Standings.HasData;
            if (compNews == 0 && !hasStandings)
                return NoData("CompetitionImpact", "Derived", related, "Lig/turnuva bağlamı verisi yok.");
            var value = Math.Clamp(compNews / 5.0 + (hasStandings ? 0.4 : 0), 0, 1);
            return new AiSignal
            {
                Name = "CompetitionImpact", Category = "Derived",
                Value = Math.Round(value, 3), Impact = 0.0,
                Confidence = hasStandings ? 75 : 55, EvidenceScore = hasStandings ? 80 : 60,
                SourceTrust = hasStandings ? 92 : ctx.News.SourceTrust, Freshness = 0.9,
                Reason = $"Lig bağlamı — sıralama {(hasStandings ? "var" : "yok")}, lig haberi {compNews}.",
                RelatedEntities = related, HasData = true
            };
        }

        // ════════════════════════════ Yapısal sinyaller ═══════════════════════════

        private static AiSignal BuildAttackStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            var s = ctx.Strength;
            var has = s.HomeAttackIndex > 0 || s.AwayAttackIndex > 0;
            if (!has) return NoData("AttackStrength", "ApiFootball", related, "Gol üretim verisi yok.");
            var value = (s.HomeAttackIndex + s.AwayAttackIndex) / 2.0;
            var impact = NormDiff(s.HomeAttackIndex, s.AwayAttackIndex, 2.0);
            return new AiSignal
            {
                Name = "AttackStrength", Category = "ApiFootball",
                Value = Math.Round(value, 3), Impact = impact,
                Confidence = ConfFromQuality(ctx), EvidenceScore = QualityScore(ctx), SourceTrust = 90,
                Freshness = 1.0, Reason = $"Ev {s.HomeAttackIndex:0.00} / Dep {s.AwayAttackIndex:0.00} maç-başı gol beklentisi.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildDefenceStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            var s = ctx.Strength;
            var has = s.HomeDefenceIndex > 0 || s.AwayDefenceIndex > 0;
            if (!has) return NoData("DefenceStrength", "ApiFootball", related, "Yenilen gol verisi yok.");
            // Daha az yenen daha güçlü → ev lehine impact = (away conceded - home conceded).
            var impact = NormDiff(s.AwayDefenceIndex, s.HomeDefenceIndex, 2.0);
            var value = 1.0 / (1.0 + (s.HomeDefenceIndex + s.AwayDefenceIndex) / 2.0);
            return new AiSignal
            {
                Name = "DefenceStrength", Category = "ApiFootball",
                Value = Math.Round(value, 3), Impact = impact,
                Confidence = ConfFromQuality(ctx), EvidenceScore = QualityScore(ctx), SourceTrust = 90,
                Freshness = 1.0, Reason = $"Ev {s.HomeDefenceIndex:0.00} / Dep {s.AwayDefenceIndex:0.00} maç-başı yenilen gol.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildFormStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            string homeForm = ctx.TeamStats.HasData ? ctx.TeamStats.Home.Form : ctx.Standings.Home.Form;
            string awayForm = ctx.TeamStats.HasData ? ctx.TeamStats.Away.Form : ctx.Standings.Away.Form;
            var hp = FormPoints(homeForm); var ap = FormPoints(awayForm);
            if (hp.count == 0 && ap.count == 0)
                return NoData("FormStrength", "ApiFootball", related, "Form dizisi yok.");
            var impact = NormDiff(hp.ratio, ap.ratio, 1.0);
            return new AiSignal
            {
                Name = "FormStrength", Category = "ApiFootball",
                Value = Math.Round((hp.ratio + ap.ratio) / 2.0, 3), Impact = impact,
                Confidence = 70, EvidenceScore = 70, SourceTrust = 90, Freshness = 1.0,
                Reason = $"Form — Ev '{homeForm}' / Dep '{awayForm}'.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildStandingsStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            var st = ctx.Standings;
            if (!st.HasData || st.Home.Position <= 0 || st.Away.Position <= 0)
                return NoData("StandingsStrength", "ApiFootball", related, "Puan durumu kapsamı yok.");
            // Daha küçük pozisyon (üst sıra) daha güçlü → ev lehine = (away - home).
            var impact = NormDiff(st.Away.Position, st.Home.Position, 10.0);
            return new AiSignal
            {
                Name = "StandingsStrength", Category = "ApiFootball",
                Value = Math.Round(1.0 - Math.Min(st.Home.Position, st.Away.Position) / 30.0, 3), Impact = impact,
                Confidence = 85, EvidenceScore = 85, SourceTrust = 92, Freshness = 1.0,
                Reason = $"Sıra — Ev {st.Home.Position}. / Dep {st.Away.Position}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildHomeAdvantage(UnifiedMatchAiContext ctx, List<string> related)
        {
            var ts = ctx.TeamStats;
            if (!ts.HasData || ts.Home.Played <= 0 || ts.Away.Played <= 0)
                return NoData("HomeAdvantage", "ApiFootball", related, "Ev/deplasman ayrımlı sezon verisi yok.");
            // Ev takımının EV gol farkı vs deplasman takımının DEPLASMAN gol farkı.
            var homeHomeGd = ts.Home.GoalsForAvgHome - ts.Home.GoalsAgainstAvgHome;
            var awayAwayGd = ts.Away.GoalsForAvgAway - ts.Away.GoalsAgainstAvgAway;
            var impact = NormDiff(homeHomeGd, awayAwayGd, 2.0);
            return new AiSignal
            {
                Name = "HomeAdvantage", Category = "ApiFootball",
                Value = Math.Round(Math.Clamp((homeHomeGd + 2) / 4.0, 0, 1), 3), Impact = impact,
                Confidence = 80, EvidenceScore = 80, SourceTrust = 90, Freshness = 1.0,
                Reason = $"Ev-sahibi ev GF-GA {homeHomeGd:0.00} vs deplasman dep GF-GA {awayAwayGd:0.00}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildTeamStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            var s = ctx.Strength;
            var has = (s.HomeAttackIndex + s.AwayAttackIndex + s.HomeDefenceIndex + s.AwayDefenceIndex) > 0;
            if (!has) return NoData("TeamStrength", "ApiFootball", related, "Güç verisi yok.");
            var homeNet = s.HomeAttackIndex - s.HomeDefenceIndex;
            var awayNet = s.AwayAttackIndex - s.AwayDefenceIndex;
            var impact = NormDiff(homeNet, awayNet, 2.0);
            return new AiSignal
            {
                Name = "TeamStrength", Category = "Derived",
                Value = Math.Round(Math.Clamp((homeNet + awayNet + 4) / 8.0, 0, 1), 3), Impact = impact,
                Confidence = ConfFromQuality(ctx), EvidenceScore = QualityScore(ctx), SourceTrust = 90,
                Freshness = 1.0, Reason = $"Net güç — Ev {homeNet:0.00} / Dep {awayNet:0.00} (hücum-savunma).",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildPlayerAvailability(UnifiedMatchAiContext ctx, List<string> related)
        {
            var a = ctx.Availability;
            if (!a.HasData) return NoData("PlayerAvailability", "ApiFootball", related, "Kadro/sakatlık verisi yok.");
            // Rakip daha çok eksikse ev lehine → impact = (away - home).
            var impact = NormDiff(a.AwayKeyAbsences, a.HomeKeyAbsences, 5.0);
            var total = a.HomeKeyAbsences + a.AwayKeyAbsences;
            return new AiSignal
            {
                Name = "PlayerAvailability", Category = "ApiFootball",
                Value = Math.Round(Math.Clamp(total / 10.0, 0, 1), 3), Impact = impact,
                Confidence = 85, EvidenceScore = 85, SourceTrust = 90, Freshness = 1.0,
                Reason = $"Kesin eksik — Ev {a.HomeKeyAbsences} / Dep {a.AwayKeyAbsences}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildHistoricalStrength(UnifiedMatchAiContext ctx, List<string> related)
        {
            var h = ctx.H2H;
            if (h.TotalMatches <= 0) return NoData("HistoricalStrength", "ApiFootball", related, "H2H geçmişi yok.");
            var impact = NormDiff(h.HomeWins, h.AwayWins, Math.Max(1, h.TotalMatches));
            return new AiSignal
            {
                Name = "HistoricalStrength", Category = "ApiFootball",
                Value = Math.Round((double)(h.HomeWins + h.AwayWins) / Math.Max(1, h.TotalMatches), 3), Impact = impact,
                Confidence = Math.Clamp(40 + h.TotalMatches * 5, 0, 90), EvidenceScore = 75, SourceTrust = 90,
                Freshness = 0.7, Reason = $"H2H {h.TotalMatches} maç — Ev {h.HomeWins} / Dep {h.AwayWins} / Berabere {h.Draws}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildCoachStability(UnifiedMatchAiContext ctx, List<string> related)
        {
            var tp = ctx.TeamProfile;
            if (!tp.Home.HasCoach && !tp.Away.HasCoach)
                return NoData("CoachStability", "ApiFootball", related, "Teknik direktör verisi yok.");
            var names = new List<string>();
            if (tp.Home.HasCoach) names.Add($"{ctx.HomeName}: {tp.Home.CoachName}");
            if (tp.Away.HasCoach) names.Add($"{ctx.AwayName}: {tp.Away.CoachName}");
            return new AiSignal
            {
                Name = "CoachStability", Category = "ApiFootball",
                Value = 1.0, Impact = 0.0,
                Confidence = 70, EvidenceScore = 70, SourceTrust = 90, Freshness = 1.0,
                Reason = "Kayıtlı teknik direktör — " + string.Join(" | ", names),
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildVenueImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var v = ctx.TeamProfile.Home; // ev sahibinin stadı
            if (!v.HasVenue) return NoData("VenueImpact", "ApiFootball", related, "Stat verisi yok.");
            var value = Math.Clamp(v.VenueCapacity / 80000.0, 0, 1);
            return new AiSignal
            {
                Name = "VenueImpact", Category = "ApiFootball",
                Value = Math.Round(value, 3), Impact = Math.Round(value * 0.3, 3), // büyük stat hafif ev avantajı
                Confidence = 65, EvidenceScore = 70, SourceTrust = 90, Freshness = 1.0,
                Reason = $"{v.VenueName} — kapasite {v.VenueCapacity}, zemin {v.VenueSurface}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildRefereeImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var r = ctx.Referee;
            if (!r.HasData) return NoData("RefereeImpact", "ApiFootball", related, "Hakem atanmadı.");
            return new AiSignal
            {
                Name = "RefereeImpact", Category = "ApiFootball",
                Value = 0.0, Impact = 0.0, // kart eğilimi verisi yok → nötr kimlik sinyali
                Confidence = 50, EvidenceScore = 60, SourceTrust = 90, Freshness = 1.0,
                Reason = $"Hakem atandı: {r.RefereeName} (kart eğilimi verisi yok, nötr).",
                RelatedEntities = related, HasData = true
            };
        }

        // ════════════════════════════ Füzyon sinyalleri ═══════════════════════════

        private AiSignal BuildInjuryImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var contributors = new List<Contributor>();
            if (ctx.Availability.HasData)
            {
                var total = ctx.Availability.HomeKeyAbsences + ctx.Availability.AwayKeyAbsences;
                if (total > 0)
                    contributors.Add(new Contributor("API-Football (kadro)", Math.Clamp(total / 6.0, 0, 1),
                        90, 90, 1.0, null, $"{total} kesin eksik"));
            }
            if (ctx.News.InjuryNews > 0)
                contributors.Add(new Contributor("Haber", Math.Clamp(ctx.News.InjuryNews / 5.0, 0, 1),
                    ctx.News.SourceTrust, ctx.News.EvidenceScore, FreshnessFrom(ctx.News.LatestPublishedUtc),
                    ctx.News.LatestPublishedUtc, $"{ctx.News.InjuryNews} sakatlık haberi"));
            if (ctx.Social.OfficialInjuryAnnouncement > 0)
                contributors.Add(new Contributor("Resmi Sosyal", Math.Clamp(ctx.Social.OfficialInjuryAnnouncement / 3.0, 0, 1),
                    Math.Max(95, ctx.Social.SourceTrust), ctx.Social.EvidenceScore, FreshnessFrom(ctx.Social.LatestPublishedUtc),
                    ctx.Social.LatestPublishedUtc, "resmi sakatlık açıklaması"));

            // YÖN BURADA ÜRETİLMEZ (Impact = 0 → yönsüz/severity sinyali).
            //
            // ÖNCEKİ DAVRANIŞ: burada da NormDiff(AwayKeyAbsences, HomeKeyAbsences, 5.0)
            // hesaplanıyordu — PlayerAvailability sinyaliyle BİREBİR AYNI ifade, aynı girdi.
            // İkisi de yönlü olduğu için (SignalUnderstandingEngine: IsDirectional = |Impact|>0)
            // ConflictResolver.BuildField'daki NetHomeEdge ağırlıklı ortalamasına aynı eksik
            // farkı İKİ KEZ oy veriyordu → GoalModelFactory'deki ±%15 tilt çift sayılıyordu.
            //
            // Kadro eksiğinin YÖNÜ tek yerden gelir: PlayerAvailability (ApiFootball, ham veri).
            // InjuryImpact füzyon sinyali olarak kalır ve severity/güven/tazelik/çelişki
            // üzerinden risk, belirsizlik ve açıklanabilirliği beslemeye DEVAM eder.
            return Fuse("InjuryImpact", contributors, related, 0.0,
                "Sakatlık etkisi", "sakatlık verisi yok");
        }

        private AiSignal BuildTransferImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var contributors = new List<Contributor>();
            if (ctx.News.TransferNews > 0)
                contributors.Add(new Contributor("Haber", Math.Clamp(ctx.News.TransferNews / 5.0, 0, 1),
                    ctx.News.SourceTrust, ctx.News.EvidenceScore, FreshnessFrom(ctx.News.LatestPublishedUtc),
                    ctx.News.LatestPublishedUtc, $"{ctx.News.TransferNews} transfer haberi"));
            if (ctx.Social.OfficialTransferAnnouncement > 0)
                contributors.Add(new Contributor("Resmi Sosyal", Math.Clamp(ctx.Social.OfficialTransferAnnouncement / 3.0, 0, 1),
                    Math.Max(95, ctx.Social.SourceTrust), ctx.Social.EvidenceScore, FreshnessFrom(ctx.Social.LatestPublishedUtc),
                    ctx.Social.LatestPublishedUtc, "resmi transfer açıklaması"));
            var tp = ctx.TeamProfile;
            if (tp.HasData && (tp.Home.RecentTransfersIn + tp.Home.RecentTransfersOut + tp.Away.RecentTransfersIn + tp.Away.RecentTransfersOut) > 0)
            {
                var churn = tp.Home.RecentTransfersIn + tp.Home.RecentTransfersOut + tp.Away.RecentTransfersIn + tp.Away.RecentTransfersOut;
                contributors.Add(new Contributor("API-Football (transfers)", Math.Clamp(churn / 40.0, 0, 1),
                    88, 80, 0.8, null, $"{churn} son transfer hareketi"));
            }
            return Fuse("TransferImpact", contributors, related, 0.0, "Transfer etkisi", "transfer verisi yok");
        }

        private AiSignal BuildCoachImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var contributors = new List<Contributor>();
            if (ctx.News.CoachNews > 0)
                contributors.Add(new Contributor("Haber", Math.Clamp(ctx.News.CoachNews / 4.0, 0, 1),
                    ctx.News.SourceTrust, ctx.News.EvidenceScore, FreshnessFrom(ctx.News.LatestPublishedUtc),
                    ctx.News.LatestPublishedUtc, $"{ctx.News.CoachNews} teknik direktör haberi"));
            if (ctx.Social.OfficialCoachStatement > 0)
                contributors.Add(new Contributor("Resmi Sosyal", Math.Clamp(ctx.Social.OfficialCoachStatement / 3.0, 0, 1),
                    Math.Max(95, ctx.Social.SourceTrust), ctx.Social.EvidenceScore, FreshnessFrom(ctx.Social.LatestPublishedUtc),
                    ctx.Social.LatestPublishedUtc, "resmi teknik direktör açıklaması"));
            return Fuse("CoachImpact", contributors, related, 0.0, "Teknik direktör etkisi", "teknik direktör haberi yok");
        }

        private AiSignal BuildOfficialAnnouncement(UnifiedMatchAiContext ctx, List<string> related)
        {
            var contributors = new List<Contributor>();
            if (ctx.News.OfficialAnnouncements > 0 || ctx.News.HasOfficialSource)
                contributors.Add(new Contributor("Haber (resmi)", Math.Clamp(ctx.News.OfficialAnnouncements / 3.0, 0, 1),
                    ctx.News.HasOfficialSource ? 95 : ctx.News.SourceTrust, ctx.News.EvidenceScore,
                    FreshnessFrom(ctx.News.LatestPublishedUtc), ctx.News.LatestPublishedUtc,
                    $"{ctx.News.OfficialAnnouncements} resmi açıklama"));
            if (ctx.Social.HasData && ctx.Social.OfficialAnnouncement > 0)
                contributors.Add(new Contributor("Resmi Sosyal", Math.Clamp(ctx.Social.OfficialAnnouncement / 5.0, 0, 1),
                    Math.Max(95, ctx.Social.SourceTrust), ctx.Social.EvidenceScore,
                    FreshnessFrom(ctx.Social.LatestPublishedUtc), ctx.Social.LatestPublishedUtc,
                    $"{ctx.Social.OfficialAnnouncement} resmi paylaşım"));
            return Fuse("OfficialAnnouncement", contributors, related, 0.0, "Resmi açıklama yoğunluğu", "resmi açıklama yok");
        }

        private AiSignal BuildBreakingNewsImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            var breakingNews = ctx.News.HasBreakingNews;
            var breakingSocial = ctx.Social.HasData && ctx.Social.BreakingOfficialNews;
            if (!breakingNews && !breakingSocial)
                return NoData("BreakingNewsImpact", "Fused", related, "Son 6 saatte breaking yok.");
            var contributors = new List<Contributor>();
            if (breakingNews)
                contributors.Add(new Contributor("Haber", 0.8, ctx.News.SourceTrust, ctx.News.EvidenceScore,
                    FreshnessFrom(ctx.News.LatestPublishedUtc), ctx.News.LatestPublishedUtc, "breaking haber"));
            if (breakingSocial)
                contributors.Add(new Contributor("Resmi Sosyal", 1.0, Math.Max(95, ctx.Social.SourceTrust),
                    ctx.Social.EvidenceScore, FreshnessFrom(ctx.Social.LatestPublishedUtc),
                    ctx.Social.LatestPublishedUtc, "breaking resmi paylaşım"));
            return Fuse("BreakingNewsImpact", contributors, related, 0.0, "Breaking gelişme", "breaking yok");
        }

        // ════════════════════════════ Türev sinyaller ═════════════════════════════

        private static AiSignal BuildNewsConfidence(UnifiedMatchAiContext ctx, List<string> related)
        {
            if (!ctx.News.HasData) return NoData("NewsConfidence", "News", related, "Haber kanıtı yok.");
            return new AiSignal
            {
                Name = "NewsConfidence", Category = "News",
                Value = Math.Round(ctx.News.NewsConfidence / 100.0, 3), Impact = 0.0,
                Confidence = ctx.News.NewsConfidence, EvidenceScore = ctx.News.EvidenceScore,
                SourceTrust = ctx.News.SourceTrust, Freshness = FreshnessFrom(ctx.News.LatestPublishedUtc),
                Timestamp = ctx.News.LatestPublishedUtc,
                Reason = $"{ctx.News.TotalEvidence} kanıt, güven {ctx.News.NewsConfidence}, kaynak {ctx.News.SourceTrust}.",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildOfficialSocialImpact(UnifiedMatchAiContext ctx, List<string> related)
        {
            if (!ctx.Social.HasData) return NoData("OfficialSocialImpact", "Social", related, "Resmi sosyal paylaşım yok.");
            return new AiSignal
            {
                Name = "OfficialSocialImpact", Category = "Social",
                Value = Math.Round(Math.Clamp(ctx.Social.OfficialAnnouncement / 10.0, 0, 1), 3), Impact = 0.0,
                Confidence = ctx.Social.Confidence, EvidenceScore = ctx.Social.EvidenceScore,
                SourceTrust = ctx.Social.SourceTrust, Freshness = FreshnessFrom(ctx.Social.LatestPublishedUtc),
                Timestamp = ctx.Social.LatestPublishedUtc,
                Reason = $"{ctx.Social.OfficialAnnouncement} resmi paylaşım (Lineup {ctx.Social.OfficialLineupAnnouncement}, açıklama {ctx.Social.OfficialClubStatement}).",
                RelatedEntities = related, HasData = true
            };
        }

        private static AiSignal BuildProviderPrediction(UnifiedMatchAiContext ctx, List<string> related)
        {
            var p = ctx.Prediction;
            if (!p.HasData) return NoData("ProviderPrediction", "ApiFootball", related, "Sağlayıcı öngörüsü yok.");
            var impact = NormDiff(p.PercentHome, p.PercentAway, 100.0);
            return new AiSignal
            {
                Name = "ProviderPrediction", Category = "ApiFootball",
                Value = Math.Round(p.PredictionConfidence / 100.0, 3), Impact = impact,
                Confidence = p.PredictionConfidence, EvidenceScore = p.PredictionConfidence, SourceTrust = 88,
                Freshness = 0.9, Reason = $"Sağlayıcı: {p.ProviderPrediction} ({p.PercentHome}/{p.PercentDraw}/{p.PercentAway}).",
                RelatedEntities = related, HasData = true
            };
        }

        // ════════════════════════════ Füzyon çekirdeği ════════════════════════════

        private static AiSignal Fuse(string name, List<Contributor> all, List<string> related,
            double impactDirection, string label, string emptyReason)
        {
            if (all.Count == 0)
                return NoData(name, "Fused", related, emptyReason);

            // BASKILAMA (Suppressed): güçlü kaynak varken bayat (freshness<0.15) VEYA çok düşük güven
            // (<45) kaynağı fusion'a alma. En az bir güçlü kaynak (trust>=80) varsa uygula.
            var strongExists = all.Any(c => c.Trust >= 80);
            var contributors = strongExists
                ? all.Where(c => c.Freshness >= 0.15 && c.Trust >= 45).ToList()
                : all;
            var suppressed = strongExists && contributors.Count < all.Count;
            if (contributors.Count == 0) contributors = all; // hepsi elenirse geri al

            // Ağırlık = trust * freshness (güvenilir + taze kaynak baskın).
            double wsum = 0, vsum = 0;
            foreach (var c in contributors) { var w = c.Trust * (0.5 + 0.5 * c.Freshness); wsum += w; vsum += w * c.Magnitude; }
            var value = wsum > 0 ? vsum / wsum : contributors.Average(c => c.Magnitude);

            var sourceTrust = contributors.Max(c => c.Trust);
            var evidence = contributors.Max(c => c.Evidence);
            var freshness = contributors.Max(c => c.Freshness);
            var timestamp = contributors.Where(c => c.Timestamp != null)
                .OrderByDescending(c => c.Timestamp).Select(c => c.Timestamp).FirstOrDefault();

            // ÇELİŞKİ: büyüklükler ayrışıyorsa (max-min>0.5). Resmi/otorite (trust>=90) varsa Resolved.
            var spread = contributors.Max(c => c.Magnitude) - contributors.Min(c => c.Magnitude);
            var conflict = contributors.Count > 1 && spread > 0.5;
            var hasAuthority = contributors.Any(c => c.Trust >= 90);

            SignalConflictStatus status;
            if (conflict) status = hasAuthority ? SignalConflictStatus.Resolved : SignalConflictStatus.Unresolved;
            else if (contributors.Count > 1) status = SignalConflictStatus.Merged;
            else if (suppressed) status = SignalConflictStatus.Suppressed;
            else status = SignalConflictStatus.None;

            var agreementBonus = status == SignalConflictStatus.Merged ? 12 : 0;
            var conflictPenalty = status == SignalConflictStatus.Unresolved ? 25
                                : status == SignalConflictStatus.Resolved ? 12 : 0;

            var confidence = Math.Clamp(
                45 + contributors.Count * 8 + sourceTrust / 10 + agreementBonus - conflictPenalty, 0, 99);

            var srcList = string.Join(" + ", contributors.Select(c => $"{c.Source} ({c.Detail})"));
            var reason = status switch
            {
                SignalConflictStatus.Unresolved => $"{label}: {contributors.Count} kaynak ÇELİŞİYOR, otorite yok → çözülemedi. {srcList}.",
                SignalConflictStatus.Resolved   => $"{label}: {contributors.Count} kaynak çelişti → resmi/güvenilir kaynak ile ÇÖZÜLDÜ. {srcList}.",
                SignalConflictStatus.Merged     => $"{label}: {contributors.Count} kaynak uyumlu → BİRLEŞTİRİLDİ. {srcList}.",
                SignalConflictStatus.Suppressed => $"{label}: bayat/zayıf kaynak BASKILANDI; {contributors.Count} kaynak esas alındı. {srcList}.",
                _                               => $"{label}: {contributors.Count} kaynak. {srcList}."
            };

            return new AiSignal
            {
                Name = name, Category = "Fused",
                Value = Math.Round(value, 3), Impact = Math.Round(impactDirection, 3),
                Confidence = confidence, EvidenceScore = evidence, SourceTrust = sourceTrust,
                Freshness = Math.Round(freshness, 3), Timestamp = timestamp,
                ConflictStatus = status,
                Reason = reason, RelatedEntities = related, HasData = true
            };
        }

        // ════════════════════════════ Yardımcılar ═════════════════════════════════

        private static AiSignal NoData(string name, string category, List<string> related, string reason) => new()
        {
            Name = name, Category = category, Value = 0, Impact = 0, Confidence = 0,
            EvidenceScore = 0, SourceTrust = 0, Freshness = 0, Reason = reason,
            RelatedEntities = related, HasData = false
        };

        private static double NormDiff(double a, double b, double scale)
            => scale <= 0 ? 0 : Math.Clamp((a - b) / scale, -1.0, 1.0);

        private static int QualityScore(UnifiedMatchAiContext ctx) => (int)Math.Round(ctx.DataQuality * 100);
        private static int ConfFromQuality(UnifiedMatchAiContext ctx) => Math.Clamp((int)Math.Round(ctx.DataQuality * 90) + 5, 0, 95);

        /// <summary>ISO-8601 zamandan tazelik (0..1): ≤24s=1.0, 14 günde 0'a düşer.</summary>
        private static double FreshnessFrom(string? iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return 0.5;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t))
                return 0.5;
            var ageH = (DateTime.UtcNow - t).TotalHours;
            if (ageH <= 24) return 1.0;
            if (ageH >= 24 * 14) return 0.0;
            return Math.Clamp(1.0 - (ageH - 24) / (24 * 14 - 24), 0, 1);
        }

        // Form dizisi (WWDLW) → oran + adet.
        private static (double ratio, int count) FormPoints(string? form)
        {
            if (string.IsNullOrWhiteSpace(form)) return (0, 0);
            int pts = 0, n = 0;
            foreach (var ch in form)
            {
                var c = char.ToUpperInvariant(ch);
                if (c == 'W') { pts += 3; n++; }
                else if (c == 'D') { pts += 1; n++; }
                else if (c == 'L') { n++; }
            }
            return n == 0 ? (0, 0) : ((double)pts / (n * 3), n);
        }
    }
}
