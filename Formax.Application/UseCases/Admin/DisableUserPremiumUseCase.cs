using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Admin
{
    public class DisableUserPremiumUseCase
    {
        private readonly IUserRepository _userRepository;

        public DisableUserPremiumUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public bool Execute(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                return false;

            user.IsPremium = false;
            _userRepository.Update(user);

            return true;
        }
    }
}
