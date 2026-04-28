using System.Collections.Generic;

namespace Formax.Application.DTOs.Teams
{
    public class SetMyTeamsRequest
    {
        public List<int> TeamIds { get; set; } = new();
    }
}
