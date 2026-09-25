using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Outcomes
{
    /// <summary>
    /// YAYIN DURUMLARI — kullanıcıya gösterilen KARARLI durum. Ham pencere sonucu (<see cref="RawGateStatuses"/>) ile ayrıdır.
    ///
    /// NEDEN (25.09.2026): organizasyon × market kapısı her model koşusunda sıfırdan veriliyordu. Test penceresi bir hafta
    /// kaydırıldığında (model değişmeden) Bundesliga ve Serie A hücreleri kendi başına açılıp kapanıyordu: bu hücreler eşiğin
    /// gürültü bandında oturuyor. Tek bir tarihin ölçümü kullanıcı kartını değiştirmemeli.
    /// </summary>
    public static class PublishedStates
    {
        public const string Closed = "Closed";
        public const string PendingOpen = "PendingOpen";
        public const string Open = "Open";
        public const string PendingClose = "PendingClose";

        /// <summary>Kullanıcıya yüzde gösterilen durumlar. PendingOpen GÖSTERİLMEZ; PendingClose tek FAIL toleransıdır.</summary>
        public static bool IsVisible(string? state) => state is Open or PendingClose;
    }

    /// <summary>Bir değerlendirme penceresinin matematiksel sonucu.</summary>
    public static class RawGateStatuses
    {
        public const string Pass = "PASS";
        public const string Fail = "FAIL";
        /// <summary>Beklemesiz kapatan kusur: veri bütünlüğü, sızıntı, kimlik, anlamlı kötülük, ciddi örneklem düşüşü, kalibrasyon güvenliği.</summary>
        public const string HardFail = "HARD_FAIL";
    }

    /// <summary>
    /// HARD-FAIL GEREKÇELERİ — hiçbiri yeni eşik değildir; hepsi mevcut <see cref="EligibilityPolicy"/> /
    /// <see cref="MarketEligibilityPolicy"/> sınırlarının "Disabled" tarafıdır ya da yapısal bütünlük kontrolüdür.
    /// </summary>
    public static class HardFailCodes
    {
        public const string DataIntegrity = "HARD_DATA_INTEGRITY";
        public const string Leakage = "HARD_LEAKAGE";
        public const string HomeAwayIdentity = "HARD_HOME_AWAY_IDENTITY";
        /// <summary>Test maçı &lt; <see cref="EligibilityPolicy.DisabledBelowMatches"/> (mevcut Disabled sınırı).</summary>
        public const string SampleSeverelyBelow = "HARD_SAMPLE_SEVERELY_BELOW_MINIMUM";
        /// <summary>Maç başına log loss farkının %95 aralığının ALT ucu &gt; 0: model tabandan istatistiksel olarak anlamlı kötü.</summary>
        public const string SignificantlyWorse = "HARD_SIGNIFICANTLY_WORSE_THAN_BASELINE";
        /// <summary>ECE &gt; <see cref="EligibilityPolicy.MaxCalibrationErrorLimited"/> (mevcut CalibrationFailed sınırı).</summary>
        public const string CalibrationSafety = "HARD_CALIBRATION_SAFETY";
        public const string ProbabilitySum = "HARD_PROBABILITY_SUM";
        public const string ModelIdentity = "HARD_MODEL_CONFIG_IDENTITY";

        /// <summary>Açılışı da engelleyen yapısal (veri/kimlik) kusurlar.</summary>
        public static readonly IReadOnlyList<string> Structural = new[] { DataIntegrity, Leakage, HomeAwayIdentity, ProbabilitySum, ModelIdentity };
    }

    /// <summary>Değerlendirme turunun organizasyon düzeyi bütünlük bulguları (backtest girdisinden ölçülür).</summary>
    public sealed class EvaluationIntegrity
    {
        public bool Leakage { get; set; }
        public bool HomeAwayViolation { get; set; }
        public bool ProbabilitySumViolation { get; set; }
        public bool DataIntegrityViolation { get; set; }
        public bool ModelIdentityViolation { get; set; }
        public List<string> Details { get; set; } = new();

        public static EvaluationIntegrity Clean => new();
    }

    /// <summary>Bir organizasyon × market × kesim tarihi değerlendirmesi (kalıcı kaydın bellek karşılığı).</summary>
    public sealed class CellEvaluation
    {
        public int OrganizationId { get; set; }
        public string MarketFamily { get; set; } = string.Empty;
        public DateTime EvaluationCutoffUtc { get; set; }
        public string ModelVersion { get; set; } = OutcomeModelVersion.Current;
        public string ModelRunId { get; set; } = string.Empty;
        public string ConfigHash { get; set; } = string.Empty;
        public string PolicyVersion { get; set; } = EligibilityPublicationPolicy.Version;
        public string GatePolicyVersion { get; set; } = MarketEligibilityPolicy.Version;
        public int SampleCount { get; set; }
        public double LogLoss { get; set; }
        public double Brier { get; set; }
        public double Ece { get; set; }
        public double? CalibrationSlope { get; set; }
        public double? CalibrationIntercept { get; set; }
        public double BaselineLogLoss { get; set; }
        public double DifferenceFromBaseline { get; set; }
        public double ConfidenceIntervalLow { get; set; }
        public double ConfidenceIntervalHigh { get; set; }
        public double Bias { get; set; }
        public double Coverage { get; set; }
        /// <summary>Mevcut kapının kendi kararı (Eligible | Limited | ...), değiştirilmeden.</summary>
        public string GateStatus { get; set; } = string.Empty;
        public string RawGateStatus { get; set; } = RawGateStatuses.Fail;
        public List<string> RawGateReasons { get; set; } = new();
        public DateTime EvaluatedAtUtc { get; set; }

        public bool IsPass => RawGateStatus == RawGateStatuses.Pass;
        public bool IsHardFail => RawGateStatus == RawGateStatuses.HardFail;
        public (int, string) Cell => (OrganizationId, MarketFamily);

        /// <summary>Aynı 5'li pencereye girebilir mi? Model, yapılandırma, kapı ve yayın politikası AYNI olmalı.</summary>
        public string Lineage => string.Join("|", ModelVersion, ConfigHash, GatePolicyVersion, PolicyVersion);
    }

    /// <summary>Açılma kuralının madde madde sonucu (admin görünürlüğü ve rapor için).</summary>
    public sealed class OpeningCheck
    {
        public bool LatestPass { get; set; }
        public int PassInLast5 { get; set; }
        public int WindowsInLast5 { get; set; }
        public int PassInLast3 { get; set; }
        public bool NoSignificantlyWorseInLast3 { get; set; }
        public bool SampleAndCoverageOk { get; set; }
        public bool NoStructuralHardFail { get; set; }
        public bool SameLineage { get; set; }
        public bool NotSingleWindow { get; set; }

        public bool AllMet => LatestPass && PassInLast5 >= EligibilityPublicationPolicy.MinPassInWindow
                              && PassInLast3 >= EligibilityPublicationPolicy.MinPassInRecent && NoSignificantlyWorseInLast3
                              && SampleAndCoverageOk && NoStructuralHardFail && SameLineage && NotSingleWindow;

        public string Summary => $"{PassInLast5}/{WindowsInLast5} PASS (son 5), {PassInLast3}/3 PASS (son 3), latest={(LatestPass ? "PASS" : "FAIL")}";
    }

    public sealed record StateTransition(int OrganizationId, string MarketFamily, string Before, string After, string Reason, OpeningCheck? Opening)
    {
        public bool Changed => Before != After;
        public bool VisibilityChanged => PublishedStates.IsVisible(Before) != PublishedStates.IsVisible(After);
    }

    /// <summary>
    /// YAYIN POLİTİKASI (eligibility-publication-1) — histerezisli durum makinesi. KALİTE EŞİKLERİNİ GEVŞETMEZ: PASS kararı
    /// değişmeden mevcut <see cref="MarketEligibilityPolicy.Decide"/>'dır. Bu sınıf yalnız tek bir tarihin gürültüsünün yayın
    /// durumunu değiştirmesini engeller.
    ///
    ///  AÇILMA  Closed →(ilk PASS)→ PendingOpen →(kararlılık)→ Open. Kararlılık: latest PASS; son 5 pencerede ≥ 4 PASS; son 3'te ≥ 2
    ///          PASS; son 3'te anlamlı kötülük yok; her PASS penceresinde örneklem ≥ 300; yapısal hard-fail yok; aynı model/config/
    ///          politika soyu; PASS'lar ≥ 4 ayrı kesim tarihine yayılmış. PendingOpen'da FAIL → Closed (seri sıfırlanır).
    ///  KAPANMA Open →(FAIL)→ PendingClose →(PASS)→ Open; PendingClose →(FAIL)→ Closed (iki ardışık FAIL).
    ///  HARD    Hangi durumda olursa olsun HARD_FAIL → Closed, BEKLEMEDEN.
    /// </summary>
    public static class EligibilityPublicationPolicy
    {
        public const string Version = "eligibility-publication-1";
        public const int WindowCount = 5;
        public const int MinPassInWindow = 4;
        public const int RecentCount = 3;
        public const int MinPassInRecent = 2;

        /// <summary>
        /// Ham kapı sınıflaması: mevcut kapı kararı (Status) + yapısal bütünlük. Yeni sayısal eşik YOK — hard-fail sınırları
        /// mevcut Disabled/CalibrationFailed/DataQualityFailed sınırlarıdır; tek ek, güven aralığının ALT ucunun sıfırın üstünde
        /// olmasıdır (bu, "tabandan kötü" kararının istatistiksel olarak kesinleşmiş hâli).
        /// </summary>
        public static (string Raw, List<string> Reasons) Classify(MarketFamilyMetrics m, EvaluationIntegrity integrity, bool inheritedFromMatchResult = false)
        {
            var hard = new List<string>();
            if (integrity.DataIntegrityViolation || m.Status == MarketEligibilityStatuses.DataQualityFailed) hard.Add(HardFailCodes.DataIntegrity);
            if (integrity.Leakage) hard.Add(HardFailCodes.Leakage);
            if (integrity.HomeAwayViolation) hard.Add(HardFailCodes.HomeAwayIdentity);
            if (integrity.ProbabilitySumViolation) hard.Add(HardFailCodes.ProbabilitySum);
            if (integrity.ModelIdentityViolation) hard.Add(HardFailCodes.ModelIdentity);
            if (m.Status == MarketEligibilityStatuses.InsufficientSample) hard.Add(HardFailCodes.SampleSeverelyBelow);
            if (m.Status == MarketEligibilityStatuses.CalibrationFailed) hard.Add(HardFailCodes.CalibrationSafety);
            if (m.Matches > 0 && m.LogLossDiffCiLow > 0) hard.Add(HardFailCodes.SignificantlyWorse);
            if (m.Matches > 0 && !(double.IsFinite(m.LogLoss) && double.IsFinite(m.BaselineLogLoss) && double.IsFinite(m.CalibrationError)))
                hard.Add(HardFailCodes.DataIntegrity);
            hard = hard.Distinct().ToList();
            if (hard.Count > 0)
                return (RawGateStatuses.HardFail, hard.Concat(m.ReasonCodes).Distinct().ToList());
            if (m.Status == MarketEligibilityStatuses.Eligible)
                return (RawGateStatuses.Pass, new List<string>());
            var reasons = m.ReasonCodes.Count > 0 ? m.ReasonCodes.ToList() : new List<string> { m.Status };
            if (m.Status == MarketEligibilityStatuses.WorseThanBaseline && !reasons.Contains("WORSE_THAN_LEAGUE_AVERAGE")) reasons.Add("WORSE_THAN_LEAGUE_AVERAGE");
            return (RawGateStatuses.Fail, reasons);
        }

        /// <summary>Kapı metriğinden kalıcı değerlendirme satırı. Çifte şans 1X2'nin sayısal metriklerini miras alır.</summary>
        public static CellEvaluation ToEvaluation(MarketFamilyMetrics m, MarketFamilyMetrics? matchResult, EvaluationIntegrity integrity,
            DateTime cutoffUtc, string modelRunId, string configHash, DateTime evaluatedAtUtc, string modelVersion)
        {
            var src = m.Family == MarketFamilies.DoubleChance && matchResult != null ? matchResult : m;
            var gate = m.Family == MarketFamilies.DoubleChance && matchResult != null
                ? new MarketFamilyMetrics
                {
                    LeagueId = m.LeagueId, Family = m.Family, Matches = src.Matches, Status = m.Status, ReasonCodes = m.ReasonCodes,
                    LogLoss = src.LogLoss, BaselineLogLoss = src.BaselineLogLoss, CalibrationError = src.CalibrationError,
                    LogLossDiffCiLow = src.LogLossDiffCiLow, LogLossDiffCiHigh = src.LogLossDiffCiHigh
                }
                : m;
            var (raw, reasons) = Classify(gate, integrity);
            return new CellEvaluation
            {
                OrganizationId = m.LeagueId ?? 0, MarketFamily = m.Family, EvaluationCutoffUtc = cutoffUtc, ModelVersion = modelVersion,
                ModelRunId = modelRunId, ConfigHash = configHash, SampleCount = src.Matches, LogLoss = src.LogLoss, Brier = src.Brier,
                Ece = src.CalibrationError, CalibrationSlope = src.CalibrationSlope, CalibrationIntercept = src.CalibrationIntercept,
                BaselineLogLoss = src.BaselineLogLoss, DifferenceFromBaseline = src.LogLossDiff,
                ConfidenceIntervalLow = src.LogLossDiffCiLow, ConfidenceIntervalHigh = src.LogLossDiffCiHigh,
                Bias = src.MaxBias, Coverage = src.DataCoverage, GateStatus = m.Status,
                RawGateStatus = raw, RawGateReasons = reasons, EvaluatedAtUtc = evaluatedAtUtc
            };
        }

        /// <summary>
        /// Açılma kuralı — <paramref name="history"/> AYNI hücrenin, AYNI soyun (model/config/kapı/politika) değerlendirmeleridir,
        /// kesim tarihine göre ARTAN sırada ve en sonuncusu dâhil.
        /// </summary>
        public static OpeningCheck CheckOpening(IReadOnlyList<CellEvaluation> history)
        {
            var check = new OpeningCheck();
            if (history.Count == 0) return check;
            var latest = history[^1];
            var lineage = history.Where(h => h.Lineage == latest.Lineage).ToList();
            var last5 = lineage.TakeLast(WindowCount).ToList();
            var last3 = lineage.TakeLast(RecentCount).ToList();
            check.LatestPass = latest.IsPass;
            check.WindowsInLast5 = last5.Count;
            check.PassInLast5 = last5.Count(h => h.IsPass);
            check.PassInLast3 = last3.Count(h => h.IsPass);
            check.NoSignificantlyWorseInLast3 = last3.All(h => !h.RawGateReasons.Contains(HardFailCodes.SignificantlyWorse) && !(h.SampleCount > 0 && h.ConfidenceIntervalLow > 0));
            check.SampleAndCoverageOk = last5.Where(h => h.IsPass).All(h => h.SampleCount >= EligibilityPolicy.EnabledMinMatches && h.Coverage > 0);
            check.NoStructuralHardFail = last5.All(h => !h.RawGateReasons.Any(r => HardFailCodes.Structural.Contains(r)));
            check.SameLineage = last5.All(h => h.Lineage == latest.Lineage) && last5.Select(h => h.EvaluationCutoffUtc).Distinct().Count() == last5.Count;
            check.NotSingleWindow = last5.Where(h => h.IsPass).Select(h => h.EvaluationCutoffUtc).Distinct().Count() >= MinPassInWindow;
            return check;
        }

        /// <summary>
        /// Tek hücre, tek pencere geçişi. <paramref name="priorHistory"/> bu pencereden ÖNCEKİ değerlendirmelerdir (artan sıra).
        /// </summary>
        public static StateTransition Decide(string currentState, IReadOnlyList<CellEvaluation> priorHistory, CellEvaluation latest)
        {
            var before = string.IsNullOrEmpty(currentState) ? PublishedStates.Closed : currentState;
            if (latest.IsHardFail)
                return new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Closed,
                    "HARD_FAIL_IMMEDIATE_CLOSE:" + string.Join("+", latest.RawGateReasons.Where(r => r.StartsWith("HARD_"))), null);

            var history = priorHistory.Where(h => h.EvaluationCutoffUtc < latest.EvaluationCutoffUtc).Append(latest).ToList();
            var opening = CheckOpening(history);
            switch (before)
            {
                case PublishedStates.Open:
                    return latest.IsPass
                        ? new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Open, "OPEN_CONFIRMED_PASS", opening)
                        : new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.PendingClose, "FIRST_FAIL_PENDING_CLOSE", opening);
                case PublishedStates.PendingClose:
                    return latest.IsPass
                        ? new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Open, "RECOVERED_PASS_AFTER_PENDING_CLOSE", opening)
                        : new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Closed, "SECOND_CONSECUTIVE_FAIL_CLOSED", opening);
                case PublishedStates.PendingOpen:
                    if (!latest.IsPass)
                        return new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Closed, "PENDING_OPEN_RESET_ON_FAIL", opening);
                    return opening.AllMet
                        ? new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Open, "STABILITY_CONFIRMED_OPEN", opening)
                        : new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.PendingOpen, "PENDING_OPEN_PROGRESS:" + opening.Summary, opening);
                default:
                    return latest.IsPass
                        ? new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.PendingOpen, "FIRST_PASS_PENDING_OPEN:" + opening.Summary, opening)
                        : new(latest.OrganizationId, latest.MarketFamily, before, PublishedStates.Closed, "CLOSED_FAIL", opening);
            }
        }

        /// <summary>
        /// Bir kesim tarihindeki bütün hücreleri uygular. <paramref name="states"/> ve <paramref name="history"/> YERİNDE
        /// güncellenir (bellek içi simülasyon; kalıcılaştırma çağıranın işidir). Değerlendirilmeyen hücrenin durumu değişmez.
        /// </summary>
        public static List<StateTransition> Apply(Dictionary<(int, string), string> states,
            Dictionary<(int, string), List<CellEvaluation>> history, IReadOnlyList<CellEvaluation> evaluations)
        {
            var list = new List<StateTransition>();
            foreach (var e in evaluations.OrderBy(e => e.OrganizationId).ThenBy(e => e.MarketFamily, StringComparer.Ordinal))
            {
                var key = e.Cell;
                if (!history.TryGetValue(key, out var h)) history[key] = h = new List<CellEvaluation>();
                var prior = h.Where(x => x.Lineage == e.Lineage).OrderBy(x => x.EvaluationCutoffUtc).ToList();
                var t = Decide(states.TryGetValue(key, out var s) ? s : PublishedStates.Closed, prior, e);
                states[key] = t.After;
                h.Add(e);
                list.Add(t);
            }
            return list;
        }

        public const string PreBootstrapHistoryReason = "PRE_BOOTSTRAP_HISTORY_NO_TRANSITION";
        public const string PreBootstrapHardFailReason = "PRE_BOOTSTRAP_HISTORY_HARD_FAIL_REPORTED";

        /// <summary>
        /// BOOTSTRAP ÇAPASI ÖNCESİ PENCERE — içeri alınan matris yayımlanmadan ÖNCEKİ kesimler kanıt geçmişine eklenir (açılma
        /// kuralının 5'li penceresine sayılır) ama durum DEĞİŞTİRMEZ: o tarihlerde hücre bu politikayla yayımlanmıyordu; geriye
        /// dönük geçiş uydurmak, bugün son pencerelerde geçen bir hücreyi Ağustos ölçümüyle kapatırdı. Açık hücrede geçmiş
        /// hard-fail varsa gerekçe koduyla RAPORLANIR.
        /// </summary>
        public static List<StateTransition> RecordHistory(IReadOnlyDictionary<(int, string), string> states,
            Dictionary<(int, string), List<CellEvaluation>> history, IReadOnlyList<CellEvaluation> evaluations)
        {
            var list = new List<StateTransition>();
            foreach (var e in evaluations.OrderBy(e => e.OrganizationId).ThenBy(e => e.MarketFamily, StringComparer.Ordinal))
            {
                if (!history.TryGetValue(e.Cell, out var h)) history[e.Cell] = h = new List<CellEvaluation>();
                h.Add(e);
                var s = states.TryGetValue(e.Cell, out var v) ? v : PublishedStates.Closed;
                var reason = e.IsHardFail && PublishedStates.IsVisible(s)
                    ? PreBootstrapHardFailReason + ":" + string.Join("+", e.RawGateReasons.Where(r => r.StartsWith("HARD_")))
                    : PreBootstrapHistoryReason;
                list.Add(new StateTransition(e.OrganizationId, e.MarketFamily, s, s, reason, null));
            }
            return list;
        }

        /// <summary>
        /// Değerlendirme yapılandırmasının parmak izi — model sürümü, kapı politikası, bütün eşikler, pencereler, değerlendirilen
        /// organizasyonlar ve lig kalibrasyon ekseni. Aynı yapılandırma aynı hash'i verir (tekrar koşuda duplicate engeli).
        /// </summary>
        public static string ConfigHash(string modelVersion, TimeSpan testWindow, TimeSpan calibrationWindow, TimeSpan trainWindow,
            IEnumerable<int> organizations, string leagueCalibrationAxes = "None", bool parsimoniousCandidate = true)
        {
            var ci = CultureInfo.InvariantCulture;
            var s = string.Join("|",
                modelVersion, MarketEligibilityPolicy.Version, EligibilityPolicy.Version,
                EligibilityPolicy.EnabledMinMatches.ToString(ci), EligibilityPolicy.DisabledBelowMatches.ToString(ci),
                EligibilityPolicy.MaxCalibrationErrorEnabled.ToString("R", ci), EligibilityPolicy.MaxCalibrationErrorLimited.ToString("R", ci),
                EligibilityPolicy.MaxHomeDrawBias.ToString("R", ci), EligibilityPolicy.MinRecentFinished.ToString(ci),
                EligibilityPolicy.BootstrapSamples.ToString(ci),
                testWindow.TotalDays.ToString(ci), calibrationWindow.TotalDays.ToString(ci), trainWindow.TotalDays.ToString(ci),
                string.Join(",", organizations.OrderBy(x => x)), "axes=" + leagueCalibrationAxes, "candidate=" + parsimoniousCandidate);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant()[..32];
        }

        /// <summary>Deterministik değerlendirme kimliği — aynı kesim + aynı config aynı kimliği verir.</summary>
        public static string EvaluationRunId(DateTime cutoffUtc, string configHash)
            => "elig-" + cutoffUtc.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "-" + configHash[..10];
    }

    /// <summary>
    /// DEĞERLENDİRME TAKVİMİ — haftada bir, Pazartesi 05:00 Europe/Istanbul (hafta sonu maçlarının sonuç botu yazımı bitmiş olur).
    /// Kayıtlar UTC tutulur. Türkiye 2016'dan beri yaz saati uygulamaz (sabit UTC+3); hesap yine de saat dilimi veritabanından
    /// yapılır, sabit ofset yazılmaz.
    /// </summary>
    public static class EligibilityEvaluationSchedule
    {
        public const DayOfWeek Day = DayOfWeek.Monday;
        public static readonly TimeSpan LocalTime = new(5, 0, 0);

        public static TimeZoneInfo Istanbul { get; } = Resolve();

        private static TimeZoneInfo Resolve()
        {
            foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Europe/Istanbul", "Europe/Istanbul");
        }

        /// <summary><paramref name="utcNow"/> anında ya da öncesinde kalan en son planlı değerlendirme (UTC).</summary>
        public static DateTime LatestSlotAtOrBefore(DateTime utcNow)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), Istanbul);
            var daysBack = ((int)local.DayOfWeek - (int)Day + 7) % 7;
            var slotLocal = local.Date.AddDays(-daysBack) + LocalTime;
            if (slotLocal > local) slotLocal = slotLocal.AddDays(-7);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(slotLocal, DateTimeKind.Unspecified), Istanbul);
        }

        /// <summary><paramref name="utcNow"/> sonrasındaki ilk planlı değerlendirme (UTC).</summary>
        public static DateTime NextSlotAfter(DateTime utcNow)
        {
            var latest = LatestSlotAtOrBefore(utcNow);
            var local = TimeZoneInfo.ConvertTimeFromUtc(latest, Istanbul).AddDays(7);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Istanbul);
        }

        /// <summary>ISO yıl-hafta anahtarı (Istanbul yerel saatine göre) — haftada tek yayın kararı bu anahtarla tekildir.</summary>
        public static string WeekKey(DateTime cutoffUtc)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(cutoffUtc, DateTimeKind.Utc), Istanbul);
            return ISOWeek.GetYear(local).ToString("0000", CultureInfo.InvariantCulture) + "-W" + ISOWeek.GetWeekOfYear(local).ToString("00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// OKUMA KATMANI — snapshot işinin organizasyon × market matrisi artık KARARLI yayın durumundan gelir. Ham koşu satırları
    /// yalnız örneklem sayısı için okunur; kararı VERMEZ. Durum tablosu boşsa (politika henüz başlatılmadıysa) eski davranış korunur.
    /// </summary>
    public static class EligibilityPublicationOverlay
    {
        public const string StateReasonPrefix = "PUBLICATION_STATE_";

        public static Dictionary<int, List<MarketFamilyMetrics>> Apply(Dictionary<int, List<MarketFamilyMetrics>> rawByLeague,
            IReadOnlyDictionary<(int, string), string> states)
        {
            if (states.Count == 0) return rawByLeague;
            var result = new Dictionary<int, List<MarketFamilyMetrics>>();
            foreach (var league in states.Keys.Select(k => k.Item1).Distinct().OrderBy(x => x))
            {
                var raw = rawByLeague.TryGetValue(league, out var r) ? r : new List<MarketFamilyMetrics>();
                var list = new List<MarketFamilyMetrics>();
                foreach (var family in MarketFamilies.All)
                {
                    var rawRow = raw.FirstOrDefault(x => x.Family == family);
                    var state = states.TryGetValue((league, family), out var s) ? s : null;
                    var visible = PublishedStates.IsVisible(state);
                    var reasons = visible
                        ? new List<string>()
                        : (rawRow?.ReasonCodes ?? new List<string>()).Append(StateReasonPrefix + (state ?? "MISSING")).Distinct().ToList();
                    list.Add(new MarketFamilyMetrics
                    {
                        LeagueId = league, Family = family, Matches = rawRow?.Matches ?? 0,
                        Status = visible ? MarketEligibilityStatuses.Eligible
                               : rawRow == null || rawRow.Status == MarketEligibilityStatuses.Eligible ? MarketEligibilityStatuses.Limited
                               : rawRow.Status,
                        ReasonCodes = reasons
                    });
                }
                result[league] = list;
            }
            return result;
        }

        /// <summary>Snapshot girdi özetine giren yayın imzası — bir hücre açılır/kapanırsa ilgili ligin snapshot'ı yeniden yazılır.</summary>
        public static string Signature(IEnumerable<MarketFamilyMetrics> rows)
            => "pub:" + string.Join(",", rows.OrderBy(r => r.Family, StringComparer.Ordinal).Select(r => r.Family + "=" + (r.IsEligible ? "1" : "0")));
    }
}
