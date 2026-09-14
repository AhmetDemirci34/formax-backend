using System.Collections.Generic;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.PostMatch
{
    /// <summary>
    /// Bitmiş maçın kanonik olay ve istatistiklerini okur — SALT DB.
    ///
    /// Yazma yolu <see cref="PostMatchDataIngestionService"/>'tedir ve YALNIZ arka plan
    /// işinden çağrılır. Bu okuyucu hiçbir koşulda sağlayıcıya çıkmaz: kullanıcı maç
    /// detayını açtığında üretilen dış istek sayısı SIFIRDIR (ölçüldü 06.09.2026:
    /// 5 bitmiş maç detayı arka arkaya açıldı, günlük sayaç 62'de sabit kaldı).
    /// </summary>
    public sealed class PostMatchDataReader : IPostMatchDataReader
    {
        private readonly FormaxDbContext _db;

        public PostMatchDataReader(FormaxDbContext db) => _db = db;

        public IReadOnlyList<MatchEventRecord> GetEvents(int matchId)
            => _db.MatchEventRecords.AsNoTracking()
                .Where(e => e.MatchId == matchId)
                .OrderBy(e => e.Minute)
                .ThenBy(e => e.ExtraMinute)
                .ToList();

        public IReadOnlyList<MatchTeamStatistic> GetTeamStatistics(int matchId)
            => _db.MatchTeamStatistics.AsNoTracking()
                .Where(s => s.MatchId == matchId)
                .ToList();

        public MatchPostMatchSummary? GetSummary(int matchId)
            => _db.MatchPostMatchSummaries.AsNoTracking().FirstOrDefault(s => s.MatchId == matchId);

        public MatchVideoDiscoveryQueueItem? GetVideoDiscovery(int matchId)
            => _db.MatchVideoDiscoveryQueue.AsNoTracking().FirstOrDefault(q => q.MatchId == matchId);
    }
}
