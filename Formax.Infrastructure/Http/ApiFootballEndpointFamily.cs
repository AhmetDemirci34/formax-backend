using System;

namespace Formax.Infrastructure.Http
{
    /// <summary>
    /// api-football istek yolunu endpoint ailesine indirger (ör. "fixtures", "fixtures/lineups",
    /// "teams/statistics", "standings"). Query string ATILIR — bu yüzden <c>injuries?team=</c> ile
    /// <c>injuries?fixture=</c> aynı "injuries" ailesine düşer; job ayrımı
    /// <see cref="Telemetry.ApiFootballCallScope"/> ile yapılır.
    ///
    /// Mantık <see cref="ApiFootballMeteringHandler"/> içindeki özel metottan BİREBİR taşındı
    /// (davranış değişikliği YOK); artık job-attribution handler'ı da aynı sınıflandırmayı
    /// kullanıyor → iki sayaç aynı aile adlarını üretir.
    /// </summary>
    public static class ApiFootballEndpointFamily
    {
        public static string Classify(Uri? uri)
        {
            if (uri == null) return "unknown";
            var path = uri.AbsolutePath.Trim('/');
            if (string.IsNullOrWhiteSpace(path)) return "root";

            var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // İki segmentli bilinen aileler ("fixtures/lineups", "fixtures/statistics",
            // "fixtures/events", "fixtures/headtohead", "teams/statistics", "players/squads")
            if (segs.Length >= 2 &&
                (segs[0] is "fixtures" or "teams" or "players"))
                return $"{segs[0]}/{segs[1]}";
            return segs[0];
        }
    }
}
