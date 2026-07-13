using System;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Data Engine v1 — bir provider'ın döndürdüğü tek, normalize edilmiş fixture
    /// adayı. Her provider kendi ham şemasını bu modele çevirir → motor tek tip görür.
    /// Ücretli API bağımlılığı yok; kaynak Provider mantığıyla soyutlanır.
    /// </summary>
    public sealed class FixtureCandidate
    {
        public string League { get; set; } = "";
        public string Country { get; set; } = "";
        public string Season { get; set; } = "";
        public string? Round { get; set; }
        public DateTime DateUtc { get; set; }            // KickoffUtc
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public string? Venue { get; set; }
        public string Status { get; set; } = "";

        /// <summary>Bu adayı üreten provider adı (ör. "TheSportsDB").</summary>
        public string Source { get; set; } = "";

        /// <summary>Provider'ın kendi veri kalitesi güveni (0–100).</summary>
        public int SourceConfidence { get; set; } = 70;

        /// <summary>Çakışmada hangi kaynağın kazanacağını belirleyen öncelik (yüksek = öncelikli).</summary>
        public int ProviderPriority { get; set; } = 1;
    }
}
