using Formax.Application.Interfaces;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Formax.Infrastructure.Repositories
{
    public class AiSpeakTelemetryRepository : IAiSpeakTelemetryRepository
    {
        private readonly FormaxDbContext _db;

        public AiSpeakTelemetryRepository(FormaxDbContext db)
        {
            _db = db;
        }

        public void Add(AiSpeakTelemetry telemetry)
        {
            _db.AiSpeakTelemetries.Add(telemetry);
            _db.SaveChanges();
        }

        // 🔒 FAZ-15 / ADIM-2.5 — ADMIN METRICS
        public async Task<List<AiSpeakTelemetry>> GetAllAsync()
        {
            return await _db.AiSpeakTelemetries
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
