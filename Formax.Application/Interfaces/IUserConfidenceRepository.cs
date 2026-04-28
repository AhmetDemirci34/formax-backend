using Formax.Domain.Entities;

public interface IUserConfidenceRepository
{
    Task<UserConfidence?> Get(string userId);
    Task Add(UserConfidence c);
    Task Update(UserConfidence c);
}
