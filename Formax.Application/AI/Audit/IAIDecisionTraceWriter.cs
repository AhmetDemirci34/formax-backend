using Formax.Domain.Entities;
using Formax.Application.AI.Guardrails;

namespace Formax.Application.AI.Audit
{
    public interface IAIDecisionTraceWriter
    {
        // 🔒 MEVCUT (DOKUNULMADI)
        void Write(AIDecisionTrace trace);

        // 🔒 FAZ F6 — ADIM 6 (YENİ, OPSİYONEL)
        void Write(
            AIDecisionTrace trace,
            AiSpeakReason? silenceReason);
    }
}
