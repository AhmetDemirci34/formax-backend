using System;
using System.Collections.Generic;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — motorun nihai çıktısı. Birden fazla kaynaktan gelen
    /// adaylar tek kimliğe (FORMAX_MATCH_ID) indirgenir, çok-kaynak mutabakatıyla
    /// güven hesaplanır. Bu ID sistem genelinde (News/Discovery/Reasoning/Scenario)
    /// kullanılacak ortak kimliktir — dış API MatchId'sine bağımlı değildir.
    /// </summary>
    public sealed class FixtureDiscoveryResult
    {
        public string FormaxMatchId { get; set; } = "";
        public string League { get; set; } = "";
        public string Country { get; set; } = "";
        public string Season { get; set; } = "";
        public string? Round { get; set; }
        public DateTime KickoffUtc { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string? Venue { get; set; }
        public string Status { get; set; } = "";

        /// <summary>Çok-kaynak mutabakat güveni (0–100).</summary>
        public int Confidence { get; set; }

        /// <summary>Bu maçı doğrulayan kaynakların adları.</summary>
        public List<string> Sources { get; set; } = new();
    }
}
