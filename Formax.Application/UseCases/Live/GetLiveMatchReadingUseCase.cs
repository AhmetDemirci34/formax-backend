using Formax.Application.DTOs.Live;
using Formax.Application.Interfaces;
using Formax.Application.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Formax.Application.UseCases.Live
{
    public class GetLiveMatchReadingUseCase
    {
        private readonly IMatchReadRepository _matchReadRepository;
        private readonly IMatchWriteRepository _matchWriteRepository;
        private readonly MatchEventService _matchEventService;
        private readonly MatchEventNotificationService _matchEventNotificationService;

        public GetLiveMatchReadingUseCase(
            IMatchReadRepository matchReadRepository,
            IMatchWriteRepository matchWriteRepository,
            MatchEventService matchEventService,
            MatchEventNotificationService matchEventNotificationService)
        {
            _matchReadRepository = matchReadRepository;
            _matchWriteRepository = matchWriteRepository;
            _matchEventService = matchEventService;
            _matchEventNotificationService = matchEventNotificationService;
        }

        public async Task<LiveMatchReadingDto> ExecuteAsync(int matchId)
        {
            // 🔹 CURRENT SNAPSHOT
            var currentMatch = _matchReadRepository
                .Query()
                .FirstOrDefault(m => m.Id == matchId);

            if (currentMatch == null)
                return new LiveMatchReadingDto();

            // 🔹 PREVIOUS SNAPSHOT (persisted state üzerinden)
            var previousMatch = currentMatch.CloneForComparison();

            // 🔹 EVENT TESPİTİ
            var matchEvent = _matchEventService.Detect(previousMatch, currentMatch);

            if (matchEvent != null)
            {
                // 🔒 Event tekrar üretilmesin diye state yaz
                currentMatch.LastEmittedEventType = matchEvent.EventType.ToString();
                _matchWriteRepository.Update(currentMatch);

                // 🔔 Notification
                await _matchEventNotificationService.HandleAsync(matchEvent);

             
            }

            var minute = int.TryParse(currentMatch.MatchMinute, out var parsedMinute)
                ? parsedMinute
                : 0;

            var reading = new LiveMatchReadingDto
            {
                MatchId = currentMatch.Id,
                MatchMinute = minute,
                IsPremiumContent = true,
                LastEventType = matchEvent?.EventType.ToString()
            };

            // MAÇ DURUMU
            if (minute < 15)
            {
                reading.MatchState = "Maç dengeli başladı";
                reading.CurrentFlow = "Takımlar kontrollü oynuyor, tempo henüz yükselmedi.";
                reading.PossibleScenario = "İlk gol genellikle bu dakikalardan sonra gelir.";
                reading.LowProbabilityNote = "Erken skor ihtimali düşük görünüyor.";
            }
            else if (minute < 45)
            {
                reading.MatchState = "Tempo artmaya başladı";
                reading.CurrentFlow = "Orta saha mücadelesi sertleşiyor, oyun yön değiştirebilir.";
                reading.PossibleScenario = "Devre bitmeden skor değişimi mümkün.";
                reading.LowProbabilityNote = "Uzun süreli golsüzlük ihtimali azalıyor.";
            }
            else if (minute < 70)
            {
                reading.MatchState = "Maç kritik evrede";
                reading.CurrentFlow = "Fiziksel düşüşler başladı, hatalar artabilir.";
                reading.PossibleScenario = "Tek bir an maçın yönünü belirleyebilir.";
                reading.LowProbabilityNote = "Ritmin tamamen düşmesi zor.";
            }
            else
            {
                reading.MatchState = "Maç son bölüme girdi";
                reading.CurrentFlow = "Riskler artıyor, savunma hataları görülebilir.";
                reading.PossibleScenario = "Son dakikalarda skor değişimi ihtimali yüksek.";
                reading.LowProbabilityNote = "Maçın tamamen kilitlenmesi zor.";
            }

            reading.UserWarning =
                "Canlı maç verileri hızlı değişebilir. Tek bir anlık duruma göre karar vermekten kaçın.";

            return reading;
        }
    }
}
