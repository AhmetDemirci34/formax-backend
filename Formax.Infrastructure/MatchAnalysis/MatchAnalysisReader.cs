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
        private readonly Formax.Application.Interfaces.IMatchOutcomeSnapshotReader? _outcomes;

        public MatchAnalysisReader(FormaxDbContext db, Formax.Application.Interfaces.IMatchOutcomeSnapshotReader? outcomes = null)
        {
            _db = db;
            _outcomes = outcomes;
        }

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

            var dto = new MatchAnalysisDto
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
            if (_outcomes == null) return dto;

            // ÇELİŞKİ KAPISI — analiz, kartlarla AYNI güncel snapshot üzerinden süzülür (saf fonksiyon, dış istek yok).
            var snapshot = await _outcomes.GetCurrentAsync(matchId, ct);
            var names = await _db.Matches.AsNoTracking().Where(m => m.Id == matchId)
                .Select(m => new { Home = m.HomeTeam!.Name, Away = m.AwayTeam!.Name }).FirstOrDefaultAsync(ct);
            var checkedResult = Formax.Application.Services.Outcomes.AnalysisConsistencyValidator.Validate(dto, snapshot, names?.Home ?? string.Empty, names?.Away ?? string.Empty);
            var result = checkedResult.Analysis;
            result.SnapshotId = snapshot.SnapshotId;
            result.RemovedSentences = checkedResult.Violations.Count;
            return result;
        }
    }
}
