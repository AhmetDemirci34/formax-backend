using Formax.Application.DTOs.Admin;
using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Admin
{
    public class GetAdminUserDetailUseCase
    {
        private readonly IUserRepository _userRepository;

        public GetAdminUserDetailUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public AdminUserDetailDto? Execute(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                return null;

            return new AdminUserDetailDto
            {
                UserId = user.Id,
                Email = user.Email,
                IsPremium = user.IsPremium,
                MatchesWithAiCount = user.MatchesWithAiCount,
                AiContextShownCount = user.AiContextShownCount,
                LastAiInteractionType = user.LastAiInteractionType,
                HasSeenRegisterHint = user.HasSeenRegisterHint,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}
