using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    /// <summary>
    /// BİTMİŞ MAÇ VERİSİ — SALT OKUMA.
    ///
    /// Arka plan işinin kanonik tablolara yazdığı olay ve istatistikleri okur.
    /// Bu arayüzün hiçbir üyesi sağlayıcıya çıkmaz; maç detayına tıklamak
    /// api-football'a İSTEK ÜRETMEZ.
    /// </summary>
    public interface IPostMatchDataReader
    {
        /// <summary>Maçın kanonik olayları (dakika sırasıyla). Yoksa boş liste.</summary>
        IReadOnlyList<MatchEventRecord> GetEvents(int matchId);

        /// <summary>Maçın kanonik takım istatistikleri (ev + deplasman). Yoksa boş liste.</summary>
        IReadOnlyList<MatchTeamStatistic> GetTeamStatistics(int matchId);
    }
}
