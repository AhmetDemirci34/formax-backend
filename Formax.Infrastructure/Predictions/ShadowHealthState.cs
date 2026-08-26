using System;

namespace Formax.Infrastructure.Predictions
{
    /// <summary>
    /// SHADOW WORKER SAĞLIK DURUMU — süreç içi, tek örnek (singleton).
    ///
    /// Sürekli çalışma ortamında "iş gerçekten koşuyor mu?" sorusunun tek cevap yeri burasıdır.
    /// Yalnız ÖLÇÜM tutar: modele, kapıya, sözleşmeye ve tahmin üretimine hiçbir etkisi yoktur.
    /// Job bu nesneyi doldurur, /admin/shadow/health okur.
    ///
    /// Süreç yeniden başlarsa bu sayaçlar sıfırlanır — bu bir kusur değil, tanımdır: buradaki
    /// sayaçlar "BU SÜREÇ ne yaptı"yı söyler. Kalıcı gerçek (kaç tahmin var) DB'den okunur.
    /// </summary>
    public sealed class ShadowHealthState
    {
        private readonly object _gate = new();

        /// <summary>Süreç (host) başlangıç anı — UTC.</summary>
        public DateTime ProcessStartedUtc { get; } = DateTime.UtcNow;

        // ── Shadow prediction job ────────────────────────────────────────────────
        public DateTime? ShadowJobStartedUtc { get; private set; }
        public DateTime? ShadowLastCycleStartedUtc { get; private set; }
        public DateTime? ShadowLastSuccessfulCycleUtc { get; private set; }
        public long ShadowLastCycleMs { get; private set; }
        public int ShadowCyclesCompleted { get; private set; }
        public int ShadowCyclesFailed { get; private set; }
        public int ShadowConsecutiveFailures { get; private set; }
        public string? ShadowLastError { get; private set; }
        public DateTime? ShadowLastErrorUtc { get; private set; }

        // Kümülatif (bu süreç boyunca) cycle sonuçları
        public int ShadowInserted { get; private set; }
        public int ShadowAlreadyPublished { get; private set; }
        public int ShadowConflicted { get; private set; }
        public int ShadowFailedMatches { get; private set; }
        public int ShadowLastCycleSeen { get; private set; }
        public string? ShadowModelFingerprint { get; private set; }
        public DateTime? ShadowRatingEvidenceThroughUtc { get; private set; }

        /// <summary>Job hiç başlamadıysa sebebi (ör. motor dosyaları bulunamadı).</summary>
        public string? ShadowDisabledReason { get; private set; }

        // ── Settlement job ───────────────────────────────────────────────────────
        public DateTime? SettlementJobStartedUtc { get; private set; }
        public DateTime? SettlementLastSuccessfulCycleUtc { get; private set; }
        public int SettlementCyclesCompleted { get; private set; }
        public int SettlementCyclesFailed { get; private set; }
        public int SettlementSettled { get; private set; }
        public string? SettlementLastError { get; private set; }
        public DateTime? SettlementLastErrorUtc { get; private set; }

        public void ShadowStarted()
        {
            lock (_gate) { ShadowJobStartedUtc = DateTime.UtcNow; ShadowDisabledReason = null; }
        }

        public void ShadowDisabled(string reason)
        {
            lock (_gate) { ShadowDisabledReason = reason; }
        }

        public void ShadowCycleStarted()
        {
            lock (_gate) { ShadowLastCycleStartedUtc = DateTime.UtcNow; }
        }

        public void ShadowCycleSucceeded(ShadowCycleReport report)
        {
            lock (_gate)
            {
                ShadowLastSuccessfulCycleUtc = DateTime.UtcNow;
                ShadowLastCycleMs = report.ElapsedMs;
                ShadowCyclesCompleted++;
                ShadowConsecutiveFailures = 0;
                ShadowInserted += report.Inserted;
                ShadowAlreadyPublished += report.AlreadyPublished;
                ShadowConflicted += report.NotInsertedDueToConflict;
                ShadowFailedMatches += report.Failed;
                ShadowLastCycleSeen = report.UpcomingMatchesSeen;
                ShadowModelFingerprint = report.ModelFingerprint ?? ShadowModelFingerprint;
                ShadowRatingEvidenceThroughUtc =
                    report.RatingEvidenceThrough?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                    ?? ShadowRatingEvidenceThroughUtc;
            }
        }

        public void ShadowCycleFailed(Exception ex)
        {
            lock (_gate)
            {
                ShadowCyclesFailed++;
                ShadowConsecutiveFailures++;
                // Yalnız tip + mesajın ilk satırı. Bağlantı dizesi / anahtar bu yola girmez.
                ShadowLastError = Sanitize(ex);
                ShadowLastErrorUtc = DateTime.UtcNow;
            }
        }

