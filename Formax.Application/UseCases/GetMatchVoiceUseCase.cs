using System.Linq;
using Formax.Application.AI.Context;
using Formax.Application.AI.Decision;
using Formax.Application.AI.LLM;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.Radar.Intelligence.Scenarios;

namespace Formax.Application.UseCases
{
    /// <summary>
    /// FORMAX'IN SESİ — Voice diagnostic/introspection. Motorun ürettiği AI Decision Package'i
    /// <see cref="FormaxVoiceComposer"/> ile insan-diline-çevrilecek system+user prompt'a dönüştürür
    /// ve LLM'e GÖNDERİLECEK metni döner (LLM çağrısı YAPMADAN — yalnız kompozisyonu doğrular).
    /// Ses YALNIZ AiDecisionPackage okur; başka veri kaynağı yok. Salt-okunur doğrulama içindir.
    /// </summary>
    public sealed class GetMatchVoiceUseCase
    {
        private readonly IMatchReadRepository _matchRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly IMatchAiContextBuilder _builder;
        private readonly MarketProbabilityEngine _engine;
        private readonly FormaxVoiceComposer _voice = new();

        public GetMatchVoiceUseCase(
            IMatchReadRepository matchRepo,
            ITeamReadRepository teamRepo,
            IMatchAiContextBuilder builder,
            MarketProbabilityEngine engine)
        {
            _matchRepo = matchRepo;
            _teamRepo = teamRepo;
            _builder = builder;
            _engine = engine;
        }

        public object? Execute(int matchId, string? screen = null)
        {
            var match = _matchRepo.GetById(matchId);
            if (match == null) return null;

            var homeName = _teamRepo.GetById(match.HomeTeamId)?.Name ?? "Ev sahibi";
            var awayName = _teamRepo.GetById(match.AwayTeamId)?.Name ?? "Deplasman";

            // FAZ 1 — gerçek TeamComparison/H2H/GücSkoru builder tarafından üretilir (boş DTO kalktı).
            var context = _builder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId, homeName, awayName);

            AiDecisionPackage package = _engine.BuildDecisionPackage(context);

            var screenEnum = ParseScreen(screen);

            // Keşfet kartı = analizin fragmanı (analiz DEĞİL) → ayrı çıktı.
            if (screenEnum == FormaxVoiceScreen.DiscoverCard)
            {
                var comment = _voice.ComposeDiscoverCardComment(package);
                return new
                {
                    matchId,
                    screen = screenEnum.ToString(),
                    comment,
                    charCount = comment?.Length ?? 0,
                    bannedLeaks = FormaxVoiceComposer.DiscoverBannedTerms
                        .Where(t => (comment ?? "").Contains(t, System.StringComparison.OrdinalIgnoreCase))
                        .ToArray(),
                    systemPrompt = _voice.ComposeDiscoverCardSystemPrompt()
                };
            }

            var systemPrompt = _voice.ComposeSystemPrompt();
            var userBrief = _voice.ComposeUserBrief(package, screenEnum);

            return new
            {
                matchId,
                screen = screenEnum.ToString(),
                systemPrompt,
                userBrief,
                // Sızıntı denetimi: brief'te yasaklı teknik terim var mı (0 olmalı).
                forbiddenTermLeaks = FormaxVoiceComposer.ForbiddenTerms
                    .Where(t => userBrief.Contains(t, System.StringComparison.OrdinalIgnoreCase))
                    .ToArray()
            };
        }

        private static FormaxVoiceScreen ParseScreen(string? s) =>
            System.Enum.TryParse<FormaxVoiceScreen>(s, ignoreCase: true, out var v) ? v : FormaxVoiceScreen.MatchDetail;
    }
}
