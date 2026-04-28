using Formax.Domain.Entities;

public interface IUserTasteVectorRepository
{
    Task<UserTasteVector?> Get(int userId);
    Task Save(UserTasteVector vector);
}