        // ── Shadow B (NEWS_ADJUSTED deney hattı) ─────────────────────────────────
        // AYRI SAYAÇLAR. Yukarıdaki Shadow A alanlarının hiçbiri bu blok yüzünden değişmez.
        public DateTime? ShadowBJobStartedUtc { get; private set; }
        public DateTime? ShadowBLastCycleStartedUtc { get; private set; }
        public DateTime? ShadowBLastSuccessfulCycleUtc { get; private set; }
        public long ShadowBLastCycleMs { get; private set; }
        public int ShadowBCyclesCompleted { get; private set; }
        public int ShadowBCyclesFailed { get; private set; }
        public int ShadowBConsecutiveFailures { get; private set; }
        public string? ShadowBLastError { get; private set; }
        public DateTime? ShadowBLastErrorUtc { get; private set; }
        public int ShadowBInserted { get; private set; }
        public int ShadowBAlreadyPublished { get; private set; }
        public int ShadowBAdjusted { get; private set; }
        public int ShadowBNoEvidence { get; private set; }
        public int ShadowBConflicted { get; private set; }
        public int ShadowBSettledThisProcess { get; private set; }

        public void ShadowBStarted()
        {
            lock (_gate) { ShadowBJobStartedUtc = DateTime.UtcNow; }
        }

        public void ShadowBCycleStarted()
        {
            lock (_gate) { ShadowBLastCycleStartedUtc = DateTime.UtcNow; }
        }

        public void ShadowBCycleSucceeded(ShadowBCycleReport report)
        {
            lock (_gate)
            {
                ShadowBLastSuccessfulCycleUtc = DateTime.UtcNow;
                ShadowBLastCycleMs = report.ElapsedMs;
                ShadowBCyclesCompleted++;
                ShadowBConsecutiveFailures = 0;
                ShadowBInserted += report.Inserted;
                ShadowBAlreadyPublished += report.AlreadyPublished;
                ShadowBAdjusted += report.Adjusted;
                ShadowBNoEvidence += report.NoEvidence;
                ShadowBConflicted += report.NotInsertedDueToConflict;
                ShadowBSettledThisProcess += report.Settled;
            }
        }

        public void ShadowBCycleFailed(Exception ex)
        {
            lock (_gate)
            {
                ShadowBCyclesFailed++;
                ShadowBConsecutiveFailures++;
                ShadowBLastError = Sanitize(ex);
                ShadowBLastErrorUtc = DateTime.UtcNow;
            }
        }

        public ShadowBHealthSnapshot ShadowBSnapshot()
        {
            lock (_gate)
            {
                return new ShadowBHealthSnapshot
                {
                    JobStartedUtc = ShadowBJobStartedUtc,
                    LastCycleStartedUtc = ShadowBLastCycleStartedUtc,
                    LastSuccessfulCycleUtc = ShadowBLastSuccessfulCycleUtc,
                    LastCycleMs = ShadowBLastCycleMs,
                    CyclesCompleted = ShadowBCyclesCompleted,
                    CyclesFailed = ShadowBCyclesFailed,
                    ConsecutiveFailures = ShadowBConsecutiveFailures,
                    LastError = ShadowBLastError,
                    LastErrorUtc = ShadowBLastErrorUtc,
                    Inserted = ShadowBInserted,
                    AlreadyPublished = ShadowBAlreadyPublished,
                    Adjusted = ShadowBAdjusted,
                    NoEvidence = ShadowBNoEvidence,
                    Conflicted = ShadowBConflicted,
                    SettledThisProcess = ShadowBSettledThisProcess
                };
            }
        }

        public void SettlementStarted()
        {
            lock (_gate) { SettlementJobStartedUtc = DateTime.UtcNow; }
        }

        public void SettlementCycleSucceeded(int settled)
        {
            lock (_gate)
            {
                SettlementLastSuccessfulCycleUtc = DateTime.UtcNow;
                SettlementCyclesCompleted++;
                SettlementSettled += settled;
            }
        }

        public void SettlementCycleFailed(Exception ex)
        {
            lock (_gate)
            {
                SettlementCyclesFailed++;
                SettlementLastError = Sanitize(ex);
                SettlementLastErrorUtc = DateTime.UtcNow;
            }
        }

