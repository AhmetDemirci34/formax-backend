using System;

namespace Formax.Application.DTOs.Matches
{
    /// <summary>Sonuç kartındaki takım — ad + arma (arma yoksa null, uydurulmaz).</summary>
    public sealed class MatchResultTeamDto
    {
        public int TeamId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? LogoUrl { get; init; }
    }

    /// <summary>
    /// SONUÇ KARTI — "Maçlar → SONUÇLAR" sekmesinin tek satırı.
    ///
    /// NEDEN AYRI BİR DTO: <see cref="MatchListItemDto"/> "şimdi" merkezli yaklaşan-maç
    /// listesi içindir ve durumu SAATTEN türetir (kickoff geçmişse "Finished" der).
    /// Sonuç listesi bunu yapamaz: bir maç ertelenmiş, iptal olmuş ya da hâlâ oynanıyor
    /// olabilir. Burada durum DEPODAN gelir ve yalnız gerçekten bitmiş maçlar döner.
    /// Ayrıca kart, listede olmayan alanlara ihtiyaç duyar: fikstür kimliği, lig id'si,
    /// tur/aşama ve videonun gerçekten oynatılabilir olup olmadığı.
    ///
    /// Hiçbir alan türetilmez: skorlar, İY skorları ve tur adı depoda ne ise odur.
    /// </summary>
    public sealed class MatchResultItemDto
    {
        public int MatchId { get; init; }

        /// <summary>Sağlayıcı fikstür kimliği — tekilleştirmenin ve kimliğin dayanağı.</summary>
        public string? ExternalFixtureId { get; init; }

        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = string.Empty;

        /// <summary>Sağlayıcının HAM tur adı ("Play-offs", "Regular Season - 3").</summary>
        public string? Round { get; init; }

        /// <summary>Ham turdan türeyen Türkçe aşama etiketi ("Play-off Turu", "Lig Maçı").</summary>
        public string? MatchTypeLabel { get; init; }

        /// <summary>Kickoff (UTC). Türkiye saatine çevirme işi tek yerde, istemcide yapılır.</summary>
        public DateTime MatchDateUtc { get; init; }

        public MatchResultTeamDto HomeTeam { get; init; } = new();
        public MatchResultTeamDto AwayTeam { get; init; } = new();

        public int HomeScore { get; init; }
        public int AwayScore { get; init; }

        /// <summary>İlk yarı — sağlayıcı vermediyse null. "0-0" UYDURULMAZ.</summary>
        public int? HalfTimeHomeScore { get; init; }
        public int? HalfTimeAwayScore { get; init; }

        /// <summary>Depodaki gerçek durum (Finished).</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// Bu maçın uygulama İÇİNDE oynatılabilen doğrulanmış resmî videosu var mı?
        ///
        /// false ise kartta hiçbir video işareti gösterilmez. "Belki vardır" işareti
        /// koymak, kullanıcıyı boş bir ekrana göndermenin en kısa yoludur.
        /// </summary>
        public bool HasPlayableOfficialVideo { get; init; }
    }

    /// <summary>
    /// Sonuç bulunan bir Türkiye takvim günü. Tarih seçicinin "en yakın sonuçlu günü seç"
    /// kararı bu listeden verilir — 8 ayrı gün için 8 istek atılmaz.
    /// </summary>
    public sealed class MatchResultDayDto
    {
        /// <summary>Türkiye takvim günü, "yyyy-MM-dd".</summary>
        public string Date { get; init; } = string.Empty;
        public int MatchCount { get; init; }
    }
}
