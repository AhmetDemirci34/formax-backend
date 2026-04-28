using Formax.Domain.Entities;
using System.Linq;

namespace Formax.Application.Interfaces
{
    public interface IMatchOynanmaSnapshotReadRepository
    {
        IQueryable<MatchOynanmaSnapshot> Query();

        /// <summary>
        /// Maç tarihinden önceki en yakın snapshot (yoksa null).
        /// </summary>
        MatchOynanmaSnapshot? GetLatestBeforeMatchUtc(int matchId, DateTime matchUtc);

        /// <summary>
        /// FAZ 1.5 — PreMatchFinal
        /// Maç tarihinden önceki en yakın Source=PreMatchFinal snapshot (yoksa null).
        /// </summary>
        MatchOynanmaSnapshot? GetPreMatchFinalBeforeMatchUtc(int matchId, DateTime matchUtc);
    }
}
