using Formax.Application.DTOs.Recommendations;
using System.Text;

public class DetailAnalysisEngine
{
    private static readonly Random _rnd = new();

    public string Generate(RecommendationCardDto x)
    {
        var sb = new StringBuilder();

        sb.AppendLine(BuildIntro(x));
        sb.AppendLine(BuildStrength(x));
        sb.AppendLine(BuildRisk(x));
        sb.AppendLine(BuildMarket(x));

        return sb.ToString().Trim();
    }

    // 🔥 1. GİRİŞ
    private string BuildIntro(RecommendationCardDto x)
    {
        if (x.ConfidenceScore > 0.7)
            return $"{x.TeamA} bu maçta daha önde görünüyor.";

        if (x.ConfidenceScore < 0.4)
            return "Bu maçta net bir üstünlük yok.";

        return "Taraflar birbirine yakın görünüyor.";
    }

    // 🔥 2. GÜÇ
    private string BuildStrength(RecommendationCardDto x)
    {
        if (x.ConfidenceScore > 0.7)
        {
            return _rnd.Next(2) == 0
                ? $"{x.TeamA} son dönemde daha dengeli bir performans sergiliyor."
                : $"{x.TeamA} oyun yapısı olarak daha oturmuş görünüyor.";
        }

        return _rnd.Next(2) == 0
            ? "İki takım da benzer seviyede performans gösteriyor."
            : "Taraflar arasında belirgin bir kalite farkı yok.";
    }

    // 🔥 3. RİSK
    private string BuildRisk(RecommendationCardDto x)
    {
        if (x.ConfidenceScore < 0.4)
        {
            return _rnd.Next(2) == 0
                ? "Bu yüzden sürpriz ihtimali oldukça yüksek."
                : "Bu tip maçlar genelde beklenmedik sonuçlar üretir.";
        }

        if (x.ConfidenceScore < 0.6)
        {
            return "Maç içinde denge kolayca değişebilir.";
        }

        return "Ancak oyun içi kırılmalar sonucu etkileyebilir.";
    }

    // 🔥 4. PİYASA / TREND
    private string BuildMarket(RecommendationCardDto x)
    {
        if (x.Trend?.PlayRate > 70)
        {
            return _rnd.Next(2) == 0
                ? "Kullanıcıların büyük kısmı bu maça yönelmiş durumda."
                : "Bu maçta ciddi bir kullanıcı ilgisi var.";
        }

        if (x.Trend?.PlayRate < 30)
        {
            return _rnd.Next(2) == 0
                ? "Bu maça olan ilgi oldukça düşük."
                : "Piyasa bu karşılaşmada sakin görünüyor.";
        }

        return "Bu maçta ilgi dengeli seviyede.";
    }
}