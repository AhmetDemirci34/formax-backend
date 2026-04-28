using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.States
{
    /// <summary>
    /// 🔒 AI konuşma state-machine kök durumu
    /// </summary>
    public enum AiSpeakState
    {
        Initial = 0,        // İlk temas / reset
        Silent = 1,         // Bilinçli sessizlik
        SoftRead = 2,       // Yüzeysel okuma
        DeepRead = 3,       // Derin bağlam
        Cooldown = 4        // Aşırı kullanım sonrası bekleme
    }
}
