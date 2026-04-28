public class ConfidenceCalculator
{
    public ConfidenceResult Calculate(double baseScore, double trendScore, double externalScore)
    {
        // normalize
        var baseNormalized = Clamp(baseScore / 15.0);
        var trendNormalized = Clamp(trendScore / 100.0);
        var externalNormalized = Clamp(externalScore / 10.0);

        // 🔥 UPDATED WEIGHTS (external güçlendirildi)
        var final =
            (baseNormalized * 0.55) +
            (trendNormalized * 0.20) +
            (externalNormalized * 0.25);

        // 🔥 düşük veri = risk artır
        if (trendScore < 20 && externalScore < 2)
        {
            final -= 0.1;
        }

        final = Clamp(final);

        return new ConfidenceResult
        {
            Score = final,
            Label = GetLabel(final),
            Reason = GetReason(final)
        };
    }

    // 🔥 THRESHOLD DARALTILDI
    private string GetLabel(double score)
    {
        if (score >= 0.85) return "SAFE";
        if (score >= 0.65) return "BALANCED";
        return "RISKY";
    }

    // 🔥 USER MESSAGE EKLENDİ (AI konuşuyor hissi)
    private string GetReason(double score)
    {
        if (score >= 0.85)
            return "Piyasa güçlü, risk düşük";

        if (score >= 0.65)
            return "Piyasa dengeli, kontrollü ilerliyorsun";

        return "Belirsizlik yüksek, dikkatli ol";
    }

    private double Clamp(double value)
    {
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}

public class ConfidenceResult
{
    public double Score { get; set; }
    public string Label { get; set; } = "";
    public string Reason { get; set; } = ""; // 🔥 yeni alan
}