using Formax.Application.DTOs.AI;
using System;

namespace Formax.Application.UX
{
    public sealed class AiUxResponseDto
    {
        public string UxState { get; init; } = string.Empty;

        public WorldStatus World { get; init; } = new();
        public NarrativeStatus Narrative { get; init; } = new();
        public SilenceStatus Silence { get; init; } = new();
        public PermissionStatus Permissions { get; init; } = new();
        public bool IsSilent { get; set; }
        public string? SilenceReasonKey { get; set; }
        public WorldExpectationDto? WorldExpectation { get; set; }
        public string? SilenceMessage { get; set; }
    }

    public sealed class WorldStatus
    {
        public bool Updated { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public bool ShiftDetected { get; init; }
    }

    public sealed class NarrativeStatus
    {
        public NarrativeBlock Base { get; init; } = new();
        public NarrativeExtension Extension { get; init; } = new();
    }

    public sealed class NarrativeBlock
    {
        public bool Exists { get; init; }
        public string? Text { get; init; }
    }

    public sealed class NarrativeExtension
    {
        public bool Allowed { get; init; }
        public bool Exists { get; init; }
        public string? Text { get; init; }
        public string? Reason { get; init; }
    }

    public sealed class SilenceStatus
    {
        public bool Active { get; init; }
        public string? Reason { get; init; }
    }

    public sealed class PermissionStatus
    {
        public bool CanAskAgain { get; init; }
        public bool CanExtend { get; init; }
        public bool RequiresPremium { get; init; }
    }
}
