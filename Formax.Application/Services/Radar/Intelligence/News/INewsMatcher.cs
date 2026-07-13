using System.Collections.Generic;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.1) — deterministically detects which teams a staged
    /// news item mentions, using the alias dataset (substring match) and any resolved
    /// team id. No text analysis, no AI.
    /// </summary>
    public interface INewsMatcher
    {
        IReadOnlyList<NewsMention> DetectMentions(StagedSourceItem newsItem);
    }
}
