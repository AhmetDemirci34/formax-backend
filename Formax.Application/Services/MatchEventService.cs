using Formax.Application.Live;
using Formax.Domain.Entities;

namespace Formax.Application.Services
{
    public class MatchEventService
    {
        public MatchEvent? Detect(Match previous, Match current)
        {
            var prevCtx = MatchEventAdapter.From(previous);
            var currCtx = MatchEventAdapter.From(current);

            bool AlreadyEmitted(MatchEventType type) =>
                current.LastEmittedEventType == type.ToString();

            // 🟢 MAÇ BAŞLADI
            if (previous.Status != "Live" &&
                current.Status == "Live" &&
                !AlreadyEmitted(MatchEventType.MatchStarted))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.MatchStarted,
                    Minute = currCtx.Minute,
                    Description = "Maç başladı"
                };
            }

            // 🔴 MAÇ BİTTİ
            if (previous.Status != "Finished" &&
                current.Status == "Finished" &&
                !AlreadyEmitted(MatchEventType.MatchEnded))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.MatchEnded,
                    Minute = currCtx.Minute,
                    Description = "Maç sona erdi"
                };
            }

            // 🟥 KIRMIZI KART
            if (!prevCtx.HasRedCard &&
                currCtx.HasRedCard &&
                !AlreadyEmitted(MatchEventType.RedCardAwarded))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.RedCardAwarded,
                    Minute = currCtx.Minute,
                    PlayerName = currCtx.RedCardPlayerName,
                    TeamName = currCtx.RedCardTeamName,
                    Description = currCtx.RedCardPlayerName != null
                        ? $"{currCtx.RedCardTeamName} takımından {currCtx.RedCardPlayerName}, {currCtx.Minute}. dakikada kırmızı kart gördü."
                        : "Maçta kırmızı kart çıktı."
                };
            }

            // ⚽ GOL
            if (previous.HomeScore + previous.AwayScore <
                current.HomeScore + current.AwayScore &&
                !AlreadyEmitted(MatchEventType.Goal))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.Goal,
                    Minute = currCtx.Minute,
                    Description = "Maçta gol oldu"
                };
            }

            // 🟡 PENALTI VERİLDİ
            if (!prevCtx.HasPenalty &&
                currCtx.HasPenalty &&
                !AlreadyEmitted(MatchEventType.PenaltyAwarded))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.PenaltyAwarded,
                    Minute = currCtx.Minute,
                    Description = "Maçta penaltı kararı verildi"
                };
            }

            // 🟣 VAR İNCELEMESİ
            if (!prevCtx.IsVarReview &&
                currCtx.IsVarReview &&
                !AlreadyEmitted(MatchEventType.PenaltyVarReviewStarted))
            {
                return new MatchEvent
                {
                    MatchId = current.Id,
                    EventType = MatchEventType.PenaltyVarReviewStarted,
                    Minute = currCtx.Minute,
                    Description = "Pozisyon VAR tarafından inceleniyor"
                };
            }

            return null;
        }
    }
}
