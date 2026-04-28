using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Formax.Domain.Entities;

namespace Formax.Application.Interfaces.Repositories
{
    public interface ILastExtendedContextKeyRepository
    {
        LastExtendedContextKey? GetByMatchId(int matchId);

        void Upsert(LastExtendedContextKey entity);
    }
}
