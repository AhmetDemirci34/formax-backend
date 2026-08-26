using Formax.Domain.Entities;
using Formax.Domain.States;

namespace Formax.Application.AI.Contexts
{
    public class AIUxStateResolver
    {
        /// <summary>Maç öncesi AI anlatısının üretilebildiği pencere (saat) — fikstür keşfiyle aynı ufuk.</summary>
        private const double PreMatchAiWindowHours = 168; // 7 gün

        public AIUxState Resolve(
            Match match,
            LastExtendedContextKey? lastExtendedContext)
        {
            var now = DateTime.UtcNow;
            var hoursToMatch = (match.MatchDate - now).TotalHours;

            if (match.Status == "Live")
                return AIUxState.Extended;

            // MAÇ ÖNCESİ AI PENCERESİ. Eskiden 6 saatti; ölçüldüğünde yaklaşan maçların tamamı
            // (fikstür keşfi 7 gün ileriyi kapsıyor) Silent kalıyor ve üç AI yüzeyi hiç
            // üretilmiyordu. Pencere fikstür keşfiyle aynı ufka (7 gün) çekildi; durum
            // makinesinin kendisi (Live → Extended, tekrar okumada Short, decay/SelfRetracted)
            // AYNEN korunur. Ufkun ötesindeki maçta AI yine susar.
            if (hoursToMatch <= PreMatchAiWindowHours)
            {
                if (lastExtendedContext != null)
                    return AIUxState.Short;

                return AIUxState.Extended;
            }

            return AIUxState.Silent;
        }
    }
}
