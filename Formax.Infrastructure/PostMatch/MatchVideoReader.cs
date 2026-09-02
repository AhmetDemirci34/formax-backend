using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// MAÇ VİDEOLARININ OKUMA YOLU — SALT DB.
    ///
    /// Bu sınıf HİÇBİR dış istek üretmez: kullanıcı maç detayına tıkladığında YouTube'a,
    /// arama motoruna veya sağlayıcıya çıkılmaz. Videolar arka planda
    /// <see cref="MatchVideoRegistrar"/> kapısından geçip DB'ye yazılmıştır.
    ///
    /// SIRALAMA ÜRÜN KARARIDIR: önce oynatılabilen kayıtlar, sonra ana özet türleri,
    /// en sonda yayın anı. Ekranın ilk kartı böylece her zaman gerçekten OYNAYAN
    /// bir maç özeti olur; oynatılamayan kaynak listeyi başa tıkamaz.
    /// </summary>
    public sealed class MatchVideoReader : IMatchVideoReader
    {
        private readonly FormaxDbContext _db;

        public MatchVideoReader(FormaxDbContext db) => _db = db;

        public List<MatchVideoDto> GetVideos(int matchId, int max = 12)
            => _db.MatchVideos.AsNoTracking()
                   .Where(v => v.MatchId == matchId)
                   .ToList()
                   .OrderByDescending(v => v.CanPlayInApp)
                   .ThenByDescending(v => MatchVideoTypes.IsMainHighlight(v.VideoType))
                   .ThenByDescending(v => v.PublishedAtUtc)
                   .Take(max)
                   .Select(v => new MatchVideoDto
                   {
                       Title           = v.Title,
                       Publisher       = v.OfficialPublisher,
                       SourcePageUrl   = v.SourcePageUrl,
                       // Oynatılamayan kayda gömme adresi TAŞINMAZ: ekranın elinde
                       // deneyebileceği bir adres kalmasın.
                       EmbedUrl        = v.CanPlayInApp ? v.EmbedUrl : null,
                       ThumbnailUrl    = v.ThumbnailUrl,
                       VideoType       = v.VideoType,
                       PublishedAtUtc  = v.PublishedAtUtc,
                       DurationSeconds = v.DurationSeconds,
                       CanPlayInApp    = v.CanPlayInApp,
                       // Bölgesel kısıt DTO'ya TAŞINIR: ekran "oynamıyor" ile
                       // "senin bölgende oynamıyor"u ayırt edebilsin.
                       AvailableCountries = SplitCountries(v.AvailableCountries),
                       IsRegionRestricted = v.IsRegionRestricted,
                       EventMinute      = v.EventMinute,
                       EventExtraMinute = v.EventExtraMinute,
                       EventPlayer      = v.EventPlayer,
                       EventTeam        = v.EventTeam
                   })
                   .ToList();

        /// <summary>"TR,DE" → ["TR","DE"]. Boşsa boş liste (null "her yer" demek değil).</summary>
        private static IReadOnlyList<string> SplitCountries(string? raw)
            => string.IsNullOrWhiteSpace(raw)
                ? Array.Empty<string>()
                : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
