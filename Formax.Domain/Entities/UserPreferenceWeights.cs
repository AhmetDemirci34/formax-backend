using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Domain.Entities
{
    public class UserPreferenceWeights
    {
        public int UserId { get; set; }

        public double LikeWeight { get; set; }
        public double SkipWeight { get; set; }
        public double TeamWeight { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
