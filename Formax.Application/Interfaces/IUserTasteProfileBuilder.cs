using Formax.Domain.Entities;

namespace Formax.Application.Interfaces;

public interface IUserTasteProfileBuilder
{
    Task<UserTasteProfile> Build(int userId);
}
