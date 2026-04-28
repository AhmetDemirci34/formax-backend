using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Formax.Infrastructure.Services
{
    public class PasswordHashService : IPasswordHashService
    {
        private readonly PasswordHasher<string> _hasher = new();

        public string Hash(string password)
            => _hasher.HashPassword(null!, password);

        public bool Verify(string hash, string password)
            => _hasher.VerifyHashedPassword(null!, hash, password)
               != PasswordVerificationResult.Failed;
    }
}
