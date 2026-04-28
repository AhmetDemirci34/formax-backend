using Formax.Domain.Entities;

public class ExplainBuilder
{
    public List<string> Build(
        UserPickStats? pickStats,
        UserConfidence? confidence,
        double probability,
        double edge)
    {
        var reasons = new List<string>();

        // 🔥 USER SKILL
        if (pickStats != null && pickStats.Total >= 5)
        {
            var acc = (int)(pickStats.Accuracy * 100);

            if (acc >= 65)
                reasons.Add($"🔥 Bu tipte iyisin (%{acc})");
        }

        // 🔥 CONFIDENCE
        if (confidence != null)
        {
            if (confidence.Value >= 70)
                reasons.Add("📈 Formun yüksek");

            else if (confidence.Value <= 30)
                reasons.Add("⚠️ Formun düşük");
        }

        // 🔥 MATCH LOGIC
        if (probability >= 65)
            reasons.Add("⚡ Yüksek olasılık");

        if (edge >= 10)
            reasons.Add("💎 Değerli oran");

        return reasons;
    }
}
