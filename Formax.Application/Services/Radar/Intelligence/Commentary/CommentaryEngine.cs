using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.Services.Radar.Intelligence.Commentary
{
    /// <summary>
    /// Radar Commentary (R.12.1) — default deterministic engine. Reads the signals stored
    /// on the intelligence snapshot (weight-ordered), maps each signal type to a fixed
    /// Turkish phrase, and assembles:
    ///   Headline = top 1-2 signals
    ///   Summary  = top 3-5 signals
    /// Tone is banded from the importance level (bumped by news/market signals). No AI,
    /// no prediction, no "wins"/"favourite" — only a summary of the data.
    /// </summary>
    public sealed class CommentaryEngine : ICommentaryEngine
    {
        private static readonly IReadOnlyDictionary<MatchSignalType, string> Phrases =
            new Dictionary<MatchSignalType, string>
            {
                [MatchSignalType.Derby] = "Derbi karşılaşması",
                [MatchSignalType.Rivalry] = "Geleneksel rakipler karşı karşıya",
                [MatchSignalType.Final] = "Final maçı",
                [MatchSignalType.Playoff] = "Playoff karşılaşması",
                [MatchSignalType.TitleRace] = "Zirve yarışını etkileyebilir",
                [MatchSignalType.RelegationBattle] = "Düşme hattı mücadelesi",
                [MatchSignalType.ImportantMatch] = "Önemli bir karşılaşma",
                [MatchSignalType.HighImportance] = "Yüksek öneme sahip maç",
                [MatchSignalType.StrongForm] = "Takımlardan biri güçlü form grafiği sergiliyor",
                [MatchSignalType.WeakForm] = "Form düşüklüğü dikkat çekiyor",
                [MatchSignalType.FormAdvantage] = "Form farkı belirgin",
                [MatchSignalType.HistoricalDominance] = "Tarihsel üstünlük öne çıkıyor",
                [MatchSignalType.BalancedRivalry] = "Dengeli bir rekabet geçmişi var",
                [MatchSignalType.NewsAttention] = "Maç öncesi haber ilgisi var",
                [MatchSignalType.NewsMomentum] = "Haber akışı yoğun",
                [MatchSignalType.UserAttention] = "Kullanıcı ilgisi yükseliyor",
                [MatchSignalType.FollowAttention] = "Takip ilgisi yüksek",
                [MatchSignalType.SourceConfidence] = "Birden fazla kaynak doğruluyor",
                [MatchSignalType.NewsDrivenMatch] = "Maç öncesi haber akışı yoğun",
                [MatchSignalType.MarketAttention] = "Karşılaşma yüksek ilgi görüyor",
            };

        public CommentaryResult Generate(MatchIntelligenceSnapshot intelligence, NewsIntelligenceSnapshot? news)
        {
            var signals = Parse(intelligence.SignalsJson);
            if (signals.Count == 0)
                return new CommentaryResult { Tone = CommentaryTone.Neutral };

            // Weight-ordered phrases (skip unknown types).
            var phrases = signals
                .OrderByDescending(s => s.Weight)
                .Select(s => Phrases.TryGetValue((MatchSignalType)s.Type, out var p) ? p : null)
                .Where(p => p is not null)
                .Cast<string>()
                .ToList();

            if (phrases.Count == 0)
                return new CommentaryResult { Tone = ToneFrom(intelligence) };

            var headline = string.Join(". ", phrases.Take(2));
            var summary = string.Join(". ", phrases.Take(5)) + ".";

            return new CommentaryResult
            {
                Headline = headline,
                Summary = summary,
                Tone = ToneFrom(intelligence)
            };
        }

        private static CommentaryTone ToneFrom(MatchIntelligenceSnapshot mi)
        {
            var baseTone = mi.ImportanceLevel switch
            {
                MatchImportanceLevel.Critical => CommentaryTone.Critical,
                MatchImportanceLevel.High => CommentaryTone.Important,
                MatchImportanceLevel.Medium => CommentaryTone.Attention,
                _ => CommentaryTone.Neutral
            };

            // News / synthetic Critical signals raise the floor to Important.
            if (baseTone < CommentaryTone.Important
                && (mi.NewsImpactLevel == NewsImpactLevel.Critical
                    || mi.SyntheticSignalLevel == SyntheticOddsLevel.Critical))
            {
                baseTone = CommentaryTone.Important;
            }

            return baseTone;
        }

        private static List<SignalDto> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<SignalDto>();
            try
            {
                return JsonSerializer.Deserialize<List<SignalDto>>(json) ?? new List<SignalDto>();
            }
            catch
            {
                return new List<SignalDto>();
            }
        }

        private sealed class SignalDto
        {
            public int Type { get; set; }
            public double Weight { get; set; }
        }
    }
}
