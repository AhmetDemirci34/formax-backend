using System;
using System.Collections.Generic;
using System.Linq;

namespace Formax.Application.Services.Lineups
{
    /// <summary>
    /// TEK TAKIM-MAÇ ARTIĞI — gerçek gol ile modelin O MAÇTAN ÖNCEKİ bilgiyle beklediği gol arasındaki
    /// log fark. Oyuncu etkisinin ÖĞRENİLDİĞİ tek gözlem budur; maç sonrası istatistik, oyuncu reytingi
    /// ya da sağlayıcı puanı KULLANILMAZ.
    ///
    ///   AttackResidual  = ln((attığı gol + c) / (beklenen attığı gol + c))    → pozitif: beklenenden çok attı
    ///   DefenceResidual = ln((yediği gol + c) / (beklenen yediği gol + c))    → pozitif: beklenenden çok yedi
    /// </summary>
    public readonly record struct TeamMatchResidual(
        int MatchId, DateTime KickoffUtc, int LeagueId, int TeamId, bool IsHome,
        double AttackResidual, double DefenceResidual);

    /// <summary>Tek oyuncunun öğrenilmiş etkisi (log-gol ölçeğinde, takım normuna GÖRE).</summary>
    public sealed record PlayerImpactEstimate(
        string PlayerKey,
        int TeamId,
        string Position,
        int Matches,
        double AttackImpact,
        double DefenceImpact,
        double Shrinkage,
        bool Sufficient)
    {
        public static PlayerImpactEstimate Zero(string key, int teamId, string position, int matches)
            => new(key, teamId, position, matches, 0, 0, 0, false);
    }

    /// <summary>Oyuncu etki modelinin ayarları — hepsi ÖNSEL güvenlik sınırıdır, test penceresine bakılarak seçilmez.</summary>
    public sealed class PlayerImpactParameters
    {
        /// <summary>Bu kadar başlangıcın ALTINDA etki tam olarak 0'dır (uydurma değer üretilmez).</summary>
        public int MinPlayerMatches { get; set; } = 8;

        /// <summary>Daraltma sabiti K: ağırlık = n / (n + K). Küçük örneklem lig/mevki ortalamasına (0) çekilir.</summary>
        public double ShrinkageMatches { get; set; } = 10;

        /// <summary>Artık hesabındaki Laplace yumuşatması (0 gol / 0 beklenti log'unu tanımlı kılar).</summary>
        public double ResidualSmoothing { get; set; } = 0.5;

        /// <summary>Tek oyuncunun tek kanaldaki etkisinin mutlak tavanı (log-gol ölçeği).</summary>
        public double MaxPlayerLogImpact { get; set; } = 0.15;

        /// <summary>Bir takımın tek kanaldaki (hücum ya da savunma) toplam etkisinin tavanı.</summary>
        public double MaxSideLogDelta { get; set; } = 0.06;

        /// <summary>Tek λ üzerindeki toplam kadro etkisinin son tavanı (hücum + rakip savunması).</summary>
        public double MaxLambdaLogDelta { get; set; } = 0.10;

        /// <summary>Bu eşiğin altındaki toplam düzeltme "değişiklik yok" sayılır (gürültü yayımlanmaz).</summary>
        public double MaterialChangeThreshold { get; set; } = 0.005;

        /// <summary>Takım normunun anlamlı olması için gereken en az takım-maç sayısı.</summary>
        public int MinTeamMatches { get; set; } = 6;

        // ── ABLASYON ANAHTARLARI (yalnız ölçüm için; üretimde üçü de açıktır) ──────────────

        /// <summary>false: küçük örneklem daraltması KAPALI (ham ortalama). Yalnız ablasyon ölçümü içindir.</summary>
        public bool ApplyShrinkage { get; set; } = true;

        /// <summary>false: mevki kanalı KAPALI — her oyuncu iki kanaldan yarım ağırlıkla okunur.</summary>
        public bool ApplyPositionChannel { get; set; } = true;

        /// <summary>false: "muhtemel yedeğin katkısı" ÇIKARILMAZ (ham başlangıç etkisi).</summary>
        public bool ApplyReplacement { get; set; } = true;

        public PlayerImpactParameters Clone() => (PlayerImpactParameters)MemberwiseClone();
    }

