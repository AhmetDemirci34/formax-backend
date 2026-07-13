using System;

namespace Formax.Domain.Entities
{
    /// <summary>
    /// FORMAX Data Engine v1 — keşfedilen maçın kalıcı kaydı (veri omurgası).
    /// FORMAX_MATCH_ID sistemin TEK kimliğidir; dış API MatchId'sine bağımlı değildir.
    /// Bundan sonra News / Reasoning / AI / Analytics / Timeline bu ID ile çalışır.
    /// </summary>
    public class Fixture
    {
        public int Id { get; set; }

        /// <summary>FORMAX'ın kendi ürettiği deterministik kimlik (benzersiz).</summary>
        public string FormaxMatchId { get; set; } = "";

        public string Country { get; set; } = "";
        public string League { get; set; } = "";
        public string Season { get; set; } = "";
        public string? Round { get; set; }

        public DateTime KickoffUtc { get; set; }

        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string? Venue { get; set; }

        /// <summary>NotStarted | Live | Finished</summary>
        public string Status { get; set; } = "NotStarted";

        /// <summary>Çok-kaynak mutabakat güveni (0–100).</summary>
        public int Confidence { get; set; }

        /// <summary>Bu maçı doğrulayan kaynaklar (virgülle ayrılmış).</summary>
        public string Sources { get; set; } = "";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
