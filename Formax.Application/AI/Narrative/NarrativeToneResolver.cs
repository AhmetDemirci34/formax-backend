using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Narrative
{
    public class NarrativeToneResolver : INarrativeToneResolver
    {
        public NarrativeTone Resolve(bool isPremium)
        {
            return NarrativeTone.Neutral;
        }
    }
}

