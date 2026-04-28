using Formax.Domain.Entities;

namespace Formax.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string Generate(User user);
    }
}
