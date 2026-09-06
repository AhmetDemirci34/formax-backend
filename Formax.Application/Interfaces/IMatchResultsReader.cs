using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Matches;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// SONUÇ LİSTESİNİN OKUMA SÖZLEŞMESİ — salt DB, SIFIR dış istek.
    ///
    /// "Maçlar → SONUÇLAR" sekmesi bu uçtan beslenir. Sekmeye geçmek, gün değiştirmek
    /// veya karta basmak HİÇBİR sağlayıcıya (api-football dâhil) istek üretmez: veriler
    /// zaten fikstür senkronunun yazdığı depodadır.
    /// </summary>
    public interface IMatchResultsReader
    {
        /// <summary>
        /// Bir Türkiye takvim gününün bitmiş maçları — kilitli 11 organizasyon,
        /// tekilleştirilmiş ve kickoff'a göre deterministik sıralı.
        /// </summary>
        Task<List<MatchResultItemDto>> GetResultsAsync(DateOnly istanbulDay, CancellationToken ct = default);

        /// <summary>
        /// Son <paramref name="days"/> Türkiye takvim günü içinde SONUÇ BULUNAN günler
        /// (bugün dâhil, yeniden eskiye). Tarih seçici "en yakın sonuçlu gün"ü buradan seçer.
        /// </summary>
        Task<List<MatchResultDayDto>> GetRecentResultDaysAsync(int days, CancellationToken ct = default);

        /// <summary>
        /// TAKIM ARAMASI — "Takım ara…" kutusunun veri kaynağı.
        ///
        /// Salt DB'den okur, sağlayıcıya ÇIKMAZ. Aramak api-football kotası harcamaz.
        /// Kapsam: kilitli 11 organizasyon.
        /// <paramref name="scope"/> upcoming → Status=NotStarted; finished → Status=Finished.
        /// </summary>
        /// <param name="term">Arama terimi — en az 2 karakter, önceden kırpılmış.</param>
        /// <param name="scope">upcoming veya finished.</param>
        /// <param name="maxResults">Döndürülecek azami sonuç sayısı.</param>
        Task<List<MatchResultItemDto>> SearchByTeamAsync(
            string term, string scope, int maxResults = 100, CancellationToken ct = default);
    }
}
