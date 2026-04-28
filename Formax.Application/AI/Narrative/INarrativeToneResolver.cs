using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Narrative
{
    public interface INarrativeToneResolver
    {
        NarrativeTone Resolve(bool isPremium);
    }
}

