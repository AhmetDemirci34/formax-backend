using Formax.Domain.Entities;
using System.Text;

namespace Formax.Application.Services
{
    public class AICouponCommentService
    {
        public string GenerateComment(
            List<CouponItem> items,
            double averageConfidence)
        {
            var sb = new StringBuilder();

            if (items == null || items.Count == 0)
                return "Kuponda henüz yeterli veri yok.";

            if (items.Count >= 3)
            {
                sb.AppendLine("Bu kupon birden fazla maç içermektedir ve parçalı risk taşır.");
            }

            var hasSameMatchSelections = items
                .GroupBy(i => i.MatchId)
                .Any(g => g.Count() > 1);

            if (hasSameMatchSelections)
            {
                sb.AppendLine("Aynı maçtan birden fazla seçim yapılmıştır. Bu durum senaryoyu daha kırılgan hale getirir.");
            }

            if (items.Count >= 5)
            {
                sb.AppendLine("Kupondaki seçim sayısı yüksektir. Risk seviyesi doğal olarak artmaktadır.");
            }

            if (averageConfidence < 0.70)
            {
                sb.AppendLine("Kupondaki maçların genel güven seviyesi düşüktür. Senaryo bazlı değerlendirme yapılmalıdır.");
            }

            sb.AppendLine("FORMAX kupon yorumları kesinlik içermez ve yalnızca senaryo bazlı değerlendirme sunar.");

            return sb.ToString().Trim();
        }
    }
}
