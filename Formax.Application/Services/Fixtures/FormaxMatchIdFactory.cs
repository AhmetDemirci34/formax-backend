using System;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Match Identity Engine — dış API MatchId'sine bağımlı OLMAYAN, deterministik
    /// kendi kimliğimiz. Aynı maç (lig + gün + ev + deplasman) hangi kaynaktan gelirse
    /// gelsin AYNI FORMAX_MATCH_ID'ye çözülür → çok-kaynak birleştirmenin anahtarı.
    ///
    ///   Kimlik = SHA256( leagueSlug | yyyyMMdd(UTC) | homeSlug | awaySlug )  → "FMX-" + 16 hex
    /// </summary>
    public sealed class FormaxMatchIdFactory
    {
        private readonly TeamIdentityResolver _teams;
        private readonly LeagueIdentityResolver _leagues;

        public FormaxMatchIdFactory(TeamIdentityResolver teams, LeagueIdentityResolver leagues)
        {
            _teams = teams;
            _leagues = leagues;
        }

        public string Create(string league, DateTime dateUtc, string homeTeam, string awayTeam)
        {
            var key = string.Join('|',
                _leagues.Slug(league),
                dateUtc.ToUniversalTime().ToString("yyyyMMdd"),
                _teams.Slug(homeTeam),
                _teams.Slug(awayTeam));

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return "FMX-" + Convert.ToHexString(hash, 0, 8); // 16 hex karakter
        }
    }
}