    /// <summary>
    /// OYUNCU ETKİ MODELİ — "oyuncu sahadayken takım kendi normundan ne kadar sapıyor?" sorusunun
    /// daraltılmış cevabı. Elle yazılmış "yıldız oyuncu +%10" kuralı YOKTUR.
    ///
    /// ÖĞRENME
    ///  1. Her takım-maç için artık: gerçek gol ile modelin o maçtan ÖNCEKİ bilgiyle beklediği golün log farkı.
    ///  2. Takım normu: takımın kadro gözlemi olan bütün maçlarındaki ortalama artık.
    ///  3. Oyuncu ham etkisi: oyuncunun İLK 11'de BAŞLADIĞI maçların ortalama artığı − takım normu.
    ///  4. Daraltma: n/(n+K) ile 0'a (lig/mevki ortalamasına) çekilir; n &lt; MinPlayerMatches ise etki TAM 0.
    ///  5. Mevki kanalı: kaleci/savunma yalnız SAVUNMA artığından, forvet yalnız HÜCUM artığından,
    ///     orta saha ikisinden yarım ağırlıkla okunur. Savunmacı gol/asistle ÖLÇÜLMEZ.
    ///
    /// SIZINTI GÜVENCESİ: model <see cref="BuildAsOf"/> ile kurulur ve yalnız verilen andan ÖNCE başlamış
    /// maçları görür. Bir maçın kendi sonucu, olayları ya da maç sonrası istatistiği kendi tahminine giremez.
    /// </summary>
    public sealed class PlayerImpactModel
    {
        private readonly Dictionary<string, PlayerImpactEstimate> _players;
        private readonly Dictionary<(int TeamId, string Position), double[]> _teamPositionPool;
        private readonly Dictionary<int, int> _teamMatches;

        public PlayerImpactParameters Parameters { get; }
        public DateTime CutoffUtc { get; }
        public int ObservedMatches { get; }
        public int ResolvedPlayers => _players.Count;
        public int SufficientPlayers => _players.Values.Count(p => p.Sufficient);

        private PlayerImpactModel(
            PlayerImpactParameters parameters,
            DateTime cutoffUtc,
            int observedMatches,
            Dictionary<string, PlayerImpactEstimate> players,
            Dictionary<(int, string), double[]> teamPositionPool,
            Dictionary<int, int> teamMatches)
        {
            Parameters = parameters;
            CutoffUtc = cutoffUtc;
            ObservedMatches = observedMatches;
            _players = players;
            _teamPositionPool = teamPositionPool;
            _teamMatches = teamMatches;
        }

        /// <summary>Hiç kadro geçmişi olmayan (ya da katman kapalı) durum: her oyuncunun etkisi 0.</summary>
        public static PlayerImpactModel Empty(PlayerImpactParameters? parameters = null)
            => new(parameters ?? new PlayerImpactParameters(), DateTime.MinValue, 0,
                   new Dictionary<string, PlayerImpactEstimate>(StringComparer.Ordinal),
                   new Dictionary<(int, string), double[]>(),
                   new Dictionary<int, int>());

        /// <summary>
        /// Modeli <paramref name="cutoffUtc"/> anına kadar bitmiş maçlardan kurar. Bu andan SONRA başlayan
        /// hiçbir maç görülmez — zamansal sızıntı yapısal olarak engellenir.
        /// </summary>
        public static PlayerImpactModel BuildAsOf(
            IEnumerable<MatchLineupObservation> lineups,
            IEnumerable<TeamMatchResidual> residuals,
            DateTime cutoffUtc,
            PlayerImpactParameters? parameters = null)
        {
            var p = parameters ?? new PlayerImpactParameters();
            var res = residuals.Where(r => r.KickoffUtc < cutoffUtc)
                .GroupBy(r => (r.MatchId, r.TeamId))
                .ToDictionary(g => g.Key, g => g.First());
            var obs = lineups.Where(l => l.KickoffUtc < cutoffUtc).ToList();

            // Takım normu — YALNIZ kadro gözlemi olan maçlardan (oyuncu ortalamasıyla aynı örnek uzayı).
            var teamSum = new Dictionary<int, (double Att, double Def, int N)>();
            foreach (var l in obs)
                foreach (var (teamId, _) in new[] { (l.HomeTeamId, true), (l.AwayTeamId, false) })
                {
                    if (!res.TryGetValue((l.MatchId, teamId), out var r)) continue;
                    var cur = teamSum.GetValueOrDefault(teamId);
                    teamSum[teamId] = (cur.Att + r.AttackResidual, cur.Def + r.DefenceResidual, cur.N + 1);
                }
            var teamNorm = teamSum.ToDictionary(
                k => k.Key,
                k => k.Value.N == 0 ? (0.0, 0.0) : (k.Value.Att / k.Value.N, k.Value.Def / k.Value.N));
            var teamMatches = teamSum.ToDictionary(k => k.Key, k => k.Value.N);

            // Oyuncu başlangıçları.
            var acc = new Dictionary<string, (int TeamId, string Position, double Att, double Def, int N)>(StringComparer.Ordinal);
            foreach (var l in obs)
            {
                foreach (var home in new[] { true, false })
                {
                    var teamId = home ? l.HomeTeamId : l.AwayTeamId;
                    if (!res.TryGetValue((l.MatchId, teamId), out var r)) continue;
                    foreach (var pl in l.Side(home).Where(x => x.Starter && x.Resolved))
                    {
                        var pos = LineupPositions.Normalize(pl.Position)!;
                        var cur = acc.GetValueOrDefault(pl.PlayerKey);
                        acc[pl.PlayerKey] = (teamId, pos, cur.Att + r.AttackResidual, cur.Def + r.DefenceResidual, cur.N + 1);
                    }
                }
            }

            var players = new Dictionary<string, PlayerImpactEstimate>(StringComparer.Ordinal);
            foreach (var (key, v) in acc)
            {
                var (normAtt, normDef) = teamNorm.GetValueOrDefault(v.TeamId, (0.0, 0.0));
                var teamN = teamMatches.GetValueOrDefault(v.TeamId);
                // Örneklem ya da takım normu yetersizse etki TAM 0 (uydurma değer yok).
                if (v.N < p.MinPlayerMatches || teamN < p.MinTeamMatches)
                {
                    players[key] = PlayerImpactEstimate.Zero(key, v.TeamId, v.Position, v.N);
                    continue;
                }
                var rawAtt = v.Att / v.N - normAtt;
                var rawDef = v.Def / v.N - normDef;
                var (chAtt, chDef) = p.ApplyPositionChannel ? LineupPositions.Channel(v.Position) : (0.5, 0.5);
                var w = p.ApplyShrinkage ? v.N / (v.N + p.ShrinkageMatches) : 1.0;
                var att = Clamp(w * chAtt * rawAtt, p.MaxPlayerLogImpact);
                var def = Clamp(w * chDef * rawDef, p.MaxPlayerLogImpact);
                players[key] = new PlayerImpactEstimate(key, v.TeamId, v.Position, v.N, att, def, w, true);
            }

            // Takım × mevki havuzu — "muhtemel yedeğin katkısı" buradan gelir.
            var pool = players.Values
                .Where(x => x.Sufficient)
                .GroupBy(x => (x.TeamId, x.Position))
                .ToDictionary(g => g.Key, g => new[] { g.Average(x => x.AttackImpact), g.Average(x => x.DefenceImpact), g.Count() });

            return new PlayerImpactModel(p, cutoffUtc, obs.Count, players, pool, teamMatches);
        }

