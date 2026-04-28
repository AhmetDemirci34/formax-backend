using Formax.Application.AI.Contexts;
using Formax.Application.Interfaces;

namespace Formax.Application.Services
{
    public class UserExperienceTransferService
    {
        private readonly IUserRepository _userRepository;

        public UserExperienceTransferService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public void TransferToUser(UserExperienceContext ctx)
        {
            // 🔒 Sadece REGISTER sonrası çalışır
            if (!ctx.IsRegistered || !ctx.UserId.HasValue)
                return;

            var user = _userRepository.GetById(ctx.UserId.Value);
            if (user == null)
                return;

            // 🧠 ANON → USER AKTARIMI
            // Toplama YOK → overwrite (şişirme engellenir)
            user.MatchesWithAiCount = ctx.MatchesWithAiCount;
            user.AiContextShownCount = ctx.AiContextShownCount;
            user.HasSeenRegisterHint = ctx.HasSeenRegisterHint;

            // Opsiyonel bağlam (null güvenli)
            if (!string.IsNullOrWhiteSpace(ctx.LastAiInteractionType))
                user.LastAiInteractionType = ctx.LastAiInteractionType;

            _userRepository.Update(user);
        }
    }
}
