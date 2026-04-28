using Formax.Application.AI.Contexts;
using Formax.Application.DTOs.Auth;
using Formax.Application.Interfaces;
using Formax.Application.Services;

namespace Formax.Application.UseCases.Auth
{
    public class LoginUserUseCase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHashService _passwordHashService;
        private readonly IJwtTokenService _jwtService;
        private readonly UserExperienceTransferService _experienceTransfer;

        public LoginUserUseCase(
            IUserRepository userRepository,
            IPasswordHashService passwordHashService,
            IJwtTokenService jwtService,
            UserExperienceTransferService experienceTransfer)
        {
            _userRepository = userRepository;
            _passwordHashService = passwordHashService;
            _jwtService = jwtService;
            _experienceTransfer = experienceTransfer;
        }

        public AuthResponseDto Execute(LoginRequestDto request)
        {
            // ----------------------------
            // USER BUL
            // ----------------------------
            var user = _userRepository.GetByEmail(request.Email)
                ?? throw new InvalidOperationException("Invalid credentials.");

            // ----------------------------
            // PASSWORD VERIFY
            // ----------------------------
            if (!_passwordHashService.Verify(user.PasswordHash, request.Password))
                throw new InvalidOperationException("Invalid credentials.");

            // ----------------------------
            // LAST LOGIN
            // ----------------------------
            user.LastLoginAt = DateTime.UtcNow;
            _userRepository.Update(user);

            // ----------------------------
            // EXPERIENCE TRANSFER
            // anon behaviour → user profile
            // ----------------------------
            try
            {
                var ctx = new UserExperienceContext
                {
                    UserId = user.Id,
                    IsRegistered = true
                };

                _experienceTransfer.TransferToUser(ctx);
            }
            catch
            {
                // experience transfer login'i bozmasın
            }

            // ----------------------------
            // JWT TOKEN
            // ----------------------------
            var token = _jwtService.Generate(user);

            // ----------------------------
            // RESPONSE
            // ----------------------------
            return new AuthResponseDto
            {
                UserId = user.Id,
                Token = token,
                IsPremium = user.IsPremium,
                AccessLevel = user.IsPremium ? "Premium" : "Free"
            };
        }
    }
}