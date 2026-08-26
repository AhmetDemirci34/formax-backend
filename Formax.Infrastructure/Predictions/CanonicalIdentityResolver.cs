using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Formax.Infrastructure.Predictions
{
    /// <summary>
    /// Üretim kimliği ile araştırma kimliği arasındaki köprü.
    ///
    /// Üretim <c>Matches</c>/<c>Teams</c> tabloları int kimlik kullanır; olasılık motoru ise
    /// canonical <c>FMXT…</c> takım kimliğiyle çalışır. Köprü zaten veride var:
    /// <c>Teams.ExternalTeamId</c> sağlayıcı (api-football) takım kimliğini tutuyor ve
    /// <c>FORMAX_HISTORICAL_TEAMS.csv</c> bunu canonical kimliğe bağlıyor.
    ///
    /// Eşlenemeyen takım UYDURULMAZ: eşleşme yoksa maç kapsam dışıdır ve tahmin üretilmez.
    /// </summary>
    public sealed class CanonicalIdentityResolver
    {
        private readonly Dictionary<string, string> _providerToCanonical;
        private readonly Dictionary<string, string> _canonicalToName;

        /// <summary>Kilitli müsabaka kapsamı — tarihsel veri setindeki 11 canonical müsabaka.</summary>
        private static readonly Dictionary<int, string> LeagueIdToCompetition = new()
        {
            [2] = "UEFA Champions League",
            [3] = "UEFA Europa League",
            [848] = "UEFA Conference League",
            [39] = "Premier League",
            [40] = "Championship",
            [61] = "Ligue 1",
            [78] = "Bundesliga",
            [88] = "Eredivisie",
            [135] = "Serie A",
            [140] = "La Liga",
            [203] = "Süper Lig"
        };

        public int MappedTeamCount => _providerToCanonical.Count;

        public CanonicalIdentityResolver(string historicalTeamsCsvPath)
        {
            _providerToCanonical = new Dictionary<string, string>(StringComparer.Ordinal);
            _canonicalToName = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!File.Exists(historicalTeamsCsvPath))
                throw new FileNotFoundException($"canonical team map not found: {historicalTeamsCsvPath}");

            using var sr = new StreamReader(historicalTeamsCsvPath);
            var header = ParseLine(sr.ReadLine() ?? throw new InvalidOperationException("empty team csv"));
            if (header.Count > 0) header[0] = header[0].TrimStart('﻿');
            var ix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++) ix[header[i]] = i;

            string Col(List<string> f, string n) => ix.TryGetValue(n, out var i) && i < f.Count ? f[i] : string.Empty;

            while (sr.ReadLine() is { } line)
            {
                if (line.Length == 0) continue;
                var f = ParseLine(line);

                // Kimliği doğrulanmamış takım motora hiç girmez — araştırma hattındaki kuralın aynısı.
                if (!string.Equals(Col(f, "IdentityConfidence"), "CONFIRMED", StringComparison.OrdinalIgnoreCase))
                    continue;

                var canonical = Col(f, "CanonicalTeamId");
                var provider = Col(f, "ProviderTeamId");
                if (string.IsNullOrWhiteSpace(canonical) || string.IsNullOrWhiteSpace(provider)) continue;

                _providerToCanonical[provider] = canonical;
                _canonicalToName[canonical] = Col(f, "CanonicalTeamName");
            }
        }

        /// <summary>Üretim <c>Teams.ExternalTeamId</c> → canonical <c>FMXT…</c>. Eşleşme yoksa null.</summary>
        public string? ResolveTeam(string? externalTeamId)
            => string.IsNullOrWhiteSpace(externalTeamId) ? null
             : _providerToCanonical.TryGetValue(externalTeamId.Trim(), out var c) ? c : null;

        public string TeamName(string canonicalId)
            => _canonicalToName.TryGetValue(canonicalId, out var n) ? n : canonicalId;

        /// <summary>Üretim <c>Matches.LeagueId</c> → canonical müsabaka adı. Kapsam dışıysa null.</summary>
        public static string? ResolveCompetition(int leagueId)
            => LeagueIdToCompetition.TryGetValue(leagueId, out var c) ? c : null;

        /// <summary>
        /// CompetitionType — tarihsel veri setinin kendi kuralının birebir aynısı, uydurma değil.
        /// UEFA turnuvalarında türü sağlayıcının Round alanı belirler; yerel liglerde
        /// play-off turları DOMESTIC_PLAYOFF, kalanı DOMESTIC_LEAGUE.
        /// </summary>
        public static string ResolveCompetitionType(string competition, string? round)
        {
            var r = (round ?? string.Empty).Trim();

            if (competition.StartsWith("UEFA", StringComparison.Ordinal))
            {
                if (r.IndexOf("Qualifying Round", StringComparison.OrdinalIgnoreCase) >= 0
                 || r.IndexOf("Preliminary Round", StringComparison.OrdinalIgnoreCase) >= 0
                 || r.Equals("Round 1", StringComparison.OrdinalIgnoreCase))
                    return "UEFA_QUALIFIER";

                if (r.IndexOf("Play-off", StringComparison.OrdinalIgnoreCase) >= 0
                 || r.IndexOf("Playoff", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "UEFA_QUALIFICATION_PLAYOFF";

                return "UEFA_MAIN";
            }

            if (r.IndexOf("Semi-final", StringComparison.OrdinalIgnoreCase) >= 0
             || r.IndexOf("Semifinal", StringComparison.OrdinalIgnoreCase) >= 0
             || r.Equals("Final", StringComparison.OrdinalIgnoreCase)
             || r.Equals("Finals", StringComparison.OrdinalIgnoreCase))
                return "DOMESTIC_PLAYOFF";

            return "DOMESTIC_LEAGUE";
        }

        /// <summary>Sezon etiketi — tarihsel veri setiyle aynı biçim ("2025/26").</summary>
        public static string ResolveSeason(DateTime matchDateUtc)
        {
            var y = matchDateUtc.Month >= 6 ? matchDateUtc.Year : matchDateUtc.Year - 1;
            return $"{y}/{(y + 1) % 100:00}";
        }

        private static List<string> ParseLine(string line)
        {
            var fields = new List<string>();
            var sb = new System.Text.StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields;
        }
    }
}
