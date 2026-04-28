using Formax.Application.Interfaces;
using System.Globalization;
using System.Text;

public class UserInterestService : IUserInterestService
{
    private readonly IUserInterestScoreRepository _repository;

    public UserInterestService(IUserInterestScoreRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserInterestProfile> GetProfileAsync(int userId, DateTime utcNow)
    {
        var data = await _repository.GetByUser(userId);

        var teamScores = data
            .Where(x => x.Layer == "team")
            .GroupBy(x => Normalize(x.Key))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Score)
            );

        var leagueScores = data
            .Where(x => x.Layer == "league")
            .GroupBy(x => Normalize(x.Key))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Score)
            );

        return new UserInterestProfile
        {
            TeamScores = teamScores,
            LeagueScores = leagueScores
        };
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = Char.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}