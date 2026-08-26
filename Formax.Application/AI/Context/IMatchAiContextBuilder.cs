using Formax.Application.DTOs.Matches;

namespace Formax.Application.AI.Context
{
    /// <summary>
    /// GDP-türevli maç sinyallerini tek <see cref="UnifiedMatchAiContext"/>'e indirger.
    /// Motorun gördüğü tüm veri bu katmandan geçer → motor repo/provider/ham hesap yapmaz.
    ///
    /// FAZ 1 imzası mevcut hesaplanmış sinyalleri (TeamComparison/H2H/GücSkoru) alır.
    /// Sonraki fazlarda bu arayüz, ek GDP sinyalleriyle (availability, evidence...) zenginleşen
    /// overload'lar kazanır; motorun bağımlılığı (UnifiedMatchAiContext) sabit kalır.
    /// </summary>
    public interface IMatchAiContextBuilder
    {
        UnifiedMatchAiContext Build(
            int matchId,
            int homeTeamId,
            int awayTeamId,
            TeamComparisonDto home,
            TeamComparisonDto away,
            H2HDto h2h,
            int gucSkoru,
            string homeName,
            string awayName);

        /// <summary>
        /// SELF-SERVE giriş: TeamComparison / H2H / GücSkoru'yu çağırandan BEKLEMEZ, mevcut
        /// Matches verisinden kendisi üretir (MatchComparisonFactory + IGucSkoruCalculator).
        ///
        /// Bunlar daha önce yalnız Detail use-case'inde doluyordu; diğer AI yüzeyleri boş DTO
        /// geçtiği için aynı maç farklı yüzeylerde farklı değerlendiriliyordu. Bu overload
        /// tüm yüzeyleri AYNI gerçek veriye bağlar. Yeni API çağrısı/job YOKTUR.
        /// </summary>
        UnifiedMatchAiContext Build(
            int matchId,
            int homeTeamId,
            int awayTeamId,
            string homeName,
            string awayName);
    }
}
