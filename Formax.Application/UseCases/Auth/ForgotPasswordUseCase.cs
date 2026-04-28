using Formax.Application.Interfaces;
using System;

namespace Formax.Application.UseCases.Auth
{
    public class ForgotPasswordUseCase
    {
        private readonly IUserRepository _users;

        public ForgotPasswordUseCase(IUserRepository users)
        {
            _users = users;
        }

        public string Execute(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Email zorunlu.");

            var normalized = email.Trim().ToLowerInvariant();

            var user = _users.GetByEmail(normalized);

            if (user == null)
                throw new InvalidOperationException("Bu email ile kayıtlı hesap bulunamadı.");

            // 🔹 MVP: gerçek mail gönderme yok
            // 🔹 İleride burada token üretilecek + mail servisine gönderilecek

            return "Şifre sıfırlama bağlantısı gönderildi.";
        }
    }
}