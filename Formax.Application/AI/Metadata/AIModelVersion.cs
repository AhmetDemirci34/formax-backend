using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Metadata;

public sealed class AIModelVersion
{
    public string Code { get; init; } = default!;
    public string Description { get; init; } = default!;
    public DateTime ReleasedAt { get; init; }
}
