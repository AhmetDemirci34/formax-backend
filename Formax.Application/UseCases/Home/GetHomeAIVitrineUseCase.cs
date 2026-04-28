using Formax.Application.DTOs.AI;
using Formax.Application.Services;
using Formax.Application.AI.World;
using System;
using System.Collections.Generic;

namespace Formax.Application.UseCases.Home
{
    /// <summary>
    /// FORMAX — FAZ 4.4
    /// Home ekranı AI vitrini.
    /// Amaç: yönlendirme yapmadan bağlam, sessizlik ve preview mantığını home ekranında göstermek.
    /// </summary>
    public class GetHomeAIVitrineUseCase
    {
        private readonly AIHomeVitrineService _vitrineService;
        private readonly WorldPerceptionProvider _worldPerceptionProvider;

        public GetHomeAIVitrineUseCase(
            AIHomeVitrineService vitrineService,
            WorldPerceptionProvider worldPerceptionProvider)
        {
            _vitrineService = vitrineService;
            _worldPerceptionProvider = worldPerceptionProvider;
        }

        public IReadOnlyList<AIHomeVitrineDto> Execute(bool isPremium)
        {
            var world = new WorldPerceptionSummary
            {
                Headline = "Bugün dikkat çeken maçlar var.",
                Description = "Günün ritmi kontrollü başlıyor. Sapma tablosu sakin görünse de bağlam izleniyor.",
                ConfidenceLevel = "low",
                Source = "system",
                LastUpdatedUtc = DateTime.UtcNow
            };

            var main = _vitrineService.GetVitrine(world, isPremium);
            main.Tag = "Ana Okuma";
            main.Tone = isPremium ? "watch" : "premium";

            var cards = new List<AIHomeVitrineDto>
            {
                main,

                new AIHomeVitrineDto
                {
                    Tag = "Sapma Yorumu",
                    Tone = "watch",
                    Title = "Sapma dili sessiz ama izliyor",
                    Message = "Home vitrini sayı gösterir; AI ise bu sayının arkasındaki bağlamı açıklar. Sapma düşükse bunu sakinlik olarak okur, yükselirse kullanıcı algısındaki aşırılığı işaret eder.",
                    IsPremiumHint = false
                },

                new AIHomeVitrineDto
                {
                    Tag = "Sessizlik Kuralı",
                    Tone = "quiet",
                    Title = "Yeterli bağlam yoksa AI konuşmaz",
                    Message = "FORMAX her maçta konuşmak zorunda değildir. Veri zayıfsa, güven eşiği korunur ve sistem sessiz kalır. Bu sessizlik hata değil, ürün disiplinidir.",
                    IsPremiumHint = false
                },

                new AIHomeVitrineDto
                {
                    Tag = isPremium ? "Premium Aktif" : "Preview",
                    Tone = isPremium ? "watch" : "premium",
                    Title = isPremium ? "Derin bağlam katmanı açık" : "Derin bağlam katmanı preview modunda",
                    Message = isPremium
                        ? "Premium kullanıcıda AI, aynı sayfanın içinde daha geniş bağlam ve senaryo derinliği sunar. Arayüz değişmez; fark içerikte oluşur."
                        : "Free kullanıcı ana okumayı görür. Derin anlatı, aynı sayfada preview mantığıyla hissedilir ama tam açılmaz. Böylece paywall sertliği oluşmaz.",
                    IsPremiumHint = !isPremium
                }
            };

            return cards;
        }
    }
}
