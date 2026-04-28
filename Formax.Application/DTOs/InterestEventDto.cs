using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class InterestEventDto
{
    public int MatchId { get; set; }
    public string EventType { get; set; } = string.Empty;

    public int? UserId { get; set; }
    public string? SessionId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
