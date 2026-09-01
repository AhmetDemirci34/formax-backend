using System;
using Formax.Application.Services.Seasons;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// LİG SEZONU ÇÖZÜCÜSÜ — "bu sezon" ne demek sorusunun TEK cevabı.
    ///
    /// KAYNAK ÖNCELİĞİ (isim/yıl TAHMİNİ YAPILMAZ):
    ///   1) Açık yapılandırma — LeagueSeasons:{leagueId}:{seasonYear}:Start (ISO tarih).
    ///   2) GERÇEK VERİ — ligin sezon penceresindeki İLK maçının tarihi (Matches).
    /// Hiçbiri yoksa çözüm BAŞARISIZDIR: null + açık hata döner, uydurma tarih üretilmez.
    ///
    /// Sezon penceresi (Temmuz kırılımı) projede zaten kullanılan sözleşmedir
    /// (WorldPerceptionDailyJob.ResolveSeasonYear, HistoricalFeatureService.SeasonStart);
    /// burada TEK yere toplanmıştır. Kapsamdaki 11 lig Avrupa takvimlidir.
    /// </summary>
    public interface ILeagueSeasonResolver
    {
        /// <summary>Referans tarihin düştüğü sezonu çözer.</summary>
        LeagueSeasonResolution Resolve(int leagueId, DateTime referenceUtc);

        /// <summary>Sezon başlangıç yılı — Temmuz kırılımı (projedeki mevcut sözleşme).</summary>
        int SeasonYearOf(DateTime utc);
    }
}
