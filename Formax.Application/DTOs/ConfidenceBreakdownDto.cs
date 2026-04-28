using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs;

public class ConfidenceBreakdownDto
{
    public string Factor { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