        public ShadowHealthSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new ShadowHealthSnapshot
                {
                    ProcessStartedUtc = ProcessStartedUtc,
                    ShadowJobStartedUtc = ShadowJobStartedUtc,
                    ShadowLastCycleStartedUtc = ShadowLastCycleStartedUtc,
                    ShadowLastSuccessfulCycleUtc = ShadowLastSuccessfulCycleUtc,
                    ShadowLastCycleMs = ShadowLastCycleMs,
                    ShadowCyclesCompleted = ShadowCyclesCompleted,
                    ShadowCyclesFailed = ShadowCyclesFailed,
                    ShadowConsecutiveFailures = ShadowConsecutiveFailures,
                    ShadowLastError = ShadowLastError,
                    ShadowLastErrorUtc = ShadowLastErrorUtc,
                    ShadowInserted = ShadowInserted,
                    ShadowAlreadyPublished = ShadowAlreadyPublished,
                    ShadowConflicted = ShadowConflicted,
                    ShadowFailedMatches = ShadowFailedMatches,
                    ShadowLastCycleSeen = ShadowLastCycleSeen,
                    ShadowModelFingerprint = ShadowModelFingerprint,
                    ShadowRatingEvidenceThroughUtc = ShadowRatingEvidenceThroughUtc,
                    ShadowDisabledReason = ShadowDisabledReason,
                    SettlementJobStartedUtc = SettlementJobStartedUtc,
                    SettlementLastSuccessfulCycleUtc = SettlementLastSuccessfulCycleUtc,
                    SettlementCyclesCompleted = SettlementCyclesCompleted,
                    SettlementCyclesFailed = SettlementCyclesFailed,
                    SettlementSettled = SettlementSettled,
                    SettlementLastError = SettlementLastError,
                    SettlementLastErrorUtc = SettlementLastErrorUtc
                };
            }
        }

        /// <summary>
        /// Hata metnini sağlık ucuna taşımadan önce daraltır: yalnız istisna TİPİ ve mesajın
        /// ilk satırı, 300 karakterle sınırlı. Amaç, bağlantı dizesi veya anahtar sızmasını
        /// yapısal olarak imkânsıza yaklaştırmak.
        /// </summary>
        private static string Sanitize(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            var firstLine = message.Split('\n')[0].Trim();
            if (firstLine.Length > 300) firstLine = firstLine.Substring(0, 300);
            return ex.GetType().Name + ": " + firstLine;
        }
    }

    /// <summary>Shadow B sayaçlarının anlık kopyası — A'nın snapshot'ından AYRI.</summary>
    public sealed class ShadowBHealthSnapshot
    {
        public DateTime? JobStartedUtc { get; init; }
        public DateTime? LastCycleStartedUtc { get; init; }
        public DateTime? LastSuccessfulCycleUtc { get; init; }
        public long LastCycleMs { get; init; }
        public int CyclesCompleted { get; init; }
        public int CyclesFailed { get; init; }
        public int ConsecutiveFailures { get; init; }
        public string? LastError { get; init; }
        public DateTime? LastErrorUtc { get; init; }
        public int Inserted { get; init; }
        public int AlreadyPublished { get; init; }
        public int Adjusted { get; init; }
        public int NoEvidence { get; init; }
        public int Conflicted { get; init; }
        public int SettledThisProcess { get; init; }
    }

    /// <summary>Sağlık ucunun okuduğu, kilit tutmayan anlık kopya.</summary>
    public sealed class ShadowHealthSnapshot
    {
        public DateTime ProcessStartedUtc { get; init; }
        public DateTime? ShadowJobStartedUtc { get; init; }
        public DateTime? ShadowLastCycleStartedUtc { get; init; }
        public DateTime? ShadowLastSuccessfulCycleUtc { get; init; }
        public long ShadowLastCycleMs { get; init; }
        public int ShadowCyclesCompleted { get; init; }
        public int ShadowCyclesFailed { get; init; }
        public int ShadowConsecutiveFailures { get; init; }
        public string? ShadowLastError { get; init; }
        public DateTime? ShadowLastErrorUtc { get; init; }
        public int ShadowInserted { get; init; }
        public int ShadowAlreadyPublished { get; init; }
        public int ShadowConflicted { get; init; }
        public int ShadowFailedMatches { get; init; }
        public int ShadowLastCycleSeen { get; init; }
        public string? ShadowModelFingerprint { get; init; }
        public DateTime? ShadowRatingEvidenceThroughUtc { get; init; }
        public string? ShadowDisabledReason { get; init; }
        public DateTime? SettlementJobStartedUtc { get; init; }
        public DateTime? SettlementLastSuccessfulCycleUtc { get; init; }
        public int SettlementCyclesCompleted { get; init; }
        public int SettlementCyclesFailed { get; init; }
        public int SettlementSettled { get; init; }
        public string? SettlementLastError { get; init; }
        public DateTime? SettlementLastErrorUtc { get; init; }
    }
}
