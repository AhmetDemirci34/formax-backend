using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Entities;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>
    /// MAÇ OLAYI — "Önemli Anlar" zaman çizelgesinin tek satırı.
    ///
    /// Kaynak: <see cref="MatchLiveEvent"/> (depodaki GERÇEK olay kaydı). Kaynakta
    /// olmayan hiçbir olay üretilmez; alanlar yalnız doluysa taşınır.
    /// </summary>
    public sealed class MatchEventDto
    {
        public int Minute { get; init; }
        /// <summary>"90+3" gibi uzatma dakikası — kaynakta ayrı alan yoksa null.</summary>
        public int? ExtraMinute { get; init; }
        public string? Team { get; init; }
        public string? Player { get; init; }
        public string? Assist { get; init; }
        /// <summary>Goal / Card / Subst / Var … (sağlayıcının ham türü).</summary>
        public string EventType { get; init; } = string.Empty;
        /// <summary>"Normal Goal", "Yellow Card" … kaynaktaki açıklama.</summary>
        public string? Detail { get; init; }

        /// <summary>
        /// Depodaki olayları kronolojik, TEKİLLEŞTİRİLMİŞ bir listeye çevirir.
        ///
        /// TEKİLLEŞTİRME: aynı dakika + tür + takım + oyuncu + açıklama birleşimi bir
        /// kez görünür. Canlı alım tekrarları (aynı olayın iki turda yazılması) zaman
        /// çizelgesinde çift satır üretmemelidir.
        /// </summary>
        public static List<MatchEventDto> FromEvents(IEnumerable<MatchLiveEvent>? events)
        {
            if (events == null) return new List<MatchEventDto>();

            return events
                .Where(e => e != null)
                .GroupBy(e => new
                {
                    e.Minute,
                    Type = (e.EventType ?? string.Empty).Trim().ToLowerInvariant(),
                    Team = (e.Team ?? string.Empty).Trim().ToLowerInvariant(),
                    Player = (e.Player ?? string.Empty).Trim().ToLowerInvariant(),
                    Detail = (e.Detail ?? string.Empty).Trim().ToLowerInvariant()
                })
                .Select(g => g.First())
                .OrderBy(e => e.Minute)
                .ThenBy(e => e.EventType ?? string.Empty, StringComparer.Ordinal)
                .Select(e => new MatchEventDto
                {
                    Minute    = e.Minute,
                    Team      = string.IsNullOrWhiteSpace(e.Team) ? null : e.Team,
                    Player    = string.IsNullOrWhiteSpace(e.Player) ? null : e.Player,
                    EventType = e.EventType ?? string.Empty,
                    Detail    = string.IsNullOrWhiteSpace(e.Detail) ? null : e.Detail
                })
                .ToList();
        }
    }
}
