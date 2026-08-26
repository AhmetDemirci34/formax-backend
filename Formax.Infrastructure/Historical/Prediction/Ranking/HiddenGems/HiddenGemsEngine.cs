using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.HiddenGems;

/// <summary>Hidden Gem analizi üreten motor sözleşmesi (saf, deterministik).</summary>
public interface IHiddenGemsEngine
{
    HiddenGemAnalysis Analyze(RadarMatchContext context, HiddenGemWeights weights);
}

/// <summary>
/// Hidden Gems Engine — BAĞIMSIZ analiz katmanı (sıralama değil, rozet değil). "Feed'de öne çıkmayacak ama
/// yüksek izleme değeri olan" maçları tespit eder. SAF + DETERMİNİSTİK. Mevcut Radar/Discovery/Recommendation'a
/// DOKUNMAZ; yalnız çıktılarını okur. HiddenGemScore = KALİTE × OBSCURITY: her ikisi de yüksek olmalı →
/// (a) büyük takım (yüksek TeamStrength) obscurity'yi düşürür + HARD CAP ile asla gem olmaz, (b) yalnız düşük
/// skor kalite tabanını geçemez → gem olmaz. Veri olmayan sinyal hesaba katılmaz; kritik veri (kalite yok /
/// TeamStrength yok) → gem üretilmez (uydurma yok).
/// </summary>
public sealed class HiddenGemsEngine : IHiddenGemsEngine
{
    public HiddenGemAnalysis Analyze(RadarMatchContext ctx, HiddenGemWeights w)
    {
        var sig = ctx.Radar.Breakdown.ToDictionary(b => b.Signal, b => b, StringComparer.Ordinal);
        double? Val(string name) => sig.TryGetValue(name, out var s) && s.DataAvailable ? s.Value : null;

        var confidence = Math.Clamp(ctx.Confidence.ConfidenceScore, 0, 1);
        var teamStrength = Val("TeamStrength");
        var stakes = Val("Stakes");
        var interestDelta = ctx.Discovery.PersonalScore - ctx.Discovery.BaseScore;

        var breakdown = new List<SignalContribution>();

        // ── KALİTE (yüksek izleme değeri) ──
        var quality = new List<(string name, double v, double w)>();
        void Q(string name, double? v, double weight) { if (v is double d) quality.Add((name, d, weight)); }
        Q("Competitiveness", Val("Competitiveness"), w.Competitiveness);
        Q("GoalPotential", Val("GoalPotential"), w.GoalPotential);
        Q("Form", Val("Form"), w.Form);
        Q("H2HIntensity", Val("H2HIntensity"), w.H2HIntensity);
        Q("Confidence", confidence, w.Confidence);

        var qWeight = quality.Sum(x => x.w);
        var qualityFrac = qWeight > 0 ? quality.Sum(x => x.w * x.v) / qWeight : 0.0;
        foreach (var (name, v, weight) in quality)
            breakdown.Add(Row($"Quality:{name}", v, weight, qWeight > 0 ? weight * v / qWeight : 0, "kalite"));

        // ── OBSCURITY (radar-altılık) ──
        var obscurity = new List<(string name, double v, double w)>();
        void O(string name, double v, double weight) => obscurity.Add((name, Math.Clamp(v, 0, 1), weight));
        if (teamStrength is double ts) O("NotBigTeam", 1 - ts, w.NotBigTeam);
        O("LowVisibility", 1 - Math.Clamp(ctx.Discovery.DiscoveryScore / 100.0, 0, 1), w.LowVisibility);
        if (stakes is double st) O("LowStakes", 1 - st, w.LowStakes);
        O("NotPreferred", 1 - Math.Clamp(interestDelta / w.InterestScale, 0, 1), w.NotPreferred);

        var oWeight = obscurity.Sum(x => x.w);
        var obscurityFrac = oWeight > 0 ? obscurity.Sum(x => x.w * x.v) / oWeight : 0.0;
        foreach (var (name, v, weight) in obscurity)
            breakdown.Add(Row($"Obscurity:{name}", v, weight, oWeight > 0 ? weight * v / oWeight : 0, "obscurity"));

        var qualityScore = Math.Round(qualityFrac * 100.0, 4);
        var hiddenGemScore = Math.Round(100.0 * qualityFrac * obscurityFrac, 4);

        breakdown.Add(Row("QualityScore", qualityFrac, 0, qualityScore, "kalite özeti (0-100)"));
        breakdown.Add(Row("ObscurityFactor", obscurityFrac, 0, obscurityFrac, "obscurity özeti (0-1)"));

        // ── Gem kararı: kalite verisi VAR + TeamStrength verisi VAR + kalite tabanı + skor eşiği + büyük-takım-değil ──
        var hasQuality = quality.Count > 0;
        var isBigTeam = teamStrength is double t && t >= w.BigTeamHardCap;
        var isGem = hasQuality
                    && teamStrength is not null            // fame bilinmeden gem üretilmez (uydurma yok)
                    && !isBigTeam                          // büyük takım ASLA gem olamaz
                    && qualityFrac >= w.MinQuality         // yalnız düşük skor gem yapmaz
                    && hiddenGemScore >= w.MinHiddenGemScore;

        var reasons = BuildReasons(isGem, quality, obscurityFrac, teamStrength, ctx.Discovery.DiscoveryScore, isBigTeam, hasQuality);

        return new HiddenGemAnalysis
        {
            MatchId = ctx.MatchId,
            IsHiddenGem = isGem,
            HiddenGemScore = hiddenGemScore,
            QualityScore = qualityScore,
            Obscurity = Math.Round(obscurityFrac, 4),
            Reasons = reasons,
            Breakdown = breakdown
        };
    }

    private static List<string> BuildReasons(bool isGem, List<(string name, double v, double w)> quality,
        double obscurity, double? teamStrength, double discoveryScore, bool isBigTeam, bool hasQuality)
    {
        var r = new List<string>();
        if (!hasQuality) { r.Add("Yeterli kalite sinyali yok — analiz edilemedi"); return r; }
        if (isBigTeam) { r.Add("Büyük takım maçı — Hidden Gem değil (zaten görünür)"); return r; }
        if (teamStrength is null) { r.Add("Takım gücü bilinmiyor — gem üretilmedi"); return r; }

        if (isGem)
        {
            var top = quality.OrderByDescending(x => x.v).Take(3)
                .Select(x => $"{TrName(x.name)} {Pct(x.v)}");
            r.Add($"Yüksek izleme değeri: {string.Join(", ", top)}");
            r.Add($"Radar-altı: büyük takım değil, feed'de öne çıkmadı (görünürlük düşük, Discovery {discoveryScore.ToString("0", CultureInfo.InvariantCulture)})");
        }
        else
        {
            r.Add($"Hidden Gem değil (kalite/obscurity eşiği aşılmadı; obscurity {Pct(obscurity)})");
        }
        return r;
    }

    private static SignalContribution Row(string name, double value, double weight, double contribution, string note)
        => new() { Signal = name, Value = Math.Round(value, 4), Weight = weight, WeightedContribution = Math.Round(contribution, 4), DataAvailable = true, Note = note };

    private static string Pct(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    private static string TrName(string s) => s switch
    {
        "Competitiveness" => "rekabet",
        "GoalPotential" => "gol beklentisi",
        "Form" => "form",
        "H2HIntensity" => "H2H",
        "Confidence" => "güven",
        _ => s
    };
}
