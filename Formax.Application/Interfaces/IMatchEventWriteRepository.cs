public interface IMatchEventWriteRepository
{
    Task AddAsync(
        int matchId,
        string eventType,
        int minute,
        string teamName,
        string? playerName,
        string description);
}
