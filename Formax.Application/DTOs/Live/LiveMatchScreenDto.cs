using System.Collections.Generic;

namespace Formax.Application.DTOs.Live
{
    public class LiveMatchScreenDto
    {
        public required LiveMatchReadingDto Live { get; set; }
        public required List<LiveMatchEventDto> Timeline { get; set; }

        /// <summary>
        /// UI için bağlamsal flag (örn: Kupa maçı)
        /// Backend hesap yapmaz, sadece taşır.
        /// </summary>
        public bool IsHighRiskMatch { get; set; }
    }
}
