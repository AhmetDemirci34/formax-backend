using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Contexts
{
    /// <summary>
    /// Kadro ve son dakika bilgileri.
    /// Detay yok, isim yok.
    /// </summary>
    public sealed class SquadContext
    {
        public bool LineupsAnnounced { get; init; }
        public bool HasKeyAbsence { get; init; }
        public bool LastMinuteChange { get; init; }
    }
}

