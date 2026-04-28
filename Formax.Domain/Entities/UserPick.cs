using Formax.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities;

public class UserPick
{
    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public int MatchId { get; set; }

    public required string PickLabel { get; set; }

    public int Confidence { get; set; }

    public PickStatus Status { get; set; } = PickStatus.Pending;

    public DateTime CreatedAt { get; set; }
}
