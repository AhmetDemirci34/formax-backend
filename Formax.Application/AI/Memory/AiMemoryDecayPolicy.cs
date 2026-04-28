using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Memory
{
    public static class AiMemoryDecayPolicy
    {
        // 🧠 Extended context en fazla kaç gün tutulur
        public const int ExtendedContextRetentionDays = 7;

        // 🧠 Aynı gün içinde tekrar gösterime izin verme
        public const bool AllowSameDayRepeat = false;
    }
}

