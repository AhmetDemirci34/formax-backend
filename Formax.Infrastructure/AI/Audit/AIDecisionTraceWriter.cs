using Formax.Application.AI.Audit;
using Formax.Application.AI.Guardrails;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;

namespace Formax.Infrastructure.AI.Audit
{
    public class AIDecisionTraceWriter : IAIDecisionTraceWriter
    {
        private readonly FormaxDbContext _db;

        public AIDecisionTraceWriter(FormaxDbContext db)
        {
            _db = db;
        }

        // 🔒 MEVCUT (DOKUNULMADI)
        public void Write(AIDecisionTrace trace)
        {
            _db.AIDecisionTraces.Add(trace);
            _db.SaveChanges();
        }

        // 🔒 FAZ F6 — ADIM 6 (YENİ OVERLOAD)
        public void Write(
            AIDecisionTrace trace,
            AiSpeakReason? silenceReason)
        {
            // Şimdilik davranış aynen korunur
            // silenceReason ileride kullanılacak
            Write(trace);
        }
    }
}
