using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Follow
{
    /// <summary>
    /// "TAKİP ETTİĞİM MAÇLAR" ekranının tam yanıtı — iki sade bölüm.
    ///
    /// Bu sözleşmede takım kartı, lig kartı, istatistik kutusu, gelişme akışı veya
    /// takip EDİLMEYEN hiçbir içerik YOKTUR. Ekran yalnız kullanıcının gerçekten
    /// takip ettiği maçları gösterir; başka bir şey gösteremesin diye DTO da başka
    /// bir şey taşımaz.
    /// </summary>
    public sealed class FollowedMatchesScreenDto
    {
        /// <summary>Henüz oynanmamış takipler — en yakın maç önce.</summary>
        public List<FollowedMatchCardDto> Upcoming { get; set; } = new();

        /// <summary>Tamamlanmış takipler — en yeni maç önce.</summary>
        public List<FollowedMatchCardDto> Finished { get; set; } = new();

        /// <summary>Toplam takip edilen maç sayısı (boş durum kararı için).</summary>
        public int TotalCount => Upcoming.Count + Finished.Count;
    }

    /// <summary>Takip kartı — tıklandığında <c>/match/{matchId}</c> açılır.</summary>
    public sealed class FollowedMatchCardDto
    {
        public int MatchId { get; set; }
        public int LeagueId { get; set; }
        public string League { get; set; } = string.Empty;

        /// <summary>Kickoff (UTC). Geri sayımı arayüz bundan hesaplar.</summary>
        public DateTime MatchDateUtc { get; set; }

        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string? HomeTeamLogoUrl { get; set; }
        public string? AwayTeamLogoUrl { get; set; }

        /// <summary>DEPODAKİ gerçek durum — saatten türetilmez.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Yalnız bitmiş maçta dolu; aksi hâlde null (0-0 uydurulmaz).</summary>
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public int? HalfTimeHomeScore { get; set; }
        public int? HalfTimeAwayScore { get; set; }
    }
}