        /// <summary>Oyuncunun öğrenilmiş etkisi; tanınmıyorsa 0 (unresolved).</summary>
        public PlayerImpactEstimate Impact(string playerKey)
            => _players.TryGetValue(playerKey, out var v) ? v : PlayerImpactEstimate.Zero(playerKey, 0, string.Empty, 0);

        /// <summary>
        /// MUHTEMEL YEDEĞİN KATKISI — takımın AYNI MEVKİDEKİ ölçülebilir oyuncularının ortalaması.
        /// Aranan oyuncu havuzdan çıkarılır (kendisiyle kıyaslanmaz). Havuz yoksa 0 döner ve
        /// <see cref="LineupReasonCodes.ReplacementBaselineUnknown"/> gerekçesi yazılır.
        /// </summary>
        public (double Attack, double Defence, int PoolSize) Replacement(int teamId, string position, string? excludeKey = null)
        {
            if (!_teamPositionPool.TryGetValue((teamId, position), out var v)) return (0, 0, 0);
            var n = (int)v[2];
            if (n == 0) return (0, 0, 0);
            if (excludeKey != null && _players.TryGetValue(excludeKey, out var self) && self.Sufficient
                && self.TeamId == teamId && self.Position == position)
            {
                if (n <= 1) return (0, 0, 0);
                return ((v[0] * n - self.AttackImpact) / (n - 1), (v[1] * n - self.DefenceImpact) / (n - 1), n - 1);
            }
            return (v[0], v[1], n);
        }

        public int TeamObservedMatches(int teamId) => _teamMatches.GetValueOrDefault(teamId);

        /// <summary>Takımın YETERLİ ÖRNEKLEMLİ (etkisi gerçekten öğrenilmiş) oyuncuları.</summary>
        public IEnumerable<PlayerImpactEstimate> SufficientFor(int teamId)
            => _players.Values.Where(x => x.Sufficient && x.TeamId == teamId);

        private static double Clamp(double v, double cap) => Math.Max(-cap, Math.Min(cap, v));

        /// <summary>
        /// ARTIK ÜRETİCİSİ — tarihsel maçın gerçek sonucu ile modelin o maçtan önceki beklentisinden
        /// iki takım-maç artığı. <paramref name="expectedHome"/>/<paramref name="expectedAway"/> maçtan
        /// ÖNCE hesaplanmış λ'lardır.
        /// </summary>
        public static (TeamMatchResidual Home, TeamMatchResidual Away) Residuals(
            int matchId, DateTime kickoffUtc, int leagueId, int homeTeamId, int awayTeamId,
            int homeGoals, int awayGoals, double expectedHome, double expectedAway, double smoothing = 0.5)
        {
            var c = Math.Max(1e-6, smoothing);
            double R(double actual, double expected) => Math.Log((actual + c) / (Math.Max(1e-6, expected) + c));
            var homeAtt = R(homeGoals, expectedHome);
            var homeDef = R(awayGoals, expectedAway);
            return (
                new TeamMatchResidual(matchId, kickoffUtc, leagueId, homeTeamId, true, homeAtt, homeDef),
                new TeamMatchResidual(matchId, kickoffUtc, leagueId, awayTeamId, false, homeDef, homeAtt));
        }
    }
}
