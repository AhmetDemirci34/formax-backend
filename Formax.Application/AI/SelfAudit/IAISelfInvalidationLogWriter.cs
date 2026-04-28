using Formax.Domain.Entities;

namespace Formax.Application.AI.SelfAudit
{
    public interface IAISelfInvalidationLogWriter
    {
        void Write(AISelfInvalidationLog log);
    }
}
