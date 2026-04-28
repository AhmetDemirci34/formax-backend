using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Infrastructure.Repositories
{
    public class AiSpeakTelemetryReadRepository
        : IAiSpeakTelemetryReadRepository
    {
        private readonly FormaxDbContext _db;

        public AiSpeakTelemetryReadRepository(FormaxDbContext db)
        {
            _db = db;
        }

        public IReadOnlyList<AiSpeakTelemetry> GetAll()
        {
            return _db.AiSpeakTelemetries
                .AsNoTracking()
                .ToList();
        }
    }
}
