using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>
    /// MAÇ VİDEOSU KARTI.
    ///
    /// <see cref="CanPlayInApp"/> false ise uygulama içinde oynatıcı AÇILMAZ ve
    /// <see cref="EmbedUrl"/> null gelir — ekranın "belki denerim" diyebileceği bir alan
    /// bırakılmaz. Böyle bir kayıt sahte/boş player olarak değil, "uygulama içinde
    /// oynatılamıyor" diyen dürüst bir satır olarak gösterilir.
    /// </summary>
    public sealed class MatchVideoDto
    {
        public string Title { get; init; } = string.Empty;

        /// <summary>Resmî yayıncı adı ("UEFA", "TRT SPOR"…). Ekranda kaynak olarak yazar.</summary>
        public string Publisher { get; init; } = string.Empty;

        /// <summary>Kaynağın kendi sayfası — kullanıcı dışarıda açmak isterse.</summary>
        public string SourcePageUrl { get; init; } = string.Empty;

        /// <summary>Yalnız oynatılabilir kayıtlarda dolu.</summary>
        public string? EmbedUrl { get; init; }

        public string? ThumbnailUrl { get; init; }

        /// <summary>MatchHighlights | ExtendedHighlights | Goal | Penalty | RedCard | VAR | ImportantMoment.</summary>
        public string VideoType { get; init; } = string.Empty;

        public DateTime? PublishedAtUtc { get; init; }

        /// <summary>Süre (sn). Kaynak vermediyse null — "0:00" uydurulmaz.</summary>
        public int? DurationSeconds { get; init; }

        /// <summary>Resmî kaynak + embed izni + gömme adresi birlikte varsa true.</summary>
        public bool CanPlayInApp { get; init; }

        /// <summary>
        /// Videonun açık olduğu ülkeler (ISO alpha-2). Kaynak söylemediyse boş —
        /// "her yerde açık" varsayılmaz, yalnız bilinen kısıt taşınır.
        /// </summary>
        public IReadOnlyList<string> AvailableCountries { get; init; } = Array.Empty<string>();

        /// <summary>
        /// true ise video yalnız belirli ülkelerde oynar. Ekran bunu SONSUZ SPINNER
        /// yerine açık bir cümleyle söyler.
        /// </summary>
        public bool IsRegionRestricted { get; init; }

        // ── Olay klibi meta verisi — yalnız AYRI kliplerde dolu ────────────────
        public int? EventMinute { get; init; }
        public int? EventExtraMinute { get; init; }
        public string? EventPlayer { get; init; }
        public string? EventTeam { get; init; }
    }
}
