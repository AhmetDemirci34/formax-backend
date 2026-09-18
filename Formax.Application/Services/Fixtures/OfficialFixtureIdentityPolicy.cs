using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>Kanonik maç adayı — kimlik kararı için gereken EN AZ alan (ağ/DB yok).</summary>
    public sealed record CanonicalMatchCandidate(
        int MatchId,
        int LeagueId,
        int HomeTeamId,
        int AwayTeamId,
        DateTime KickoffUtc,
        string? ExternalMatchId);

    /// <summary>Kimlik kararı.</summary>
    public sealed record CanonicalFixtureDecision(string Outcome, int? MatchId, string Reason)
    {
        /// <summary>Kanonik maç yok — yeni satır yazılabilir.</summary>
        public const string Create = "Create";
        /// <summary>Kanonik maç VAR — yeni satır YAZILMAZ, bu maça ikinci sağlayıcı referansı bağlanır.</summary>
        public const string Adopt = "Adopt";
        /// <summary>Birden fazla ya da çelişkili aday — DUPLICATE ÜRETİLMEZ, kayıt incelemeye bırakılır.</summary>
        public const string Ambiguous = "Ambiguous";
    }

    /// <summary>
    /// KANONİK FİKSTÜR KİMLİĞİ — aynı maç iki farklı sağlayıcıdan geldiğinde İKİNCİ Match satırı oluşmasın.
    ///
    /// NEDEN GEREKLİ (ölçüldü 18.09.2026): resmî UEFA kaynağı maçı haftalar önce yazıyor; api-football aynı
    /// maçı en fazla T−1'de veriyor ve kendi kimliği (<c>ExternalMatchId</c>) ile arıyor. O kimlik UEFA satırında
    /// yok, dolayısıyla eşleşme bulunamaz ve duplicate oluşur. Kimlik bu yüzden şunların BİRLİKTE'sidir:
    /// organizasyon + SIRALI takım çifti (ev/deplasman) + dar başlama penceresi + TEK aday.
    ///
    /// ÇİFT MAÇLI TUR TUZAĞI (kilitli kural): UEFA play-off'unda aynı iki takım TERS yönde ikinci maç oynar
    /// (Fenerbahçe–Lyon 18.08 / Lyon–Fenerbahçe 26.08). Bu yüzden çift SIRALI karşılaştırılır ve pencere
    /// <see cref="KickoffTolerance"/> ile dardır: iki ayak asla aynı kimliğe çözülemez.
    /// </summary>
    public static class OfficialFixtureIdentityPolicy
    {
        /// <summary>
        /// İki sağlayıcının aynı maç için bildirdiği başlama saati farkının üst sınırı. Aynı günün saat
        /// değişikliğini tolere eder; çift maçlı turun iki ayağı (≥ 6 gün) ASLA bu pencereye girmez.
        /// </summary>
        public static readonly TimeSpan KickoffTolerance = TimeSpan.FromHours(36);

        /// <summary>
        /// Kanonik maç kararı.
        /// </summary>
        /// <param name="providerExternalId">
        /// api-football yönünde sağlayıcının fikstür kimliği. Tek aday BAŞKA bir api-football kimliği
        /// taşıyorsa bu iki AYRI fikstürdür → <see cref="CanonicalFixtureDecision.Ambiguous"/> (üzerine yazılmaz).
        /// </param>
        public static CanonicalFixtureDecision Resolve(
            int leagueId, int homeTeamId, int awayTeamId, DateTime kickoffUtc,
            IEnumerable<CanonicalMatchCandidate> candidates, string? providerExternalId = null)
        {
            if (homeTeamId <= 0 || awayTeamId <= 0 || homeTeamId == awayTeamId)
                return new(CanonicalFixtureDecision.Ambiguous, null, "InvalidTeams");

            var matched = (candidates ?? Enumerable.Empty<CanonicalMatchCandidate>())
                .Where(c => c.LeagueId == leagueId
                            && c.HomeTeamId == homeTeamId
                            && c.AwayTeamId == awayTeamId
                            && (c.KickoffUtc - kickoffUtc).Duration() <= KickoffTolerance)
                .ToList();

            if (matched.Count == 0) return new(CanonicalFixtureDecision.Create, null, "NoCanonicalMatch");
            if (matched.Count > 1)
                return new(CanonicalFixtureDecision.Ambiguous, null,
                    "MultipleCandidates:" + string.Join(",", matched.Select(m => m.MatchId)));

            var only = matched[0];
            if (!string.IsNullOrWhiteSpace(providerExternalId)
                && !string.IsNullOrWhiteSpace(only.ExternalMatchId)
                && !string.Equals(only.ExternalMatchId, providerExternalId, StringComparison.Ordinal))
                return new(CanonicalFixtureDecision.Ambiguous, only.MatchId, "ExternalIdMismatch:" + only.ExternalMatchId);

            return new(CanonicalFixtureDecision.Adopt, only.MatchId, "CanonicalMatchFound");
        }
    }
}
