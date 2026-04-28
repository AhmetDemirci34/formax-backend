using Formax.Application.DTOs.Matches;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// FORMAX ANA REFERANS v1.1
    /// Sapma Motoru: OynanmaSkoru (çoğunluk yoğunluğu) ile Gerçek Güç (oluşan veri) arasındaki sapmayı üretir.
    /// Bahis dili yok, oran/yüzde gösterimi yok. UI metinle anlatır.
    /// </summary>
    public interface ISapmaMotor
    {
        SapmaMotorResult CalculateForListItem(MatchListItemDto match);
    }

    public sealed class SapmaMotorResult
    {
        public int OynanmaSkoru { get; init; } // 0–100
        public int GucSkoru { get; init; }     // 0–100
        public int Sapma { get; init; }        // 0–100

        public string OynanmaYonu { get; init; } = "Denge";   // Home/Away/Denge
        public string GercekGucYonu { get; init; } = "Denge"; // Home/Away/Denge

        // --------------------------------------------------
        // Freshness (tazelik)
        // UI LastKnown: 5 dk
        // Analiz MaxTTL: 120 sn
        // --------------------------------------------------
        public string OynanmaFreshness { get; init; } = "Stale"; // Live / LastKnown / Stale
        public int OynanmaAgeSeconds { get; init; }
        public bool AnalysisMuted { get; init; }

        public string SapmaBolgesi { get; init; } = "Denge Bölgesi";
        public bool SessizMi { get; init; }
        public string SapmaMetni { get; init; } = string.Empty;
    }
}
