using System.Collections.Generic;
using Formax.Application.DTOs.Matches;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// MAÇ VİDEOLARININ OKUMA SÖZLEŞMESİ — salt DB, SIFIR dış istek.
    ///
    /// Maç detayı bu uç üzerinden okur. Sayfa tıklaması hiçbir arama motoruna, YouTube'a
    /// veya sağlayıcıya çıkmaz: içerik daha önce arka plan turunda toplanıp doğrulanmıştır.
    /// Kayıt yoksa boş liste döner ve ekran TEK dürüst boş durum gösterir.
    /// </summary>
    public interface IMatchVideoReader
    {
        List<MatchVideoDto> GetVideos(int matchId, int max = 12);
    }
}
