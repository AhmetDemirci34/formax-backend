using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.MatchAnalysis;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.MatchAnalysis
{
    /// <summary>
    /// Hazır analizi okur — TEK SATIR DB okuması. LLM, dış kaynak ya da üretim ÇAĞIRMAZ.
    /// Kanıt anahtarları kullanıcıya taşınmaz (iç doğrulama bilgisidir); yalnız cümle metinleri.
    /// </summary>
    public sealed class MatchAnalysisReader : IMatchAnalysisReader
    {
        private readonly FormaxDbContext _db;
        public MatchAnalysisReader(FormaxDbContext db) => _db = db;

        public async Task<MatchAnalysisDto> GetAsync(int matchId, CancellationToken ct = default)
        {
            var row = await _db.MatchAnalysisSnapshots.AsNoTracking()
                .Where(s => s.MatchId == matchId)
                .Select(s => new { s.Status, s.GeneratedAtUtc, s.ContentJson })
                .FirstOrDefaultAsync(ct);
            if (row == null) return new MatchAnalysisDto { Status = "Preparing" };
            if (row.Status != "Ready")
                return new MatchAnalysisDto { Status = row.Status == "InsufficientData" ? "InsufficientData" : "Unavailable", GeneratedAtUtc = row.GeneratedAtUtc };

            MatchAnalysisDocument? doc;
            try { doc = JsonSerializer.Deserialize<MatchAnalysisDocument>(row.ContentJson); }
            catch (JsonException) { doc = null; }
            if (doc == null) return new MatchAnalysisDto { Status = "Unavailable", GeneratedAtUtc = row.GeneratedAtUtc };

            return new MatchAnalysisDto
            {
                Status = "Ready",
                GeneratedAtUtc = row.GeneratedAtUtc,
                WhyWatch = doc.WhyWatch.Select(s => s.Text).ToList(),
                KeyBattle = doc.KeyBattle.Select(s => s.Text).ToList(),
                LineupImpact = doc.LineupImpact.Select(s => s.Text).ToList(),
                Uncertainty = doc.Uncertainty?.Text,
                Scenarios = doc.Scenarios.Select(r => new MatchAnalysisScenarioDto
                {
                    Market = r.Market, Support = r.Support?.Text, Risk = r.Risk?.Text
                }).ToList()
            };
        }
    }
}
