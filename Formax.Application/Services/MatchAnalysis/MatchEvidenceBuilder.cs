using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Formax.Application.Services.MatchAnalysis
{
    /// <summary>
    /// KANIT ÜRETİCİ — bütün sayılar BURADA, deterministik hesaplanır. LLM ve metin katmanı
    /// sayı hesaplamaz; yalnız bu kanıtlardaki değerleri yazar.
    ///
    /// FORM KURALI: takım başına en fazla son <see cref="MaxFormMatches"/> uygun maç (aynı sezon,
    /// aynı lig, kesin sonuç, kickoff öncesi). Örneklem büyüklüğü (n) her kanıtta taşınır ve
    /// metin katmanı kuralını ona göre uygular (0 / 1–2 / 3–4 / 5).
    /// </summary>
    public static class MatchEvidenceBuilder
    {
        public const int MaxFormMatches = 5;

        public static string FormKey(string side) => $"form:{side}";
        public static string VenueKey(string side) => side == "home" ? "venue:home:home" : "venue:away:away";
        public static string StandingKey(string side) => $"standing:{side}";
        public static string RestKey(string side) => $"rest:{side}";
        public static string LineupKey(string side) => $"lineup:{side}";
        public static string ShotsKey(string side) => $"shots:{side}";

        public static IReadOnlyList<EvidenceItem> Build(AnalysisInput input)
        {
            var list = new List<EvidenceItem>();

            AddTeam(list, "home", input.HomeMatches, input.KickoffUtc, venueIsHome: true,
                input.HomeStanding, input.StandingsComplete, input.HomeLineup, input.HomePreviousMatchUtc);
            AddTeam(list, "away", input.AwayMatches, input.KickoffUtc, venueIsHome: false,
                input.AwayStanding, input.StandingsComplete, input.AwayLineup, input.AwayPreviousMatchUtc);

            // Gol sınırları (tanım, veri değil): cümlede geçen "2,5" ve "3 gol" bu kanıta dayanır.
            foreach (var (label, line, goals) in new[] { ("1.5", 1.5, 2.0), ("2.5", 2.5, 3.0), ("3.5", 3.5, 4.0) })
                list.Add(new EvidenceItem(MatchAnalysisComposer.LineKey(label), "MarketLine",
                    new Dictionary<string, double> { ["line"] = line, ["goals"] = goals }));

            return list;
        }

        private static void AddTeam(
            List<EvidenceItem> list, string side, IReadOnlyList<TeamMatchFact> matches, DateTime kickoffUtc,
            bool venueIsHome, StandingFact? standing, bool standingsComplete, LineupFact? lineup, DateTime? previousUtc)
        {
            // Güvenlik ağı: çağıran yanlış liste verse bile kickoff sonrası maç forma giremez.
            var eligible = matches.Where(m => m.DateUtc < kickoffUtc).OrderByDescending(m => m.DateUtc).ToList();

            var form = eligible.Take(MaxFormMatches).ToList();
            list.Add(new EvidenceItem(FormKey(side), "Form", Stats(form)));

            var venue = eligible.Where(m => m.IsHome == venueIsHome).Take(MaxFormMatches).ToList();
            list.Add(new EvidenceItem(VenueKey(side), "Venue", Stats(venue)));

            // Şut kanıtı yalnız formdaki HER maçta şut ve isabetli şut ölçülmüşse — kısmi veri ortalama ÜRETMEZ.
            if (form.Count > 0 && form.All(m => m.Shots.HasValue && m.ShotsOnTarget.HasValue))
                list.Add(new EvidenceItem(ShotsKey(side), "Shots", new Dictionary<string, double>
                {
                    ["n"] = form.Count,
                    ["shotsAvg"] = Round1((double)form.Sum(m => m.Shots!.Value) / form.Count),
                    ["onTargetAvg"] = Round1((double)form.Sum(m => m.ShotsOnTarget!.Value) / form.Count)
                }));

            if (standing != null && standingsComplete && standing.Played > 0 && standing.Position > 0)
                list.Add(new EvidenceItem(StandingKey(side), "Standing", new Dictionary<string, double>
                {
                    ["pos"] = standing.Position,
                    ["pts"] = standing.Points,
                    ["played"] = standing.Played
                }));

            if (previousUtc is DateTime prev && prev < kickoffUtc)
            {
                var days = (int)Math.Floor((kickoffUtc - prev).TotalDays);
                if (days is >= 1 and <= 30)
                    list.Add(new EvidenceItem(RestKey(side), "Rest", new Dictionary<string, double> { ["days"] = days }));
            }

            if (lineup != null && lineup.Starters == 11)
            {
                var texts = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(lineup.Formation)) texts["formation"] = lineup.Formation!.Trim();
                if (!string.IsNullOrWhiteSpace(lineup.Coach)) texts["coach"] = lineup.Coach!.Trim();
                list.Add(new EvidenceItem(LineupKey(side), "Lineup",
                    new Dictionary<string, double> { ["starters"] = 11 }, texts));
            }
        }

        public static Dictionary<string, double> Stats(IReadOnlyList<TeamMatchFact> m)
        {
            var n = m.Count;
            var d = new Dictionary<string, double>
            {
                ["n"] = n,
                ["w"] = m.Count(x => x.GoalsFor > x.GoalsAgainst),
                ["d"] = m.Count(x => x.GoalsFor == x.GoalsAgainst),
                ["l"] = m.Count(x => x.GoalsFor < x.GoalsAgainst),
                ["gf"] = m.Sum(x => x.GoalsFor),
                ["ga"] = m.Sum(x => x.GoalsAgainst),
                ["cs"] = m.Count(x => x.GoalsAgainst == 0),
                ["fts"] = m.Count(x => x.GoalsFor == 0),
                ["btts"] = m.Count(x => x.GoalsFor > 0 && x.GoalsAgainst > 0),
                ["over15"] = m.Count(x => x.GoalsFor + x.GoalsAgainst >= 2),
                ["over25"] = m.Count(x => x.GoalsFor + x.GoalsAgainst >= 3),
                ["over35"] = m.Count(x => x.GoalsFor + x.GoalsAgainst >= 4)
            };
            // Türetilmiş sayımlar da BURADA hesaplanır; metin katmanı toplama/çıkarma yapmaz.
            d["wd"] = d["w"] + d["d"];
            d["wl"] = d["w"] + d["l"];
            d["under15"] = n - d["over15"];
            d["under25"] = n - d["over25"];
            d["under35"] = n - d["over35"];
            if (n > 0)
            {
                d["gfAvg"] = Round1((double)m.Sum(x => x.GoalsFor) / n);
                d["gaAvg"] = Round1((double)m.Sum(x => x.GoalsAgainst) / n);
                d["totalAvg"] = Round1((double)m.Sum(x => x.GoalsFor + x.GoalsAgainst) / n);
            }
            return d;
        }

        public static double Round1(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

        /// <summary>Türkçe sayı yazımı: tam sayı "3", ondalık "1,5".</summary>
        public static string Num(double v)
            => Math.Abs(v - Math.Round(v)) < 1e-9
                ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',');

        public static EvidenceItem? Find(IReadOnlyList<EvidenceItem> evidence, string key)
            => evidence.FirstOrDefault(e => e.Key == key);
    }
}
