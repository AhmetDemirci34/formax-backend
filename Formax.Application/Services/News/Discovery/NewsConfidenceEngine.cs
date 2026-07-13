using System;

namespace Formax.Application.Services.News.Discovery
{
    /// <summary>
    /// FORMAX Data Engine v2 — News Confidence. Tekilleştirilmiş bir haber için 0–100
    /// güven üretir: kaç kaynak doğruladı (tekrar sayısı), yayın tazeliği, başlık
    /// uygunluğu (her iki takım geçiyor mu). Tek kaynak asla yüksek güvene ulaşamaz.
    /// </summary>
    public sealed class NewsConfidenceEngine
    {
        private const int SingleSourceCeiling = 70;

        public int Score(int sourceCount, DateTime publishedUtc, bool bothTeamsInTitle)
        {
            var baseScore = sourceCount >= 4 ? 92
                          : sourceCount == 3 ? 85
                          : sourceCount == 2 ? 75
                          : 60;

            var hoursOld = (DateTime.UtcNow - publishedUtc).TotalHours;
            var recency = hoursOld <= 6 ? 5 : hoursOld > 48 ? -10 : 0;

            var relevance = bothTeamsInTitle ? 5 : 0;

            var score = baseScore + recency + relevance;
            if (sourceCount <= 1) score = Math.Min(score, SingleSourceCeiling);

            return Math.Clamp(score, 0, 99);
        }
    }
}
