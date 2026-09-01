using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Standings;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Standings
{
    /// <summary>
    /// PUAN DURUMU PROJEKSİYONU — tamamlanmış lig maçlarından tablo üretir.
    ///
    /// Girdi kesin kapsamdır (aynı lig + aynı sezon + Status=Finished); bu sınıf kapsam
    /// SORGUSU YAPMAZ, yalnız verilen maçları toplar. Puan: G=3, B=1, M=0.
    ///
    /// SIRALAMA: ligin kayıtlı kuralı (<see cref="LeagueStandingRuleRegistry"/>). Kural
    /// yoksa ya da head-to-head uygulanamıyorsa (takımlar aralarındaki maçları henüz
    /// tamamlamadıysa) eşit puanlı satırlar GEÇİCİ işaretlenir — sıra "resmî" diye
    /// sunulmaz. Deterministik olsun diye en son ölçüt takım adıdır.
    /// </summary>
    public static class StandingsProjector
    {
        public sealed record Result(
            List<LeagueStandingsRowDto> Rows,
            string RankingRuleId,
            bool IsProvisional,
            int MatchesIncluded,
            DateTime? LastIncludedMatchUtc);

        private const int FormWindow = 5;

        public static Result Project(int leagueId, IReadOnlyList<Match> settledMatches)
        {
            var rule = LeagueStandingRuleRegistry.For(leagueId);
            var acc = new Dictionary<int, Row>();

            // Maçlar tarih ARTAN işlenir → form dizisi doğal olarak eskiden yeniye kurulur.
            foreach (var m in settledMatches.OrderBy(x => x.MatchDate))
            {
                Apply(acc, m.HomeTeamId, m.HomeTeam?.Name, m.HomeScore, m.AwayScore);
                Apply(acc, m.AwayTeamId, m.AwayTeam?.Name, m.AwayScore, m.HomeScore);
            }

            var rows = acc.Values.Select(r => r.ToDto()).ToList();

            // Eşitlik grupları — sıralama ve "geçici mi" kararı burada verilir.
            var provisionalTeams = new HashSet<int>();
            var ordered = Order(rows, rule, settledMatches, provisionalTeams);

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Position = i + 1;
                ordered[i].IsProvisionalPosition = provisionalTeams.Contains(ordered[i].TeamId);
            }

            return new Result(
                ordered,
                rule?.RuleId ?? LeagueStandingRuleRegistry.UnsupportedRuleId,
                provisionalTeams.Count > 0,
                settledMatches.Count,
                settledMatches.Count == 0 ? null : settledMatches.Max(x => x.MatchDate));
        }

        private static void Apply(Dictionary<int, Row> acc, int teamId, string? teamName, int gf, int ga)
        {
            if (!acc.TryGetValue(teamId, out var row))
            {
                row = new Row { TeamId = teamId, TeamName = teamName ?? string.Empty };
                acc[teamId] = row;
            }
            if (row.TeamName.Length == 0 && !string.IsNullOrWhiteSpace(teamName)) row.TeamName = teamName!;

            row.Played++;
            row.GoalsFor += gf;
            row.GoalsAgainst += ga;

            if (gf > ga) { row.Won++; row.Form.Add('G'); }
            else if (gf == ga) { row.Drawn++; row.Form.Add('B'); }
            else { row.Lost++; row.Form.Add('M'); }
        }

        /// <summary>
        /// Puan → (lig kuralı) → averaj → atılan gol → ad. Eşit puanlı gruplarda ligin
        /// kuralı uygulanamıyorsa gruptaki takımlar GEÇİCİ olarak işaretlenir.
        /// </summary>
        private static List<LeagueStandingsRowDto> Order(
            List<LeagueStandingsRowDto> rows,
            LeagueStandingRuleRegistry.Rule? rule,
            IReadOnlyList<Match> matches,
            HashSet<int> provisionalTeams)
        {
            var result = new List<LeagueStandingsRowDto>(rows.Count);

            foreach (var group in rows.GroupBy(r => r.Points).OrderByDescending(g => g.Key))
            {
                var tied = group.ToList();

                if (tied.Count == 1)
                {
                    result.Add(tied[0]);
                    continue;
                }

                if (rule == null)
                {
                    // Kural bilinmiyor → sıra resmî değil.
                    foreach (var t in tied) provisionalTeams.Add(t.TeamId);
                    result.AddRange(ByGeneralGoals(tied));
                    continue;
                }

                if (rule.TieBreak == TieBreakKind.HeadToHead)
                {
                    if (TryHeadToHead(tied, matches, out var h2hOrdered))
                    {
                        result.AddRange(h2hOrdered);
                    }
                    else
                    {
                        // Aralarındaki maçlar henüz tamamlanmadı → ligin kendi kuralı gereği
                        // genel averaja düşülür, ama bu sıra KESİN DEĞİLDİR.
                        foreach (var t in tied) provisionalTeams.Add(t.TeamId);
                        result.AddRange(ByGeneralGoals(tied));
                    }
                    continue;
                }

                // Averaj öncelikli ligde genel averaj RESMÎ ölçüttür.
                var byGoals = ByGeneralGoals(tied);
                // Averaj ve atılan gol de eşitse ligin bir sonraki ölçütü (playoff/kura)
                // bizde yok → o satırlar geçici işaretlenir.
                foreach (var sub in byGoals.GroupBy(r => (r.GoalDifference, r.GoalsFor)).Where(g => g.Count() > 1))
                    foreach (var t in sub) provisionalTeams.Add(t.TeamId);

                result.AddRange(byGoals);
            }

            return result;
        }

        private static List<LeagueStandingsRowDto> ByGeneralGoals(List<LeagueStandingsRowDto> tied)
            => tied
                .OrderByDescending(r => r.GoalDifference)
                .ThenByDescending(r => r.GoalsFor)
                .ThenBy(r => r.TeamName, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Aralarındaki maçlardan mini lig. La Liga / Serie A / Süper Lig kuralı gereği
        /// bu ölçüt YALNIZ eşit takımlar aralarındaki bütün maçları oynadığında geçerlidir;
        /// aksi hâlde false döner ve çağıran genel averaja düşer.
        /// </summary>
        private static bool TryHeadToHead(
            List<LeagueStandingsRowDto> tied,
            IReadOnlyList<Match> matches,
            out List<LeagueStandingsRowDto> ordered)
        {
            ordered = tied;
            var ids = tied.Select(t => t.TeamId).ToHashSet();

            var mutual = matches
                .Where(m => ids.Contains(m.HomeTeamId) && ids.Contains(m.AwayTeamId))
                .ToList();

            // Her çift İKİ kez karşılaşmalı (çift devreli lig). Aksi hâlde kural uygulanamaz.
            var pairCount = ids.Count * (ids.Count - 1) / 2;
            var expected = pairCount * 2;
            if (mutual.Count < expected) return false;

            var mini = ids.ToDictionary(id => id, _ => new MiniRow());
            foreach (var m in mutual)
            {
                var h = mini[m.HomeTeamId];
                var a = mini[m.AwayTeamId];
                h.GoalsFor += m.HomeScore; h.GoalsAgainst += m.AwayScore;
                a.GoalsFor += m.AwayScore; a.GoalsAgainst += m.HomeScore;
                if (m.HomeScore > m.AwayScore) h.Points += 3;
                else if (m.HomeScore == m.AwayScore) { h.Points++; a.Points++; }
                else a.Points += 3;
            }

            ordered = tied
                .OrderByDescending(r => mini[r.TeamId].Points)
                .ThenByDescending(r => mini[r.TeamId].GoalsFor - mini[r.TeamId].GoalsAgainst)
                .ThenByDescending(r => r.GoalDifference)
                .ThenByDescending(r => r.GoalsFor)
                .ThenBy(r => r.TeamName, StringComparer.Ordinal)
                .ToList();

            return true;
        }

        private sealed class MiniRow
        {
            public int Points { get; set; }
            public int GoalsFor { get; set; }
            public int GoalsAgainst { get; set; }
        }

        private sealed class Row
        {
            public int TeamId { get; set; }
            public string TeamName { get; set; } = string.Empty;
            public int Played { get; set; }
            public int Won { get; set; }
            public int Drawn { get; set; }
            public int Lost { get; set; }
            public int GoalsFor { get; set; }
            public int GoalsAgainst { get; set; }
            public List<char> Form { get; } = new();

            public LeagueStandingsRowDto ToDto() => new()
            {
                TeamId = TeamId,
                TeamName = TeamName,
                Played = Played,
                Won = Won,
                Drawn = Drawn,
                Lost = Lost,
                GoalsFor = GoalsFor,
                GoalsAgainst = GoalsAgainst,
                GoalDifference = GoalsFor - GoalsAgainst,
                Points = Won * 3 + Drawn,
                Form = new string(Form.Skip(Math.Max(0, Form.Count - FormWindow)).ToArray())
            };
        }
    }
}
