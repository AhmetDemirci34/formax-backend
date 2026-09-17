using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.OfficialSources
{
    /// <summary>FORMAX tarafındaki maç kimliği (kimlik çözücünün girdisi).</summary>
    public sealed record FormaxMatchIdentity(
        int MatchId,
        int LeagueId,
        string HomeName,
        string AwayName,
        DateTime KickoffUtc);

    /// <summary>Kimlik kararı — ret gerekçesi makine okunurdur.</summary>
    public sealed record OfficialIdentityDecision(
        bool Accepted,
        string Reason,
        OfficialMatchRecord? Record,
        TimeSpan? KickoffDelta)
    {
        public const string ReasonAccepted = "Accepted";
        public const string ReasonNoCandidate = "NoCandidate";
        public const string ReasonOrientationReversed = "OrientationReversed";
        public const string ReasonKickoffOutOfWindow = "KickoffOutOfWindow";
        public const string ReasonAmbiguous = "Ambiguous";
        public const string ReasonNoKickoff = "NoKickoff";
    }

    /// <summary>
    /// RESMÎ MAÇ KİMLİĞİ ÇÖZÜCÜ — saf karar, ağ/DB yok.
    ///
    /// Kabul için HEPSİ gerekir:
    ///  • kaynağın ev sahibi = FORMAX ev sahibi VE kaynağın deplasmanı = FORMAX deplasmanı
    ///    (<see cref="OfficialTeamNameMatcher.SameTeam"/>);
    ///  • başlama anları arasındaki fark izin verilen pencere içinde;
    ///  • pencere içinde bu şartı sağlayan TEK aday.
    /// Takımlar ters sırada eşleşiyorsa (Lyon–Fenerbahçe ↔ Fenerbahçe–Lyon) maç REDDEDİLİR:
    /// çift ayaklı turda takım adı kanıt değildir, yön kanıttır.
    ///
    /// Lig kimliği çözücüye gelmeden sağlanır: çağıran yalnız o ligin resmî kaynağının
    /// kayıtlarını verir.
    /// </summary>
    public static class OfficialMatchIdentityResolver
    {
        /// <summary>
        /// Varsayılan tarih penceresi. Ligde sıralı ikili (ev, deplasman) sezonda bir kez oynanır;
        /// 72 saatlik pencere, ertelenen/öne çekilen maçı aynı maç olarak tanır ama bir sonraki
        /// hafta oynanan başka bir maçı asla yakalamaz.
        /// </summary>
        public static readonly TimeSpan DefaultKickoffWindow = TimeSpan.FromHours(72);

        /// <summary>
        /// Kaynak takımın tam adının yanında kısa adını da veriyorsa ("TSG Hoffenheim" / "Hoffenheim")
        /// ikisinden biri eşleşmelidir. Kısa ad kaynağın KENDİ verisidir; FORMAX tarafında takma ad uydurulmaz.
        /// </summary>
        public static bool HomeMatches(OfficialMatchRecord r, string formaxName)
            => OfficialTeamNameMatcher.SameTeam(r.HomeName, formaxName)
               || (r.Extra?.GetValueOrDefault("homeAltName") is { } alt && OfficialTeamNameMatcher.SameTeam(alt, formaxName))
               || (r.Extra?.GetValueOrDefault("homeAltName2") is { } alt2 && OfficialTeamNameMatcher.SameTeam(alt2, formaxName));

        public static bool AwayMatches(OfficialMatchRecord r, string formaxName)
            => OfficialTeamNameMatcher.SameTeam(r.AwayName, formaxName)
               || (r.Extra?.GetValueOrDefault("awayAltName") is { } alt && OfficialTeamNameMatcher.SameTeam(alt, formaxName))
               || (r.Extra?.GetValueOrDefault("awayAltName2") is { } alt2 && OfficialTeamNameMatcher.SameTeam(alt2, formaxName));

        public static OfficialIdentityDecision Resolve(
            FormaxMatchIdentity match,
            IEnumerable<OfficialMatchRecord> records,
            TimeSpan? kickoffWindow = null)
        {
            var window = kickoffWindow ?? DefaultKickoffWindow;
            var list = records as IReadOnlyCollection<OfficialMatchRecord> ?? records.ToList();

            var sameOrientation = list
                .Where(r => HomeMatches(r, match.HomeName) && AwayMatches(r, match.AwayName))
                .ToList();

            var inWindow = sameOrientation
                .Where(r => r.KickoffUtc.HasValue
                         && (r.KickoffUtc.Value - match.KickoffUtc).Duration() <= window)
                .ToList();

            if (inWindow.Count == 1)
            {
                var rec = inWindow[0];
                return new OfficialIdentityDecision(true, OfficialIdentityDecision.ReasonAccepted,
                    rec, rec.KickoffUtc!.Value - match.KickoffUtc);
            }
            if (inWindow.Count > 1)
                return new OfficialIdentityDecision(false, OfficialIdentityDecision.ReasonAmbiguous, null, null);

            if (sameOrientation.Count > 0)
            {
                var nearest = sameOrientation
                    .Where(r => r.KickoffUtc.HasValue)
                    .OrderBy(r => (r.KickoffUtc!.Value - match.KickoffUtc).Duration())
                    .FirstOrDefault();
                return nearest == null
                    ? new OfficialIdentityDecision(false, OfficialIdentityDecision.ReasonNoKickoff, null, null)
                    : new OfficialIdentityDecision(false, OfficialIdentityDecision.ReasonKickoffOutOfWindow,
                        null, nearest.KickoffUtc!.Value - match.KickoffUtc);
            }

            var reversed = list.Any(r => HomeMatches(r, match.AwayName) && AwayMatches(r, match.HomeName)
                                      && r.KickoffUtc.HasValue
                                      && (r.KickoffUtc.Value - match.KickoffUtc).Duration() <= window);
            return new OfficialIdentityDecision(false,
                reversed ? OfficialIdentityDecision.ReasonOrientationReversed : OfficialIdentityDecision.ReasonNoCandidate,
                null, null);
        }
    }
}
