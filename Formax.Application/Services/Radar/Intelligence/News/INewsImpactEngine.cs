using System.Collections.Generic;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.3) — computes a deterministic 0-100 news impact
    /// score from per-category counts using a fixed weight table. No AI.
    /// </summary>
    public interface INewsImpactEngine
    {
        NewsImpactResult Evaluate(IReadOnlyDictionary<NewsCategory, int> categoryCounts);
    }
}
