using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// Gerçek Güç (oluşan veri) skorunu üretir.
    /// Kaynaklar (hedef): son 5 trend, xG farkı, ev/deplasman, tempo, savunma kırılganlığı.
    /// Mevcut DB alanlarıyla mümkün olanlar hesaplanır; diğerleri placeholder bırakılır.
    /// </summary>
    public interface IGucSkoruCalculator
    {
        GucSkoruResult CalculateForMatch(int matchId);
    }

    public sealed class GucSkoruResult
    {
        public int GucSkoru { get; init; }              // 0–100 (maç genel)
        public string GercekGucYonu { get; init; } = "None"; // Home/Away/Draw/None

        // İleride analiz ekranı için detaylar (opsiyonel)
        public int HomeStrength { get; init; }
        public int AwayStrength { get; init; }
    }
}
