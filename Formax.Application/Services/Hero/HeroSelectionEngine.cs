using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Players.Intelligence;
using Formax.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Formax.Application.Services.Hero
{
    /// <summary>
    /// FORMAX HeroSelectionEngine — TEK görev: PlayerIntelligenceEngine'in ürettiği
    /// IntelligenceScore'u okuyarak her takımdan en yüksek skorlu oyuncuyu Hero seçmek,
    /// HeroReasonType'ı belirlemek ve mevcut sinyallerden HeroConfidence türetmek.
    /// Yeni sinyal ÜRETMEZ, IntelligenceScore HESAPLAMAZ, Narrative ÜRETMEZ.
    /// </summary>
    public sealed class HeroSelectionEngine : IHeroSelectionEngine
    {
        private readonly IPlayerIntelligenceEngine _playerEngine;
        private readonly IMatchIntelligenceRepository _matchIntel;
        private readonly HeroSelectionWeights _weights;
        private readonly ILogger<HeroSelectionEngine> _logger;

        public HeroSelectionEngine(
            IPlayerIntelligenceEngine playerEngine,
            IMatchIntelligenceRepository matchIntel,
            HeroSelectionWeights weights,
            ILogger<HeroSelectionEngine> logger)
        {
            _playerEngine = playerEngine;
            _matchIntel = matchIntel;
            _weights = weights;
            _logger = logger;
        }

        public async Task<HeroSelectionResult> SelectAsync(int matchId, CancellationToken ct = default)
        {
            var squad = await _playerEngine.GetSquadIntelligenceAsync(matchId, ct);

            var home = TopBySide(squad, "Home");
            var away = TopBySide(squad, "Away");
            var dominant = Dominant(home, away);

            var (radarConfidence, matchImportance) = await ReadRadarSignalsAsync(matchId, ct);

            return new HeroSelectionResult
            {
                HomeHero = home,
                AwayHero = away,
                HeroReasonType = MapReason(dominant?.PrimaryReason),
                HeroConfidence = DeriveConfidence(home, away, radarConfidence, matchImportance),
            };
        }

        private static PlayerIntelligence? TopBySide(List<PlayerIntelligence> squad, string side) =>
            squad.Where(p => string.Equals(p.Side, side, StringComparison.OrdinalIgnoreCase))
                 .OrderByDescending(p => p.IntelligenceScore)
                 .FirstOrDefault();

        private static PlayerIntelligence? Dominant(PlayerIntelligence? a, PlayerIntelligence? b)
        {
            if (a is null) return b;
            if (b is null) return a;
            return a.IntelligenceScore >= b.IntelligenceScore ? a : b;
        }

        // PlayerIntelligence.PrimaryReason (string) → HeroReasonType eşlemesi.
        private static HeroReasonType MapReason(string? reason) => (reason ?? "") switch
        {
            "In Form" or "Top Rated" or "Rising" or "Ever-present" or "High Confidence" => HeroReasonType.HighestForm,
            "Most Talked" or "Trending" => HeroReasonType.MostTalked,
            "Top Scorer" => HeroReasonType.TopScorer,
            "Playmaker" => HeroReasonType.Playmaker,
            "Match Changer" => HeroReasonType.MatchChanger,
            "Fan Favourite" => HeroReasonType.FanFavourite,
            "Team Captain" or "Squad Member" => HeroReasonType.Captain,
            _ => HeroReasonType.HighestForm,
        };

        // Radar'ın ÜRETTİĞİ sinyalleri OKUR (üretmez): Importance + SourceConfidence.
        private async Task<(double? RadarConfidence, double? MatchImportance)> ReadRadarSignalsAsync(
            int matchId, CancellationToken ct)
        {
            try
            {
                var snap = await _matchIntel.GetByMatchIdAsync(matchId, ct);
                if (snap is null) return (null, null);

                double? importance = Math.Clamp(snap.ImportanceScore, 0, 100);
                double? radarConf = ParseSourceConfidence(snap.SignalsJson);
                return (radarConf, importance);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HERO-SELECT] Radar sinyalleri okunamadı (graceful)");
                return (null, null);
            }
        }

        private static double? ParseSourceConfidence(string signalsJson)
        {
            if (string.IsNullOrWhiteSpace(signalsJson)) return null;
            try
            {
                var rows = JsonSerializer.Deserialize<List<SignalRow>>(signalsJson);
                var conf = rows?.FirstOrDefault(r => r.Type == (int)MatchSignalType.SourceConfidence);
                return conf is null ? null : Math.Clamp(conf.Weight, 0, 100);
            }
            catch { return null; }
        }

        // HeroConfidence = ağırlıklandırılmış(hero skor, Radar Confidence, Importance).
        // Ağırlıklar HeroSelectionWeights'ten; eksik kaynak → mevcutlar üzerinden yeniden normalize.
        private int DeriveConfidence(
            PlayerIntelligence? home, PlayerIntelligence? away, double? radarConf, double? importance)
        {
            var parts = new List<(double Val, double W)>();

            var heroScores = new[] { home?.IntelligenceScore, away?.IntelligenceScore }
                .Where(s => s.HasValue).Select(s => (double)s!.Value).ToList();
            if (heroScores.Count > 0) parts.Add((heroScores.Average(), _weights.HeroScore));
            if (radarConf.HasValue) parts.Add((radarConf.Value, _weights.RadarConfidence));
            if (importance.HasValue) parts.Add((importance.Value, _weights.MatchImportance));

            var totalW = parts.Sum(p => p.W);
            if (totalW <= 0) return 0;

            var conf = parts.Sum(p => p.Val * p.W) / totalW;
            return (int)Math.Round(Math.Clamp(conf, 0, 100));
        }

        private sealed class SignalRow
        {
            [JsonPropertyName("Type")] public int Type { get; set; }
            [JsonPropertyName("Weight")] public double Weight { get; set; }
        }
    }
}
