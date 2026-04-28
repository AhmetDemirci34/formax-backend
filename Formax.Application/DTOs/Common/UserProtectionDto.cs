using Formax.Application.Common.Enums;

namespace Formax.Application.DTOs.Common;

public class UserProtectionDto
{
    public RecommendationLevel RecommendationLevel { get; set; }

    public bool IsScenarioBased { get; set; }

    public string UserProtectionNote { get; set; } = string.Empty;

    public string AIResponsibilityNote { get; set; } = string.Empty;
}


