using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Formax.Application.Services.News.Intelligence;
using Formax.Domain.Entities;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>Metne girebilecek tek olay — yalnız kayıtlı veriden.</summary>
    public sealed record PostMatchFactEvent(int Minute, int? ExtraMinute, string? Side, string Kind, string? Player);

    /// <summary>Metne girebilecek istatistik — kaynak vermediyse null.</summary>
    public sealed record PostMatchFactStats(int? PossessionHome, int? PossessionAway, int? ShotsOnTargetHome, int? ShotsOnTargetAway);

    public sealed record PostMatchSummaryInput(
        string HomeTeam, string AwayTeam, int HomeScore, int AwayScore,
        int? HalfTimeHome, int? HalfTimeAway,
        IReadOnlyList<PostMatchFactEvent> Events, PostMatchFactStats? Stats);

    public sealed record PostMatchSummaryResult(IReadOnlyList<string> Sentences, string InputHash, string EvidenceJson)
    {
        public string Text => string.Join("\n", Sentences);
    }

    /// <summary>
    /// BİTMİŞ MAÇ ANALİZ METNİ — 1–4 kısa cümle, YALNIZ doğrulanmış veriden.
    ///
    /// KURALLAR:
    ///  • Maç öncesi AI yorumu girdi DEĞİLDİR; form/güç/olasılık hiç okunmaz.
    ///  • Sebep-sonuç ya da taktik iddia yok: "baskı kurdu", "hak etti", "savunma çöktü" yazılmaz.
    ///    Yalnız sıra ve zaman: kim, kaçıncı dakikada, skor nasıl değişti.
    ///  • Olay listesi skorla ÇELİŞİYORSA gol çizelgesi yazılmaz (eksik/fazla olayla anlatı kurulmaz).
    ///  • Kendi kalesine golde sağlayıcının takım alanı belirsizdir; o maçta "belirleyici gol" ve
    ///    "geri dönüş" cümlesi KURULMAZ.
    ///  • LLM kullanılmaz; aynı girdi her zaman aynı metni üretir.
    /// </summary>
    public static class PostMatchSummaryComposer
    {
        public static class Kinds
        {
            public const string Goal = "Goal";
            public const string PenaltyGoal = "PenaltyGoal";
            public const string OwnGoal = "OwnGoal";
            public const string YellowCard = "YellowCard";
            public const string SecondYellow = "SecondYellow";
            public const string RedCard = "RedCard";
            /// <summary>Oyuna GİREN oyuncu (kanonik sözleşme: AssistName = giren).</summary>
            public const string SubIn = "SubIn";
        }

        private const int MaxSentences = 4;

        /// <summary>Kanonik olay kayıtlarını metin olgularına çevirir. Tanınmayan olay atlanır.</summary>
        public static IReadOnlyList<PostMatchFactEvent> FromRecords(
            IEnumerable<MatchEventRecord> records, string homeTeam, string awayTeam)
        {
            var list = new List<PostMatchFactEvent>();
            foreach (var r in records ?? Array.Empty<MatchEventRecord>())
            {
                var isSub = string.Equals(r.EventType, "subst", StringComparison.OrdinalIgnoreCase)
                            || (r.Detail ?? "").StartsWith("Substitution", StringComparison.OrdinalIgnoreCase);
                var kind = isSub ? Kinds.SubIn : KindOf(r.EventType, r.Detail);
                if (kind == null) continue;
                // Değişiklikte olgu GİREN oyuncudur (AssistName); gireni bilinmeyen değişiklik metne girmez.
                var who = isSub ? r.AssistName : r.PlayerName;
                if (isSub && string.IsNullOrWhiteSpace(who)) continue;
                var ev = new PostMatchFactEvent(r.Minute, r.ExtraMinute, SideOf(r.TeamName, homeTeam, awayTeam), kind,
                    string.IsNullOrWhiteSpace(who) ? null : who.Trim());

                // Yalnız BİREBİR aynı kayıt tekilleştirilir. Kanonik kayıtlar zaten olay kimliğiyle tekildir;
                // "≤2 dk kaymış tekrar" kuralı gerçek iki golü birleştiriyordu (ölçüldü: 15383, Mastantuono 29' ve 30').
                if (list.Contains(ev)) continue;
                list.Add(ev);
            }
            return list.OrderBy(Abs).ThenBy(e => e.ExtraMinute ?? 0).ToList();
        }

        public static PostMatchFactStats? StatsFrom(MatchTeamStatistic? home, MatchTeamStatistic? away)
        {
            if (home == null || away == null) return null;
            var s = new PostMatchFactStats(home.BallPossession, away.BallPossession, home.ShotsOnTarget, away.ShotsOnTarget);
            return s.PossessionHome == null && s.ShotsOnTargetHome == null ? null : s;
        }

        public static PostMatchSummaryResult Compose(PostMatchSummaryInput input)
        {
            var sentences = new List<string>();
            var home = input.HomeTeam.Trim();
            var away = input.AwayTeam.Trim();
            var hs = input.HomeScore;
            var aws = input.AwayScore;

            // 1) SONUÇ (+ ilk yarı, biliniyorsa).
            var result = hs > aws ? $"{home}, sahasında {away} karşısında {hs}-{aws} kazandı"
                       : hs < aws ? $"{away}, {home} deplasmanında {hs}-{aws} kazandı"
                       : hs == 0 ? $"{home} ile {away} golsüz berabere kaldı (0-0)"
                       : $"{home} ile {away} {hs}-{aws} berabere kaldı";
            if (input.HalfTimeHome is int hth && input.HalfTimeAway is int hta && hs + aws > 0)
                result += $"; ilk yarı {hth}-{hta} tamamlanmıştı";
            sentences.Add(result + ".");

            // 2) GOLLER — yalnız olay listesi skorla tutarlıysa.
            var goals = input.Events.Where(e => e.Kind is Kinds.Goal or Kinds.PenaltyGoal or Kinds.OwnGoal).ToList();
            var ownGoals = goals.Count(g => g.Kind == Kinds.OwnGoal);
            var homeGoals = goals.Count(g => g.Kind != Kinds.OwnGoal && g.Side == "home");
            var awayGoals = goals.Count(g => g.Kind != Kinds.OwnGoal && g.Side == "away");
            var consistent = goals.Count == hs + aws && goals.Count > 0
                             && goals.All(g => g.Kind == Kinds.OwnGoal || g.Side != null)
                             && homeGoals <= hs && awayGoals <= aws;
            if (consistent)
                sentences.Add("Goller: " + string.Join(", ", goals.Select(g => GoalText(g, home, away))) + ".");

            // 3) SKORUN SEYRİ — taraflar kesin (kendi kalesine gol yok) ve sayılar birebir tutuyorsa.
            if (consistent && ownGoals == 0 && homeGoals == hs && awayGoals == aws)
            {
                var flow = FlowSentence(goals, home, away, hs, aws);
                if (flow != null) sentences.Add(flow);
            }

            // 4) EK OLGULAR — öncelik sırasıyla, cümle bütçesi dolana kadar:
            //    kırmızı kart → yedekten girip gol atan oyuncu → istatistik → sarı kart sayısı.
            var reds = input.Events.Where(e => e.Kind is Kinds.RedCard or Kinds.SecondYellow).ToList();
            if (sentences.Count < MaxSentences && reds.Count > 0)
            {
                sentences.Add("Kırmızı kart: " + string.Join(", ", reds.Select(r =>
                    $"{Minute(r)} {r.Player ?? "oyuncu adı kayıtlı değil"} ({TeamOf(r.Side, home, away) ?? "takım kayıtlı değil"}" +
                    (r.Kind == Kinds.SecondYellow ? ", ikinci sarıdan" : "") + ")")) + ".");
            }

            // Yedekten gol: aynı oyuncu (aynı taraf) önce oyuna girmiş, sonra gol atmış — sıra kayıttan okunur.
            var subScorers = goals
                .Where(g => g.Kind != Kinds.OwnGoal && g.Player != null)
                .Select(g => (Goal: g, Sub: input.Events.FirstOrDefault(s => s.Kind == Kinds.SubIn && s.Player == g.Player
                                                                            && s.Side == g.Side && Abs(s) <= Abs(g))))
                .Where(x => x.Sub != null)
                .GroupBy(x => x.Goal.Player).Select(grp => grp.First()).ToList();
            if (sentences.Count < MaxSentences && consistent && subScorers.Count > 0)
            {
                sentences.Add(string.Join("; ", subScorers.Select(x =>
                    $"{x.Goal.Player} {Minute(x.Sub!)} oyuna girdi ve {Minute(x.Goal)} gol attı")) + ".");
            }

            if (sentences.Count < MaxSentences && input.Stats is { } st)
            {
                var parts = new List<string>();
                if (st.ShotsOnTargetHome is int sh && st.ShotsOnTargetAway is int sa) parts.Add($"isabetli şut {sh}-{sa}");
                if (st.PossessionHome is int ph && st.PossessionAway is int pa) parts.Add($"topla oynama %{ph}-%{pa}");
                if (parts.Count > 0)
                    sentences.Add($"İstatistikte ({home}-{away} sırasıyla) " + string.Join(", ", parts) + ".");
            }

            var yellows = input.Events.Count(e => e.Kind == Kinds.YellowCard);
            if (sentences.Count < MaxSentences && yellows > 0 && input.Stats == null)
                sentences.Add($"Kayıtlı olaylarda {yellows} sarı kart var.");

            var evidence = JsonSerializer.Serialize(input);
            return new PostMatchSummaryResult(sentences.Take(MaxSentences).ToList(), Sha256(evidence), evidence);
        }

        /// <summary>
        /// Skorun seyri — yalnız sıradan çıkan olgular: galibin geriye düşüp düşmediği ve skoru
        /// belirleyen golün dakikası; beraberlikte son eşitlik golü.
        /// </summary>
        private static string? FlowSentence(List<PostMatchFactEvent> goals, string home, string away, int hs, int aws)
        {
            if (hs == aws)
            {
                int h = 0, a = 0;
                PostMatchFactEvent? lastEqualizer = null;
                foreach (var g in goals)
                {
                    if (g.Side == "home") h++; else a++;
                    if (h == a) lastEqualizer = g;
                }
                return lastEqualizer == null ? null
                    : $"Son eşitlik golü {Minute(lastEqualizer)} {Scorer(lastEqualizer)} ({TeamOf(lastEqualizer.Side, home, away)}).";
            }

            var winnerSide = hs > aws ? "home" : "away";
            var loserFinal = Math.Min(hs, aws);
            int w = 0, l = 0;
            var trailed = false;
            PostMatchFactEvent? decisive = null;
            foreach (var g in goals)
            {
                if (g.Side == winnerSide) { w++; if (w == loserFinal + 1) decisive = g; }
                else l++;
                if (l > w) trailed = true;
            }
            if (decisive == null) return null;
            // Fark 2+ ve galip hiç geriye düşmediyse "belirleyici gol" ilk golden ibarettir; gol
            // listesinin söylemediği bir şey söylemez (ölçüldü: Coventry 0-5 Brighton → 35').
            if (!trailed && Math.Abs(hs - aws) >= 2) return null;
            var winner = TeamOf(winnerSide, home, away);
            return trailed
                ? $"{winner} geriye düştüğü maçı çevirdi; skoru belirleyen gol {Minute(decisive)} {Scorer(decisive)}."
                : $"Skoru belirleyen gol {Minute(decisive)} {Scorer(decisive)} ({winner}).";
        }

        private static string GoalText(PostMatchFactEvent g, string home, string away)
        {
            if (g.Kind == Kinds.OwnGoal)
                return $"{Minute(g)} {(g.Player != null ? g.Player + " (kendi kalesine)" : "kendi kalesine gol")}";
            var team = TeamOf(g.Side, home, away);
            var penalty = g.Kind == Kinds.PenaltyGoal ? ", penaltı" : "";
            return g.Player != null ? $"{Minute(g)} {g.Player} ({team}{penalty})" : $"{Minute(g)} {team}{penalty}";
        }

        private static string Scorer(PostMatchFactEvent g) => g.Player ?? "oyuncu adı kayıtlı değil";

        private static string Minute(PostMatchFactEvent e)
            => e.ExtraMinute is int x && x > 0
                ? $"{e.Minute.ToString(CultureInfo.InvariantCulture)}+{x.ToString(CultureInfo.InvariantCulture)}'"
                : $"{e.Minute.ToString(CultureInfo.InvariantCulture)}'";

        private static int Abs(PostMatchFactEvent e) => e.Minute + (e.ExtraMinute ?? 0);

        private static string? TeamOf(string? side, string home, string away)
            => side == "home" ? home : side == "away" ? away : null;

        public static string? KindOf(string? eventType, string? detail)
        {
            var t = (eventType ?? string.Empty).Trim();
            var d = (detail ?? string.Empty).Trim();
            if (t.Equals("Goal", StringComparison.OrdinalIgnoreCase))
            {
                if (d.Contains("Missed", StringComparison.OrdinalIgnoreCase)) return null;
                if (d.Contains("Own", StringComparison.OrdinalIgnoreCase)) return Kinds.OwnGoal;
                if (d.Contains("Penalty", StringComparison.OrdinalIgnoreCase)) return Kinds.PenaltyGoal;
                return Kinds.Goal;
            }
            if (t.Equals("Card", StringComparison.OrdinalIgnoreCase))
            {
                if (d.Contains("Second", StringComparison.OrdinalIgnoreCase)) return Kinds.SecondYellow;
                if (d.Contains("Red", StringComparison.OrdinalIgnoreCase)) return Kinds.RedCard;
                if (d.Contains("Yellow", StringComparison.OrdinalIgnoreCase)) return Kinds.YellowCard;
            }
            return null;
        }

        /// <summary>Olayın takımı ev mi deplasman mı — ad eşleşmesi kesin değilse null.</summary>
        public static string? SideOf(string? teamName, string home, string away)
        {
            var t = NewsTextNormalizer.Fold(teamName);
            if (t.Length == 0) return null;
            var h = NewsTextNormalizer.Fold(home);
            var a = NewsTextNormalizer.Fold(away);
            var isHome = t == h || (h.Length > 0 && (t.Contains(h, StringComparison.Ordinal) || h.Contains(t, StringComparison.Ordinal)));
            var isAway = t == a || (a.Length > 0 && (t.Contains(a, StringComparison.Ordinal) || a.Contains(t, StringComparison.Ordinal)));
            return isHome == isAway ? null : isHome ? "home" : "away";
        }

        private static string Sha256(string s)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    }
}
