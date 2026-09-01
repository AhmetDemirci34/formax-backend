using Formax.Application.DTOs.Standings;

namespace Formax.Application.Services.Standings
{
    /// <summary>
    /// MAÇ DETAYINDA PUAN DURUMU SUNUM KARARI — tek merkez.
    ///
    /// "Hangi maçta tablo gösterilir, başlığı ne olur, gösterilmiyorsa kullanıcı ne görür"
    /// sorusunun cevabı burada verilir; UI bu kararı YENİDEN ÜRETMEZ. Böylece backend ile
    /// ekran farklı şeyler söyleyemez.
    /// </summary>
    public static class StandingsPresentation
    {
        public const string AvailabilityTable          = "Table";
        public const string AvailabilityLeaguePhase    = "LeaguePhaseTable";
        public const string AvailabilityNotApplicable  = "NotApplicable";
        public const string AvailabilityNotAvailable   = "NotAvailable";
        public const string AvailabilityUnresolved     = "Unresolved";

        public const string LeaguePhaseTitle = "Lig Aşaması Puan Durumu";
        public const string DefaultTitle     = "Puan Durumu";

        /// <summary>Eleme aşamasındaki maçta kullanıcıya gösterilen NÖTR metin.</summary>
        public const string QualifyingNotice =
            "Bu karşılaşma eleme aşamasındadır. Puan durumu bulunmaz.";

        /// <summary>Lig aşaması henüz başlamadıysa.</summary>
        public const string LeaguePhaseNotStartedNotice =
            "Lig aşaması henüz başlamadı. Puan durumu bu aşama başlayınca oluşur.";

        /// <summary>Aşama çözülemediğinde — teknik ayrıntı SIZDIRILMAZ.</summary>
        public const string UnresolvedNotice =
            "Bu karşılaşmanın turnuva aşaması doğrulanamadı. Puan durumu gösterilmiyor.";

        public sealed record Decision(
            string Availability,
            string? Title,
            string? Notice,
            string? Diagnostic,
            bool ShowTable);

        /// <summary>
        /// <paramref name="phase"/> maçın aşaması, <paramref name="hasLeaguePhaseTable"/> ise
        /// o lig+sezon için gerçekten satırı olan bir lig aşaması tablosu var mı.
        /// </summary>
        public static Decision Decide(
            int leagueId, CompetitionPhase phase, bool hasLeaguePhaseTable)
        {
            // ── Ulusal lig: normal tablo ─────────────────────────────────────────
            if (Domain.Constants.LockedCompetitions.IsDomestic(leagueId))
                return hasLeaguePhaseTable
                    ? new Decision(AvailabilityTable, DefaultTitle, null, null, true)
                    : new Decision(AvailabilityNotAvailable, DefaultTitle, null, null, false);

            // ── Aşama çözülemedi: tablo YOK, teknik kod yalnız teşhiste ──────────
            if (phase == CompetitionPhase.Unknown)
                return new Decision(
                    AvailabilityUnresolved, null, UnresolvedNotice,
                    CompetitionPhaseResolver.PhaseUnresolvedCode, false);

            // ── Eleme aşaması: puan durumu KAVRAM OLARAK yoktur ──────────────────
            if (CompetitionPhaseResolver.IsQualifyingStage(phase))
                return new Decision(AvailabilityNotApplicable, null, QualifyingNotice, null, false);

            // ── Lig aşaması ve knockout: her ikisinde de LİG AŞAMASI tablosu ─────
            // Knockout maçında gösterilen tablo o turun kendi tablosu DEĞİLDİR; başlık
            // her iki durumda da "Lig Aşaması Puan Durumu" olduğu için izlenim doğrudur.
            if (phase == CompetitionPhase.LeaguePhase || CompetitionPhaseResolver.IsKnockoutStage(phase))
                return hasLeaguePhaseTable
                    ? new Decision(AvailabilityLeaguePhase, LeaguePhaseTitle, null, null, true)
                    : new Decision(AvailabilityNotAvailable, LeaguePhaseTitle,
                        LeaguePhaseNotStartedNotice, null, false);

            return new Decision(AvailabilityNotAvailable, null, null, null, false);
        }

        /// <summary>Aşamayı DTO'ya taşınacak metne çevirir.</summary>
        public static string PhaseName(CompetitionPhase phase) => phase.ToString();
    }
}
