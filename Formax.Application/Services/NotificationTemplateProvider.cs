using Formax.Application.Live;
using System;

namespace Formax.Application.Services
{
    public static class NotificationTemplateProvider
    {
        public static (string Title, string Message) Get(MatchEvent matchEvent)
        {
            if (matchEvent == null)
                throw new ArgumentNullException(nameof(matchEvent));

            return matchEvent.EventType switch
            {
                // 🟢 GENEL MAÇ EVENTLERİ
                MatchEventType.MatchStarted =>
                    ("Maç başladı", "Takip ettiğin maç başladı."),

                MatchEventType.HalfTime =>
                    ("Devre arası", "Maçta ilk yarı sona erdi."),

                MatchEventType.MatchEnded =>
                    ("Maç bitti", "Takip ettiğin maç sona erdi."),

                // ⚽ OYUN İÇİ
                MatchEventType.Goal =>
                    ("Gol oldu", "Takip ettiğin maçta gol oldu."),

                // 🟡 PENALTI SÜRECİ
                MatchEventType.PenaltyAwarded =>
                    ("Penaltı kararı", "Maçta penaltı kararı verildi."),

                MatchEventType.PenaltyVarReviewStarted =>
                    ("VAR incelemesi", "Penaltı kararı VAR tarafından inceleniyor."),

                MatchEventType.PenaltyConfirmed =>
                    ("Penaltı onaylandı", "VAR incelemesi sonrası penaltı kararı geçerli sayıldı."),

                MatchEventType.PenaltyCancelled =>
                    ("Penaltı iptal edildi", "VAR incelemesi sonrası penaltı kararı iptal edildi."),

                // 🔴 KIRMIZI KART SÜRECİ
                MatchEventType.RedCardAwarded =>
                    (
                        "Kırmızı kart",
                        !string.IsNullOrWhiteSpace(matchEvent.PlayerName)
                            ? $"{matchEvent.TeamName} takımından {matchEvent.PlayerName}, {matchEvent.Minute}. dakikada kırmızı kart gördü."
                            : "Maçta kırmızı kart çıktı."
                    ),

                MatchEventType.RedCardVarReviewStarted =>
                    ("VAR incelemesi", "Kırmızı kart kararı VAR tarafından inceleniyor."),

                MatchEventType.RedCardConfirmed =>
                    ("Kırmızı kart onaylandı", "VAR incelemesi sonrası kırmızı kart kararı geçerli sayıldı."),

                MatchEventType.RedCardCancelled =>
                    ("Kırmızı kart iptal edildi", "VAR incelemesi sonrası kırmızı kart kararı iptal edildi."),

                // ❓ FALLBACK
                _ =>
                    ("Maç güncellemesi", "Takip ettiğin maçta yeni bir gelişme oldu.")
            };
        }
    }
}
