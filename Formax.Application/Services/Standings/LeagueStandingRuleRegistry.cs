using System.Collections.Generic;

namespace Formax.Application.Services.Standings
{
    /// <summary>Eşit puanlı takımlarda uygulanacak İLK ölçüt.</summary>
    public enum TieBreakKind
    {
        /// <summary>Genel averaj → atılan gol (Premier League, Bundesliga, Ligue 1, Eredivisie, UEFA lig aşaması).</summary>
        GoalDifference = 0,

        /// <summary>Aralarındaki maçlar (mini lig) → sonra genel averaj (La Liga, Serie A, Süper Lig).</summary>
        HeadToHead = 1
    }

    /// <summary>
    /// LİG BAZLI SIRALAMA KURALI KAYITLARI.
    ///
    /// NEDEN AÇIKÇA TANIMLI: eşit puanlı takımlarda her lig aynı ölçütü kullanmaz. Genel
    /// averaj sıralaması İspanya, İtalya ve Türkiye'de RESMÎ DEĞİLDİR (önce ikili maçlar
    /// bakılır). Kayıtta olmayan bir lig için sıra "resmî" diye SUNULMAZ: eşitlik varsa
    /// snapshot <c>IsProvisional</c> işaretlenir.
    ///
    /// H2H kuralı yalnız eşit takımların aralarındaki TÜM maçlar oynandığında uygulanabilir;
    /// aksi hâlde ligin kendi kuralı gereği genel averaja düşülür ve sıra geçici sayılır.
    /// </summary>
    public static class LeagueStandingRuleRegistry
    {
        public sealed record Rule(int LeagueId, string Name, TieBreakKind TieBreak, string RuleId);

        private static readonly Dictionary<int, Rule> Rules = new()
        {
            // Averaj öncelikli ligler
            [39]  = new(39,  "Premier League",          TieBreakKind.GoalDifference, "EN.PL.GD-GF"),
            [40]  = new(40,  "Championship",            TieBreakKind.GoalDifference, "EN.CH.GD-GF"),
            [78]  = new(78,  "Bundesliga",              TieBreakKind.GoalDifference, "DE.BL.GD-GF"),
            [61]  = new(61,  "Ligue 1",                 TieBreakKind.GoalDifference, "FR.L1.GD-GF"),
            [88]  = new(88,  "Eredivisie",              TieBreakKind.GoalDifference, "NL.ED.GD-GF"),
            [2]   = new(2,   "UEFA Champions League",   TieBreakKind.GoalDifference, "UEFA.LP.GD-GF"),
            [3]   = new(3,   "UEFA Europa League",      TieBreakKind.GoalDifference, "UEFA.LP.GD-GF"),
            [848] = new(848, "UEFA Conference League",  TieBreakKind.GoalDifference, "UEFA.LP.GD-GF"),

            // İkili maç (head-to-head) öncelikli ligler
            [140] = new(140, "La Liga",                 TieBreakKind.HeadToHead,     "ES.LL.H2H-GD"),
            [135] = new(135, "Serie A",                 TieBreakKind.HeadToHead,     "IT.SA.H2H-GD"),
            [203] = new(203, "Süper Lig",               TieBreakKind.HeadToHead,     "TR.SL.H2H-GD"),
        };

        /// <summary>Kayıtlı kural; yoksa null → sıralama resmî sayılmaz.</summary>
        public static Rule? For(int leagueId) => Rules.TryGetValue(leagueId, out var r) ? r : null;

        /// <summary>Kayıtta olmayan ligler için kullanılan tanım (sıra geçici işaretlenir).</summary>
        public const string UnsupportedRuleId = "Unsupported.PointsGoalDiff.Provisional";

        public static IReadOnlyCollection<Rule> All => Rules.Values;
    }
}
