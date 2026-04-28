using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.AI.Guardrails
{
    public sealed class AiForbiddenActionResult
    {
        public bool IsForbidden { get; }
        public string ReasonCode { get; }

        private AiForbiddenActionResult(bool isForbidden, string reasonCode)
        {
            IsForbidden = isForbidden;
            ReasonCode = reasonCode;
        }

        public static AiForbiddenActionResult Allowed()
            => new(false, string.Empty);

        public static AiForbiddenActionResult Forbidden(string reasonCode)
            => new(true, reasonCode);
    }
}

