using Formax.Application.DTOs.MatchIntelligence;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.Services.MatchIntelligence.Lineup;

/// <summary>
/// Expected Lineup Engine implementasyonu (Görev #013).
///
///   Predict            = GetOfficial ?? PredictFromHistory
///   GetOfficial        = resmi açıklanan 11 (yayınlanmadıysa null)
///   PredictFromHistory = son N maçın başlayan 11'lerinden recency-ağırlıklı tahmin
///
/// Tüm hesaplama backend'de; frontend hiçbir tahmin üretmez. Uydurma yok — yalnız gerçek veri.
/// </summary>
public sealed class ExpectedLineupEngine : IExpectedLineupEngine
{
    private const int RecentWindow = 6;

    private readonly IMatchLineupRepository _lineupRepo;
    private readonly IMatchPlayerStatusRepository _statusRepo;
    private readonly IMatchReadRepository _matchRepo;

    public ExpectedLineupEngine(
        IMatchLineupRepository lineupRepo,
        IMatchPlayerStatusRepository statusRepo,
        IMatchReadRepository matchRepo)
    {
        _lineupRepo = lineupRepo;
        _statusRepo = statusRepo;
        _matchRepo  = matchRepo;
    }

    public ExpectedLineupDto Predict(int matchId, int teamId, string teamName, string side)
        => GetOfficial(matchId, teamName, side) ?? PredictFromHistory(matchId, teamId, teamName, side);

    // ── Resmi açıklanan 11 ─────────────────────────────────────────────────────
    public ExpectedLineupDto? GetOfficial(int matchId, string teamName, string side)
    {
        var header = _lineupRepo.GetByMatchId(matchId);
        var released = side == "Home"
            ? header?.HomeLineupsReleased == true
            : header?.AwayLineupsReleased == true;

        var announced = _lineupRepo.GetPlayersByMatchId(matchId)
            .Where(p => p.Side == side && p.Role == "Starter")
            .ToList();

        if (!released || announced.Count < 7) return null;

        var formation = InferFormation(announced);
        var players = OrderByLine(announced).Select(p => new ExpectedPlayerDto
        {
            Number = p.ShirtNumber, Name = p.PlayerName, Position = p.Position,
            Role = FormationBuilder.RoleLabel(p.Position), Confidence = 95
        }).ToList();

        return new ExpectedLineupDto
        {
            TeamName = teamName, Formation = formation, Players = players,
            Confidence = FormationConfidenceCalculator.Team("Official", 0, players.Count, 0),
            Source = "Official", Reasons = new() { "Resmi kadro açıklandı." }
        };
    }

    // ── Geçmişten tahmin ───────────────────────────────────────────────────────
    public ExpectedLineupDto PredictFromHistory(int matchId, int teamId, string teamName, string side)
    {
        var (unavailable, unavailableDtos, statuses) = GetUnavailable(matchId, teamId);

        var recent = _matchRepo.GetRecentMatchesForTeam(teamId, RecentWindow);
        var snapshots = new List<IReadOnlyList<MatchLineupPlayer>>();
        foreach (var m in recent)
        {
            if (m.Id == matchId) continue; // mevcut maçın kendi (resmi) kadrosu "beklenen geçmiş" sayılmaz
            var pastSide = m.HomeTeamId == teamId ? "Home" : "Away";
            var starters = _lineupRepo.GetPlayersByMatchId(m.Id)
                .Where(p => p.Side == pastSide && p.Role == "Starter")
                .ToList();
            if (starters.Count >= 7) snapshots.Add(starters);
        }

        if (snapshots.Count == 0)
        {
            return new ExpectedLineupDto
            {
                TeamName = teamName, Formation = "", Players = new(), Confidence = 0,
                UnavailablePlayers = unavailableDtos, Source = "Insufficient",
                Reasons = new() { "Yeterli geçmiş kadro verisi bulunamadı; muhtemel 11 tahmin edilemedi." }
            };
        }

        var latest = snapshots[0];
        var formation = FormationBuilder.Infer(
            latest.Count(p => p.Position == "D"),
            latest.Count(p => p.Position == "M"),
            latest.Count(p => p.Position == "F"));
        var target = FormationBuilder.TargetCounts(formation);

        var selected = PlayerSelectionEngine.Select(snapshots, unavailable, target);
        var players = selected.Select(s => new ExpectedPlayerDto
        {
            Number = s.Number, Name = s.Name, Position = s.Position,
            Role = FormationBuilder.RoleLabel(s.Position), Confidence = s.Confidence
        }).ToList();

        var unavailableStarters = latest.Count(p => unavailable.Contains(p.PlayerName));
        var confidence = FormationConfidenceCalculator.Team("Predicted", snapshots.Count, players.Count, unavailableStarters);

        var reasons = new List<string>
        {
            $"Son {snapshots.Count} maçın başlayan 11'lerine dayalı tahmin.",
            $"Diziliş son maçlarda ağırlıklı {formation}.",
        };
        foreach (var s in statuses.Where(s => s.Status is "Injured" or "Suspended").Take(3))
        {
            var tr = s.Status == "Suspended" ? "cezalı" : "sakat";
            reasons.Add(string.IsNullOrWhiteSpace(s.Reason)
                ? $"{s.PlayerName} {tr} nedeniyle kadro dışı."
                : $"{s.PlayerName} kadro dışı ({s.Reason}).");
        }
        if (players.Count < 11)
            reasons.Add("Bazı mevkiler için yeterli aday verisi yok; tahmin kısmi.");

        return new ExpectedLineupDto
        {
            TeamName = teamName, Formation = formation, Players = players, Confidence = confidence,
            UnavailablePlayers = unavailableDtos, Source = "Predicted", Reasons = reasons
        };
    }

    // ── Yardımcılar ────────────────────────────────────────────────────────────
    private (HashSet<string> names, List<ExpectedUnavailableDto> dtos, List<MatchPlayerStatus> raw)
        GetUnavailable(int matchId, int teamId)
    {
        var statuses = _statusRepo.GetByMatchId(matchId).Where(s => s.TeamId == teamId).ToList();
        var names = new HashSet<string>(
            statuses.Where(s => s.Status is "Injured" or "Suspended").Select(s => s.PlayerName),
            StringComparer.OrdinalIgnoreCase);
        var dtos = statuses
            .Select(s => new ExpectedUnavailableDto { Name = s.PlayerName, Status = s.Status, Reason = s.Reason })
            .ToList();
        return (names, dtos, statuses);
    }

    private static string InferFormation(IReadOnlyList<MatchLineupPlayer> players)
        => FormationBuilder.Infer(
            players.Count(p => p.Position == "D"),
            players.Count(p => p.Position == "M"),
            players.Count(p => p.Position == "F"));

    private static readonly Dictionary<string, int> LineOrder = new()
    {
        ["G"] = 0, ["D"] = 1, ["M"] = 2, ["F"] = 3
    };

    private static List<MatchLineupPlayer> OrderByLine(IEnumerable<MatchLineupPlayer> players)
        => players
            .OrderBy(p => LineOrder.TryGetValue(p.Position, out var o) ? o : 2)
            .ThenBy(p => p.ShirtNumber)
            .ToList();
}
