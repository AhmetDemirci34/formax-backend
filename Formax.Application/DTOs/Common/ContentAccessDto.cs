using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Formax.Application.DTOs.Common;

public class ContentAccessDto
{
    public bool IsPremium { get; set; }

    // ✅ KONTRAT KİLİT: string | null (UI için tek tip)
    public string? PremiumReason { get; set; }

    public FreeContentLimitationDto? FreeContentLimitation { get; set; }

    public LegalNoticeDto LegalNotice { get; set; } = new();
}

