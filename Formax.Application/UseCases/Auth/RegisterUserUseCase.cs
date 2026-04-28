using Formax.Application.DTOs.Auth;
using Formax.Application.Interfaces;
using Formax.Domain.Entities;

namespace Formax.Application.UseCases.Auth
{
    public class RegisterUserUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHashService _passwordHashService;
        private readonly IJwtTokenService _jwtService;

        public RegisterUserUseCase(
            IUserRepository userRepository,
            IPasswordHashService passwordHashService,
            IJwtTokenService jwtService)
        {
            _userRepository = userRepository;
            _passwordHashService = passwordHashService;
            _jwtService = jwtService;
        }

        public AuthResponseDto Execute(RegisterRequestDto request)
        {
            if (_userRepository.GetByEmail(request.Email) != null)
                throw new InvalidOperationException("Email already registered.");

            var user = new User
            {
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = _passwordHashService.Hash(request.Password)
            };

            _userRepository.Add(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Token = _jwtService.Generate(user),
                IsPremium = user.IsPremium,
                AccessLevel = user.IsPremium ? "Premium" : "Free"
            };
        }
    }
}
