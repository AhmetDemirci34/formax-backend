using System;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Signals;

/// <summary>
/// FAZ-1: Oynanma Skoru Matematiği (MVP sürümü)
///
/// Notlar:
/// - Bahis/oran/yüzde yok.
/// - Bu servis "karar karmaşıklığı / belirsizlik" bandı üretir.
/// - Mevcut veri seti (MatchListItemDto) ile çalışacak şekilde tasarlanmıştır.
/// - Daha ileri fazlarda (form, kadro, motivasyon, world perception) sinyalleri eklendikçe
///   skor bileşenleri genişletilecektir.
/// </summary>
public sealed class PlayedScoreCalculator : IPlayedScoreCalculator
{
    public PlayedScoreResult CalculateForListItem(MatchListItemDto match)
    {
        // Güvenli default
        if (match == null)
            return new PlayedScoreResult { RawScore = 0, Band = "Low", Label = "Görece net maç" };

        var now = DateTime.UtcNow;

        // --------------------------------------------------
        // 1) Zaman Yakınlığı (0-25)
        // --------------------------------------------------
        var hoursToKickoff = (match.StartTime.ToUniversalTime() - now).TotalHours;
        var proximity = hoursToKickoff switch
        {
            <= -2 => 10,          // maç geçmişte ama listede varsa
            <= 0 => 18,           // başlıyor / yeni başladı
            <= 3 => 25,           // çok yakın
            <= 12 => 15,
            <= 24 => 8,
            <= 48 => 4,
            _ => 2
        };

        // --------------------------------------------------
        // 2) Canlılık & Gerilim (0-35)
        // --------------------------------------------------
        int liveTension = 0;

        var status = (match.Status ?? string.Empty).Trim();

        var isLive = status.Equals("Live", StringComparison.OrdinalIgnoreCase);
        var isFinished = status.Equals("Finished", StringComparison.OrdinalIgnoreCase)
                         || status.Equals("FT", StringComparison.OrdinalIgnoreCase);

        if (isLive)
        {
            liveTension += 18;

            var home = match.Score?.Home ?? 0;
            var away = match.Score?.Away ?? 0;
            var diff = Math.Abs(home - away);
            var totalGoals = home + away;

            // Skor yakınsa gerilim artar
            liveTension += diff switch
            {
                0 => 12,
                1 => 9,
                2 => 5,
                _ => 2
            };

            // Dakika bilgisi varsa son bölüm bonusu
            if (match.Minute.HasValue)
            {
                var m = match.Minute.Value;

                if (m >= 70 && diff <= 1)
                    liveTension += 6;

                // Geç dakikada 0-0 ise karar karmaşıklığı artar
                if (m >= 60 && totalGoals == 0)
                    liveTension += 4;
            }

            // Gol sayısı arttıkça oynaklık (hafif)
            if (totalGoals >= 3)
                liveTension += 3;
        }
        else if (!isFinished)
        {
            // NotStarted / Scheduled
            liveTension += 8;
        }

        // --------------------------------------------------
        // 3) Lig/derbi gibi bağlamsal sinyal (şimdilik basit) (0-15)
        // --------------------------------------------------
        // Bu fazda external veri yok; sadece bazı anahtar kelimelerle "yüksek ilgi" bandı.
        int contextHint = 0;
        var league = (match.League ?? string.Empty).ToLowerInvariant();
        if (league.Contains("derbi") || league.Contains("final") || league.Contains("play-off") || league.Contains("playoff"))
            contextHint = 12;
        else if (!string.IsNullOrWhiteSpace(match.League))
            contextHint = 4;

        // --------------------------------------------------
        // Toplam (0-100) + clamp
        // --------------------------------------------------
        var raw = proximity + liveTension + contextHint;
        raw = Math.Clamp(raw, 0, 100);

        var (band, label) = ToBandAndLabel(raw);

        return new PlayedScoreResult
        {
            RawScore = raw,
            Band = band,
            Label = label
        };
    }

    private static (string Band, string Label) ToBandAndLabel(int raw)
    {
        // UI: sayısal kesinlik yok. Band + Label.
        return raw switch
        {
            <= 30 => ("Low", "Görece net maç"),
            <= 55 => ("Medium", "Denge var"),
            <= 75 => ("High", "Karar zor"),
            _ => ("VeryHigh", "Yüksek belirsizlik")
        };
    }
}
