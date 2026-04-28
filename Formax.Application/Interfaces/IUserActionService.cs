using Formax.Application.DTOs;

public interface IUserActionService
{
    Task Create(UserActionDto dto);
}
