using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>
    /// SONUÇ KONTROL TAKVİMİ — bot maç sırasında canlı veri aramaz.
    ///
    /// İlk kontroller beklenen bitişe yakın: başlama +105 … +165 dk arası 3 dakikada bir, sonra +180 dk.
    /// Resmî kaynak henüz yayımlamadıysa +180'den sonra: 30 dk, 1 sa, 3 sa, 6 sa, 12 sa, 24 sa.
    /// Sonrasında günde bir düşük öncelikli kontrol.
    /// </summary>
    public static class OfficialResultSchedule
    {
        /// <summary>Başlama saatine göre dakika cinsinden plan (deneme sırası).</summary>
        public static readonly IReadOnlyList<int> MinutesFromKickoff = new[]
        {
            // 17.09.2026 (2): final yayımı ile yazım arası ≤ 5 dk hedefi — normal süre + uzatma/penaltı penceresinde (+105…+165)
            // 3 dakikada bir; iş 1 dk'da bir döner → en kötü durum ≈ 3 + 1 dk. Kaynak listesi tur başına bir kez okunur (aynı
            // anda başlayan maçlar tek istek paylaşır); pencereden sonra seyrekleşir (geri çekilme).
            105, 108, 111, 114, 117, 120, 123, 126, 129, 132, 135, 138, 141, 144, 147, 150, 153, 156, 159, 162, 165,
            180, 180 + 30, 180 + 60, 180 + 180, 180 + 360, 180 + 720, 180 + 1440
        };

        /// <summary>Beklenen final penceresindeki en uzun kontrol aralığı (dk).</summary>
        public const int FinalWindowCadenceMinutes = 3;

        public static readonly TimeSpan DailyAfterPlan = TimeSpan.FromHours(24);

        /// <summary>İlk kontrol zamanı.</summary>
        public static DateTime FirstCheck(DateTime kickoffUtc) => kickoffUtc.AddMinutes(MinutesFromKickoff[0]);

        /// <summary>
        /// <paramref name="attemptsDone"/> kontrol yapıldıktan sonraki kontrol zamanı. Plan geçmişte kaldıysa (ör. restart
        /// sonrası) kaçırılan adımlar üst üste çalıştırılmaz: bir sonraki kontrol en erken şimdiden 1 dk sonradır.
        /// </summary>
        public static DateTime NextCheck(DateTime kickoffUtc, int attemptsDone, DateTime nowUtc)
        {
            DateTime planned;
            if (attemptsDone < MinutesFromKickoff.Count)
            {
                planned = kickoffUtc.AddMinutes(MinutesFromKickoff[attemptsDone]);
                // Aynı turda geçmişte kalmış adımları atla: şimdiden sonraki ilk plan adımına geç.
                var i = attemptsDone;
                while (planned <= nowUtc && i + 1 < MinutesFromKickoff.Count && kickoffUtc.AddMinutes(MinutesFromKickoff[i + 1]) <= nowUtc)
                {
                    i++;
                    planned = kickoffUtc.AddMinutes(MinutesFromKickoff[i]);
                }
            }
            else
            {
                var last = kickoffUtc.AddMinutes(MinutesFromKickoff[^1]);
                var extra = attemptsDone - MinutesFromKickoff.Count + 1;
                planned = last + TimeSpan.FromTicks(DailyAfterPlan.Ticks * extra);
            }
            return planned <= nowUtc ? nowUtc.AddMinutes(1) : planned;
        }
    }

    /// <summary>
    /// İSTATİSTİK KONTROL TAKVİMİ — kesin sonuçtan sonra +10 dk, +30 dk, +60 dk, +3 sa, +6 sa, +12 sa, +24 sa;
    /// sonra günlük düşük öncelik; <see cref="MaxDailyChecks"/> gün sonra takvim kapanır (sonsuza kadar arama yok).
    /// </summary>
    public static class OfficialStatisticsSchedule
    {
        public static readonly IReadOnlyList<int> MinutesFromFinal = new[] { 10, 30, 60, 180, 360, 720, 1440 };
        public const int MaxDailyChecks = 7;

        public static DateTime FirstCheck(DateTime finalUtc) => finalUtc.AddMinutes(MinutesFromFinal[0]);

        /// <summary>Sonraki kontrol; takvim bittiyse null.</summary>
        public static DateTime? NextCheck(DateTime finalUtc, int attemptsDone, DateTime nowUtc)
        {
            DateTime planned;
            if (attemptsDone < MinutesFromFinal.Count) planned = finalUtc.AddMinutes(MinutesFromFinal[attemptsDone]);
            else
            {
                var extra = attemptsDone - MinutesFromFinal.Count + 1;
                if (extra > MaxDailyChecks) return null;
                planned = finalUtc.AddMinutes(MinutesFromFinal[^1]).AddDays(extra);
            }
            return planned <= nowUtc ? nowUtc.AddMinutes(1) : planned;
        }
    }

    /// <summary>Kanonik sonuç biçimleri.</summary>
    public static class OfficialResultDetails
    {
        public const string FullTime = "FT";
        public const string AfterExtraTime = "AET";
        public const string Penalties = "PEN";
    }

    /// <summary>Kaynak durumunun kanonik karşılığı.</summary>
    public sealed record CanonicalResultDecision(
        string Kind,            // Final | Postponed | Cancelled | Abandoned | NotFinal
        string? MatchStatus,    // Finished | Postponed | Cancelled | Abandoned
        string? ResultDetail,   // FT | AET | PEN
        int? HomeScore,
        int? AwayScore,
        int? HalfTimeHome,
        int? HalfTimeAway,
        int? PenaltyHome,
        int? PenaltyAway,
        string Reason);

    /// <summary>
    /// DURUM EŞLEMESİ — kaynağın kaydı → FORMAX kanonik durumu. Skor yoksa "bitti" kabul edilmez (0-0 uydurulmaz);
    /// seri penaltı skoru yalnız iki taraf da yayımlandıysa yazılır.
    /// </summary>
    public static class OfficialResultStatusPolicy
    {
        public static CanonicalResultDecision Decide(OfficialMatchRecord record)
        {
            var extra = record.Extra;
            string? detail = extra?.GetValueOrDefault("resultDetail");
            int? penH = ParseInt(extra?.GetValueOrDefault("penaltyHome"));
            int? penA = ParseInt(extra?.GetValueOrDefault("penaltyAway"));

            switch (record.Status)
            {
                case OfficialMatchStatuses.Finished:
                case OfficialMatchStatuses.FinishedAfterExtraTime:
                case OfficialMatchStatuses.FinishedAfterPenalties:
                    if (record.HomeScore is not int hs || record.AwayScore is not int aws)
                        return new("NotFinal", null, null, null, null, null, null, null, null, "FinishedWithoutScore");
                    var d = record.Status switch
                    {
                        OfficialMatchStatuses.FinishedAfterPenalties => OfficialResultDetails.Penalties,
                        OfficialMatchStatuses.FinishedAfterExtraTime => OfficialResultDetails.AfterExtraTime,
                        _ => detail is OfficialResultDetails.Penalties or OfficialResultDetails.AfterExtraTime ? detail : OfficialResultDetails.FullTime
                    };
                    if (d == OfficialResultDetails.Penalties && (penH == null || penA == null || penH == penA))
                    {
                        // Seri penaltı sonucu yayımlanmadı ya da eşit: biçim yazılır, penaltı skoru uydurulmaz.
                        penH = null; penA = null;
                    }
                    if (d != OfficialResultDetails.Penalties) { penH = null; penA = null; }
                    return new("Final", "Finished", d, hs, aws, record.HalfTimeHome, record.HalfTimeAway, penH, penA, "Final");
                case OfficialMatchStatuses.Postponed:
                    return new("Postponed", "Postponed", null, null, null, null, null, null, null, "Postponed");
                case OfficialMatchStatuses.Cancelled:
                    return new("Cancelled", "Cancelled", null, null, null, null, null, null, null, "Cancelled");
                case OfficialMatchStatuses.Abandoned:
                    return new("Abandoned", "Abandoned", null, null, null, null, null, null, null, "Abandoned");
                default:
                    return new("NotFinal", null, null, null, null, null, null, null, null, record.Status);
            }
        }

        private static int? ParseInt(string? s) => int.TryParse(s, out var v) ? v : null;
    }

    /// <summary>Bir kaynağın aynı maç için gözlemi (öncelik + kanonik karar).</summary>
    public sealed record SourceResultObservation(string SourceKey, OfficialSourceTier Tier, CanonicalResultDecision Decision);

    /// <summary>Uzlaşma kararı.</summary>
    public sealed record ConsensusDecision(string Outcome, CanonicalResultDecision? Accepted, string Reason, IReadOnlyList<string> Sources);

    /// <summary>
    /// KAYNAK ÖNCELİĞİ VE UZLAŞMA —
    ///  1) Resmî lig/federasyon maç merkezi ya da turnuva sitesi: tek yüksek güvenli kaynak final sonuç için yeterli.
    ///     Aynı öncelikte iki kaynak farklı skor verirse Conflict (kanonik yazılmaz).
    ///  2) Lig/federasyon kaynağı yoksa: ev sahibi ve deplasman kulübünün resmî sayfaları AYNI skoru ve yönü vermeden
    ///     sonuç yazılmaz; farklıysa Conflict.
    ///  3) Yayıncı yalnız başına kanonik sonuç yazdırmaz.
    /// </summary>
    public static class ResultConsensusPolicy
    {
        public const string Accept = "Accept";
        public const string Conflict = "Conflict";
        public const string Wait = "Wait";

        public static ConsensusDecision Decide(IReadOnlyList<SourceResultObservation> observations)
        {
            var finals = observations.Where(o => o.Decision.Kind == "Final").ToList();
            var high = finals.Where(o => o.Tier is OfficialSourceTier.Federation or OfficialSourceTier.LeagueMatchCentre).ToList();
            if (high.Count > 0)
            {
                var top = high.Min(o => (int)o.Tier);
                var best = high.Where(o => (int)o.Tier == top).ToList();
                var distinct = best.Select(o => (o.Decision.HomeScore, o.Decision.AwayScore)).Distinct().Count();
                if (distinct > 1)
                    return new(Conflict, null, "HighPrioritySourcesDisagree", best.Select(o => o.SourceKey).ToList());
                var other = high.Where(o => (int)o.Tier != top && (o.Decision.HomeScore, o.Decision.AwayScore) != (best[0].Decision.HomeScore, best[0].Decision.AwayScore)).ToList();
                if (other.Count > 0)
                    return new(Conflict, null, "LeagueAndFederationDisagree", high.Select(o => o.SourceKey).ToList());
                return new(Accept, best[0].Decision, "HighPrioritySource", best.Select(o => o.SourceKey).ToList());
            }

            var home = finals.FirstOrDefault(o => o.Tier == OfficialSourceTier.HomeClub);
            var away = finals.FirstOrDefault(o => o.Tier == OfficialSourceTier.AwayClub);
            if (home != null && away != null)
            {
                if (home.Decision.HomeScore == away.Decision.HomeScore && home.Decision.AwayScore == away.Decision.AwayScore)
                    return new(Accept, home.Decision, "BothClubsAgree", new[] { home.SourceKey, away.SourceKey });
                return new(Conflict, null, "ClubSourcesDisagree", new[] { home.SourceKey, away.SourceKey });
            }
            if (home != null || away != null)
                return new(Wait, null, "SingleClubSourceNeedsSecond", finals.Select(o => o.SourceKey).ToList());

            // Final olmayan kesin durumlar (erteleme/iptal/yarıda kalma) yalnız lig/federasyon kaynağından kabul edilir.
            var status = observations.Where(o => o.Decision.Kind is "Postponed" or "Cancelled" or "Abandoned"
                                                 && o.Tier is OfficialSourceTier.Federation or OfficialSourceTier.LeagueMatchCentre)
                .OrderBy(o => (int)o.Tier).FirstOrDefault();
            if (status != null) return new(Accept, status.Decision, "HighPriorityStatus", new[] { status.SourceKey });

            return new(Wait, null, finals.Count > 0 ? "OnlyBroadcaster" : "NotFinalYet", observations.Select(o => o.SourceKey).ToList());
        }
    }

    /// <summary>
    /// İstatistik alanlarının tamlık sınıfı.
    ///
    /// "Full" = çekirdek 12 alanın (topa sahip olma, toplam/isabetli/isabetsiz/bloke şut, korner, faul, ofsayt, sarı kart,
    /// kurtarış, toplam/başarılı pas) HER İKİ tarafta da yayımlanmış olması. Kırmızı kart ve pas yüzdesi çekirdeğe girmez:
    /// ölçülen resmî kaynaklar (Premier League SDP, LALIGA maç sayfası) pas yüzdesini hiç yayımlamıyor, kırmızı kartı yalnız
    /// kart gösterildiğinde yazıyor. Bu iki alan yayımlanmadıysa null kalır (0 uydurulmaz) — ama maç "Partial" sayılmaz.
    /// </summary>
    public static class StatisticsCompleteness
    {
        public const string None = "None";
        public const string Partial = "Partial";
        public const string Full = "Full";

        public static int MeasuredCount(OfficialTeamStatistics s) => All(s).Count(v => v.HasValue);

        public static int CoreMeasuredCount(OfficialTeamStatistics s) => Core(s).Count(v => v.HasValue);

        private static int?[] All(OfficialTeamStatistics s) => new int?[]
        {
            s.BallPossession, s.TotalShots, s.ShotsOnTarget, s.ShotsOffTarget, s.BlockedShots, s.Corners, s.Offsides, s.Fouls,
            s.YellowCards, s.RedCards, s.GoalkeeperSaves, s.TotalPasses, s.AccuratePasses, s.PassAccuracy
        };

        private static int?[] Core(OfficialTeamStatistics s) => new int?[]
        {
            s.BallPossession, s.TotalShots, s.ShotsOnTarget, s.ShotsOffTarget, s.BlockedShots, s.Corners, s.Offsides, s.Fouls,
            s.YellowCards, s.GoalkeeperSaves, s.TotalPasses, s.AccuratePasses
        };

        public const int FieldCount = 14;
        public const int CoreFieldCount = 12;

        public static string Of(OfficialMatchStatistics? stats)
        {
            if (stats == null) return None;
            if (MeasuredCount(stats.Home) == 0 && MeasuredCount(stats.Away) == 0) return None;
            return CoreMeasuredCount(stats.Home) == CoreFieldCount && CoreMeasuredCount(stats.Away) == CoreFieldCount ? Full : Partial;
        }
    }

    /// <summary>Tek bot denemesinin kalıcı sınıflaması (MatchResultChecks.LastErrorClass / LastValidationStatus).</summary>
    public sealed record ResultAttemptClassification(string? ErrorClass, string ValidationStatus);

    /// <summary>
    /// DENEME SINIFLAMASI — botun serbest metinli sonucundan (LastOutcome) teşhis edilebilir iki alan üretir:
    ///  • hata sınıfı: null (hata yok) | NotFinalYet | NoOfficialSource | CircuitOpen | SourceReadFailed | IdentityNotMatched |
    ///    Conflict | VerificationPending
    ///  • doğrulama durumu: Verified | NotFinal | NotChecked | Rejected:{neden} | Conflict | Pending
    /// </summary>
    public static class ResultAttemptClassifier
    {
        private static readonly HashSet<string> IdentityReasons = new(StringComparer.Ordinal)
        {
            "NoCandidate", "WrongDate", "WrongTeams", "OrientationMismatch", "IdentityRejected", "Ambiguous", "NoKickoff"
        };

        /// <param name="outcome">Tur sonucu: ResultApplied | ResultUnchanged | StatusApplied | NotFinal | NotFinalYet | Conflict |
        /// ResultConflict | VerificationPending | ResultConfirmationFetchFailed | NoObservation | ResultSourceUnavailable.</param>
        /// <param name="lastFailure">Gözlem yoksa son kaynak hatası: "{kaynak}:{okuma sonucu|kimlik nedeni}[:{ayrıntı}]".</param>
        public static ResultAttemptClassification Classify(string outcome, string? lastFailure)
        {
            switch (outcome)
            {
                case "ResultApplied":
                case "ResultUnchanged":
                case "StatusApplied":
                    return new(null, "Verified");
                case "NotFinal":
                case "NotFinalYet":
                    return new("NotFinalYet", "NotFinal");
                case "Conflict":
                case "ResultConflict":
                    return new("Conflict", "Conflict");
                case "VerificationPending":
                    return new("VerificationPending", "Pending");
                case "ResultConfirmationFetchFailed":
                    return new("SourceReadFailed", "Pending");
                case "ResultSourceUnavailable":
                    return new("NoOfficialSource", "NotChecked");
                case "NoObservation":
                    var reason = (lastFailure ?? string.Empty).Split(':') is { Length: >= 2 } parts ? parts[1] : string.Empty;
                    if (reason == "CircuitOpen") return new("CircuitOpen", "NotChecked");
                    if (IdentityReasons.Contains(reason)) return new("IdentityNotMatched", "Rejected:" + reason);
                    return new("SourceReadFailed", "NotChecked");
                default:
                    return new("Unknown", "NotChecked");
            }
        }
    }
}
