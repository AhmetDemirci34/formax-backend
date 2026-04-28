using Formax.Domain.Entities;

public interface IUserWeightProfileRepository
{
    Task<UserWeightProfile?> Get(int userId);
    Task Save(UserWeightProfile profile);
}