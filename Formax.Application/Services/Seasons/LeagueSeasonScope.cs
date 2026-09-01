using System;

namespace Formax.Application.Services.Seasons
{
    /// <summary>
    /// BİR LİGİN BİR SEZONUNUN KESİN ZAMAN KAPSAMI.
    ///
    /// Form ve puan durumu hesapları bu kapsamın DIŞINA çıkamaz: "bu sezon" ifadesi
    /// yalnız [StartUtc, EndUtc) aralığındaki, aynı ligin tamamlanmış maçlarını anlatır.
    ///
    /// StartUtc YALNIZ doğrulanmış sezon metadata kaydından gelir (LeagueSeasons tablosu
    /// veya açık yapılandırma). Depodaki ilk fikstür tarihi resmî başlangıç DEĞİLDİR;
    /// yalnız <see cref="DiagnosticFirstFixtureUtc"/> alanında teşhis amaçlı taşınır.
    /// </summary>
    /// <param name="LeagueId">Canonical lig id (Match.LeagueId).</param>
    /// <param name="SeasonYear">Sezon başlangıç yılı (2026 = 2026/27).</param>
    /// <param name="StartUtc">Sezonun RESMÎ başlangıcı (metadata).</param>
    /// <param name="EndUtc">Kapsam penceresinin sonu (dahil değil).</param>
    /// <param name="Source">Başlangıcın kaynağı: "SeasonMetadata" | "Configuration".</param>
    /// <param name="DiagnosticFirstFixtureUtc">Depodaki ilk fikstür — YALNIZ teşhis.</param>
    public sealed record LeagueSeasonScope(
        int LeagueId,
        int SeasonYear,
        DateTime StartUtc,
        DateTime EndUtc,
        string Source,
        DateTime? DiagnosticFirstFixtureUtc = null)
    {
        /// <summary>"2026/27" — kullanıcıya gösterilen sezon etiketi.</summary>
        public string Label => $"{SeasonYear}/{(SeasonYear + 1) % 100:00}";

        public bool Contains(DateTime utc) => utc >= StartUtc && utc < EndUtc;

        /// <summary>
        /// Metadata başlangıcı ile depodaki ilk fikstür arasındaki fark (gün). Teşhis
        /// amaçlıdır: büyük fark, fikstür verisinin eksik/kaymış olduğunu gösterir.
        /// </summary>
        public int? FirstFixtureDriftDays => DiagnosticFirstFixtureUtc.HasValue
            ? (int)Math.Round((DiagnosticFirstFixtureUtc.Value.Date - StartUtc.Date).TotalDays)
            : null;
    }

    /// <summary>Sezon çözülemediğinde nedeni taşır (sessiz tahmin YOK).</summary>
    public sealed record LeagueSeasonResolution(LeagueSeasonScope? Scope, string? Error)
    {
        public bool Resolved => Scope != null;

        public static LeagueSeasonResolution Ok(LeagueSeasonScope scope) => new(scope, null);
        public static LeagueSeasonResolution Fail(string error) => new(null, error);
    }
}
