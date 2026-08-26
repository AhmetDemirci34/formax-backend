using Formax.Application.DTOs.Matches;
using Formax.Application.Services.Matches;

namespace Formax.Application.Services.MatchIntelligence.Outlook;

/// <summary>
/// VERİ YOK ≠ SIFIR — Outlook analizörlerinin ortak kuralı.
///
/// Ölçüldü (Atletico Madrid–Malaga, 19.08.2026): Malaga'nın kapsam içi HİÇ maçı yokken bu
/// yüzey "Deplasman performansı 0/100", "momentum Atletico Madrid tarafında", "Tempo düşük"
/// ve "dar bir Atletico Madrid üstünlüğü" üretiyordu. Hiçbiri ölçüm değildi: FormScore=0,
/// AvgGoalsFor=0 gibi BOŞ değerler gerçek bir performans gibi işleniyordu.
///
/// Eşik burada tanımlı değildir; tüm yorum yüzeyleriyle ortak kaynaktan
/// (<see cref="FormEvidencePolicy"/>) okunur. Yeterli kanıt yoksa analizör SAYI ÜRETMEZ,
/// null döner; anlatı ve evidence katmanı o kalemi hiç göstermez.
/// </summary>
internal static class OutlookEvidence
{
    /// <summary>Bu takımın metrikleri yorumlanabilir mi?</summary>
    public static bool Enough(TeamComparisonDto? c)
        => c != null && c.SampleCount >= FormEvidencePolicy.MinSample;

    /// <summary>Karşılaştırmalı kalem (tempo/denge/gol eğilimi) için İKİ taraf da gerekir.</summary>
    public static bool Both(TeamComparisonDto? home, TeamComparisonDto? away)
        => Enough(home) && Enough(away);
}

/// <summary>Son maç formu (Görev #016).</summary>
public static class FormAnalyzer
{
    public static List<string> Recent(List<LastMatchDto>? matches, int take = 5)
        => (matches ?? new()).Take(take).Select(m => m.Result).ToList();

    /// <summary>
    /// Son N maç puanı (W=3, D=1, L=0) — momentum için. Maç YOKSA null döner: sıfır puan
    /// "kötü form" değil, "bilgi yok" demektir ve karşılaştırmaya sokulamaz.
    /// </summary>
    public static int? Points(List<LastMatchDto>? matches, int take = 5)
    {
        var list = (matches ?? new()).Take(take).ToList();
        if (list.Count < FormEvidencePolicy.MinSample) return null;
        return list.Sum(m => m.Result == "W" ? 3 : m.Result == "D" ? 1 : 0);
    }
}

/// <summary>Takım performansı: FormScore'dan 0–100 skor + iç saha/deplasman etiketi.</summary>
public static class PerformanceAnalyzer
{
    /// <summary>Yeterli maç yoksa null — "0/100" diye bir performans YOKTUR.</summary>
    public static int? Score(TeamComparisonDto c)
        => !OutlookEvidence.Enough(c)
            ? null
            : Math.Clamp((int)Math.Round(c.FormScore / 30.0 * 100), 0, 100);

    /// <summary>Yeterli maç yoksa boş etiket — UI o satırı hiç göstermez.</summary>
    public static string Label(TeamComparisonDto c, bool isHome)
    {
        var s = Score(c);
        if (s == null) return "";

        var venue = isHome ? "İç sahada" : "Deplasmanda";
        var q = s >= 67 ? "güçlü" : s >= 40 ? "dengeli" : "zayıf";
        return $"{venue} {q}";
    }
}

/// <summary>Gol eğilimi: karşılıklı gol oranı + toplam gol beklentisi.</summary>
public static class GoalTrendAnalyzer
{
    /// <summary>İki tarafta da yeterli örneklem yoksa eğilim ÜRETİLMEZ (boş döner).</summary>
    public static string Trend(TeamComparisonDto home, TeamComparisonDto away, H2HDto? h2h)
    {
        if (!OutlookEvidence.Both(home, away)) return "";

        var kg = Math.Clamp((home.GoalScoringRate + away.GoalScoringRate) / 2, 0, 100);
        var avgTotal = home.AvgGoalsFor + away.AvgGoalsFor;
        var over = avgTotal >= 2.5 ? " · Üst 2.5 eğilimi" : "";
        return $"KG %{kg}{over}";
    }
}

/// <summary>Maç temposu: toplam gol ortalamasından.</summary>
public static class TempoAnalyzer
{
    /// <summary>
    /// Eksik tarafın AvgGoalsFor=0 değeri toplamı aşağı çekip her maçı "Düşük tempo"
    /// gösteriyordu (ölçüldü: Malaga). Kanıt yoksa tempo BİLİNMEZ.
    /// </summary>
    public static string Tempo(TeamComparisonDto home, TeamComparisonDto away)
    {
        if (!OutlookEvidence.Both(home, away)) return "";

        var t = home.AvgGoalsFor + away.AvgGoalsFor;
        return t >= 2.7 ? "Yüksek" : t >= 2.0 ? "Orta" : "Düşük";
    }
}

/// <summary>Oyun dengesi: form + lig sırası farkından (0 ev ↔ 100 deplasman).</summary>
public static class BalanceAnalyzer
{
    /// <summary>
    /// Veri olmayan tarafın FormScore=0 değeri, rakip lehine sahte bir "üstünlük" üretiyordu.
    /// İki tarafta da yeterli örneklem yoksa denge HESAPLANMAZ (null) ve üstünlük iddia edilmez.
    /// </summary>
    public static int? Balance(TeamComparisonDto home, TeamComparisonDto away)
    {
        if (!OutlookEvidence.Both(home, away)) return null;

        var formGap = home.FormScore - away.FormScore; // + = ev daha güçlü
        var rankGap = 0;
        if (home.LeagueRank > 0 && away.LeagueRank > 0)
            rankGap = away.LeagueRank - home.LeagueRank; // + = ev daha iyi (düşük sıra)
        var lean = formGap * 2.5 + rankGap * 1.5;       // + = ev lehine
        return Math.Clamp((int)Math.Round(50 - lean), 0, 100);
    }
}

/// <summary>Momentum: son maç puanlarından hangi tarafın yükselişte olduğu.</summary>
public static class MomentumAnalyzer
{
    /// <summary>
    /// Bir tarafın puanı YOKSA (maç yok) karşılaştırma yapılamaz → "unknown".
    /// Eskiden eksik taraf 0 puan sayılıyor ve momentum otomatik rakibe yazılıyordu.
    /// </summary>
    public static string Momentum(List<LastMatchDto>? homeLast, List<LastMatchDto>? awayLast)
    {
        var h = FormAnalyzer.Points(homeLast);
        var a = FormAnalyzer.Points(awayLast);
        if (h == null || a == null) return "unknown";

        var diff = h.Value - a.Value;
        return diff >= 3 ? "home" : diff <= -3 ? "away" : "balanced";
    }
}
