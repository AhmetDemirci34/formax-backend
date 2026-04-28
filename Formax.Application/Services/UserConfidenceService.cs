using Formax.Domain.Entities;

public class UserConfidenceService
{
    private readonly IUserConfidenceRepository _repo;

    public UserConfidenceService(IUserConfidenceRepository repo)
    {
        _repo = repo;
    }

    public async Task Update(string userId, bool isWin)
    {
        var c = await _repo.Get(userId);

        if (c == null)
        {
            c = new UserConfidence
            {
                UserId = userId,
                Value = 50
            };

            await _repo.Add(c);
        }

        // 🔥 KALİBRASYON
        if (isWin)
            c.Value = Math.Min(100, c.Value + 2);
        else
            c.Value = Math.Max(0, c.Value - 3);

        c.UpdatedAt = DateTime.UtcNow;

        await _repo.Update(c);
    }
}
