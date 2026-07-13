using Formax.Domain.Entities;

namespace Formax.Application.Services.Radar.Intelligence.News
{
    /// <summary>
    /// Radar News Intelligence (R.10.2) — assigns exactly one <see cref="NewsClassificationResult"/>
    /// to a staged news item using a fixed keyword-priority order. Deterministic; no AI.
    /// </summary>
    public interface INewsClassifier
    {
        NewsClassificationResult Classify(StagedSourceItem newsItem);
    }
}
