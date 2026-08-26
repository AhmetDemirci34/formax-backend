using System.Collections.Generic;

namespace Formax.Infrastructure.Historical.Prediction.Ranking.Personalization;

/// <summary>
/// Bir kullanıcının bir maça olan ilgisi — MEVCUT User Interest / Match Affinity engine'lerinin çıktısından
/// türetilir (yeniden yazılmaz). AffinityScore = <c>MatchAffinityEngine</c> sonucu (0-100). InterestAgeDays =
/// en taze ilgi olayının yaşı (decay girdisi). Veri yoksa <see cref="HasData"/>=false → base skor kullanılır.
/// </summary>
public sealed record UserInterestSignal
{
    public required int UserId { get; init; }
    public required int MatchId { get; init; }

    /// <summary>Mevcut MatchAffinityEngine'den kullanıcı×maç ilgi skoru (0-100).</summary>
    public int AffinityScore { get; init; }

    // ── Affinity bileşenleri (breakdown şeffaflığı için; MatchAffinityDto'dan) ──
    public int TeamComponent { get; init; }
    public int LeagueComponent { get; init; }
    public int SignalComponent { get; init; }

    /// <summary>En taze ilgi olayının yaşı (gün) — Interest Decay girdisi.</summary>
    public double InterestAgeDays { get; init; }

    /// <summary>Kullanıcı ilgi verisi var mı. False → kişiselleştirme uygulanmaz (base skor).</summary>
    public bool HasData { get; init; }

    public static UserInterestSignal None(int userId, int matchId)
        => new() { UserId = userId, MatchId = matchId, HasData = false };
}
