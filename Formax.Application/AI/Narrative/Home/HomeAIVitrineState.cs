using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Narrative.Home
{
    /// <summary>
    /// Home ekranında AI’nin genel ruh hali.
    /// 🔒 FAZ 8.1 — STATİK
    /// 🔓 FAZ 9 — AI State-machine ile beslenecek
    /// </summary>
    public enum HomeAIVitrineState
    {
        Silent = 0,        // AI özellikle geri planda
        Cautious = 1,      // Temkinli, izliyor
        Observing = 2,     // Veri var ama yorum sınırlı
        Confident = 3      // Nadiren, güçlü bağlam
    }
}

