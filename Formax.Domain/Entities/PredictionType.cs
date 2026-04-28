using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class PredictionType
    {
        public int Id { get; set; }               // Primary Key
        public string Name { get; set; } = null!; // Over 2.5, Draw vb.
        public DateTime CreatedAt { get; set; }   // DB tarafından set edilir

        // 🧠 İş kuralı anahtarı
        public string GroupCode { get; set; } = null!;
    }
}
