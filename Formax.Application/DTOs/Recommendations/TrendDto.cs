namespace Formax.Application.DTOs.Recommendations;

public class TrendDto
{
    public int PlayRate { get; set; }
    public int TrendDelta { get; set; }
    public double GlobalTrend { get; set; }
    public bool IsTrending { get; set; }

    // 🔥 EKLE (ENGINE İÇİN ZORUNLU)
    public DateTime? LastUpdatedAt { get; set; }

}