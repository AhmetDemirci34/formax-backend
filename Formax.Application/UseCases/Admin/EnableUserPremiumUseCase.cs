using Formax.Application.Interfaces;

namespace Formax.Application.UseCases.Admin
{
    public class EnableUserPremiumUseCase
    {
        private readonly IUserRepository _userRepository;

        public EnableUserPremiumUseCase(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public bool Execute(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null)
                return false;

            user.IsPremium = true;
            _userRepository.Update(user);

            return true;
        }
    }
}
