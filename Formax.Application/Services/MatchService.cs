using Formax.Application.DTOs.Home;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;   // 🔥 BUNU EKLE
using Formax.Application.Services.Sapma;
using Formax.Domain.Entities;

namespace Formax.Application.Services
{
    public class MatchService
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly ISapmaMotor _sapmaMotor;
        private readonly IOynanmaSinyalProvider _oynanmaProvider; // 🔥 DOĞRU INTERFACE

        public MatchService(
            IMatchReadRepository matchReadRepository,
            ISapmaMotor sapmaMotor,
            IOynanmaSinyalProvider oynanmaProvider) // 🔥
        {
            _matchReadRepository = matchReadRepository;
            _sapmaMotor = sapmaMotor;
            _oynanmaProvider = oynanmaProvider;
        }

        public Match? GetById(int matchId)
        {
            return _matchReadRepository.GetById(matchId);
        }

        public async Task<MatchDetailDto?> GetMatchDetail(int id)
        {
            var match = GetById(id);
            if (match == null) return null;

            // 🔥 GERÇEK OYNANMA
            var sinyal = await _oynanmaProvider.GetAsync(id);
            var oynanmaSkoru = sinyal?.Intensity ?? 50;

            var listItem = new MatchListItemDto
            {
                MatchId = match.Id,
                Status = match.Status,
                StartTime = match.MatchDate,
                OynanmaSkoru = oynanmaSkoru // 🔥
            };

            var sapma = _sapmaMotor.CalculateForListItem(listItem);

            return new MatchDetailDto
            {
                MatchId = match.Id,

                HomeTeam = new TeamSummaryDto
                {
                    Name = match.HomeTeam?.Name ?? "Team A"
                },

                AwayTeam = new TeamSummaryDto
                {
                    Name = match.AwayTeam?.Name ?? "Team B"
                },

                MatchDate = match.MatchDate,
                Status = match.Status,

                Sapma = new SapmaDto
                {
                    OynanmaSkoru = oynanmaSkoru, // 🔥
                    GucSkoru = sapma.GucSkoru,
                    Sapma = sapma.Sapma,

                    OynanmaYonu = sapma.OynanmaYonu,
                    GercekGucYonu = sapma.GercekGucYonu,

                    SapmaBolgesi = sapma.SapmaBolgesi,
                    SapmaMetni = sapma.SapmaMetni,
                    SessizMi = sapma.SessizMi
                },

                Ai = new AiDto
                {
                    State = "READY",
                    Summary = "Sapma motoru aktif"
                },

                UserProtection = new UserProtectionDto
                {
                    ResponsibilityNote = "Bu çıktı bir ölçümdür.",
                    DecisionIsYours = true
                }
            };
        }
    }
}