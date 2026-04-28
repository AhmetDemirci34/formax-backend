using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public static class AiSpeakPermission
    {
        /// <summary>
        /// AI'nin bu anda konuşmasına izin var mı?
        /// Varsayılan: HAYIR
        /// </summary>
        public static bool CanSpeak(
            bool isPremiumUser,
            bool semanticBreakDetected,
            int aiUsageCount)
        {
            // 🔒 Varsayılan sessizlik
            if (!semanticBreakDetected)
                return false;

            // 🔓 Premium kullanıcılar daha esnek
            if (isPremiumUser)
                return true;

            // 🧱 Free kullanıcı için sınırlı frekans
            return aiUsageCount < 1;
        }
    }
}
