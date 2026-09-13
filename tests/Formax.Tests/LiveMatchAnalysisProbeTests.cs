using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.Services.MatchAnalysis;
using Formax.Domain.Constants;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.MatchAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Formax.Tests;

/// <summary>
/// CANLI ÖLÇÜM — varsayılan koşuda ATLANIR (<c>FORMAX_LIVE_ANALYSIS=1</c>). Gerçek DB'yi SALT
/// OKUR, hiçbir şey yazmaz; yaklaşan gerçek maçlar için deterministik analiz üretip doğrulayıcı,
/// tekrar ve benzerlik ölçümlerini yazdırır. Üretim girdisi maç detayı yolundan gelir; burada
/// aynı kural (aynı sezon + aynı lig + Finished + kickoff öncesi) doğrudan sorgulanır.
/// </summary>
public class LiveMatchAnalysisProbeTests
{
    private readonly ITestOutputHelper _out;
    public LiveMatchAnalysisProbeTests(ITestOutputHelper output) => _out = output;

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir, "Formax.slnx"))) dir = System.IO.Path.GetDirectoryName(dir);
        return dir!;
    }

    [SkippableFact]
    public async System.Threading.Tasks.Task Canli_GercekMaclar_AnalizOlcumu()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("FORMAX_LIVE_ANALYSIS") == "1", "FORMAX_LIVE_ANALYSIS=1 değil");
        var cs = Environment.GetEnvironmentVariable("FORMAX_DB") ??
                 "Server=localhost\\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True";
        using var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>().UseSqlServer(cs).Options);

        var leagues = new[] { 39, 40, 140, 135, 78, 61, 203, 88 };
        var now = DateTime.UtcNow;
        var seasonStart = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var upcoming = db.Matches.AsNoTracking().Include(m => m.HomeTeam).Include(m => m.AwayTeam)
            .Where(m => m.MatchDate > now && m.MatchDate < now.AddHours(96) && m.Status == MatchStatuses.NotStarted && leagues.Contains(m.LeagueId))
            .OrderBy(m => m.MatchDate).Take(40).ToList();

        List<TeamMatchFact> Facts(int teamId, int leagueId, DateTime kickoff) =>
            db.Matches.AsNoTracking()
                .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.LeagueId == leagueId
                            && m.Status == MatchStatuses.Finished && m.MatchDate >= seasonStart && m.MatchDate < kickoff)
                .OrderByDescending(m => m.MatchDate).Take(20).ToList()
                .Select(m => new TeamMatchFact(m.Id, m.MatchDate, m.HomeTeamId == teamId,
                    m.HomeTeamId == teamId ? m.HomeScore : m.AwayScore, m.HomeTeamId == teamId ? m.AwayScore : m.HomeScore))
                .ToList();

        // İsteğe bağlı LLM çevirisi (ilk N maç): FORMAX_LIVE_ANALYSIS_LLM=N
        var llmN = int.TryParse(Environment.GetEnvironmentVariable("FORMAX_LIVE_ANALYSIS_LLM"), out var ln) ? ln : 0;
        using var appsettings = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "Formax.API", "appsettings.json")),
            new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var llmSection = appsettings.RootElement.GetProperty("Llm");
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Provider"] = llmSection.GetProperty("Provider").GetString(),
                ["Llm:Endpoint"] = llmSection.GetProperty("Endpoint").GetString(),
                ["Llm:Model"] = llmSection.GetProperty("Model").GetString(),
                ["Llm:TimeoutSeconds"] = "120",
                ["Llm:ApiKey"] = Environment.GetEnvironmentVariable("Llm__ApiKey")
            }).Build();
        var llm = new Formax.Infrastructure.AI.LLM.HttpLLMClient(new System.Net.Http.HttpClient(), config,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Formax.Infrastructure.AI.LLM.HttpLLMClient>.Instance);
        int llmAccepted = 0, llmRejected = 0, llmDocs = 0;
        var accepted = new List<(int Id, string Flat, MatchAnalysisDocument Doc)>();
        int rejected = 0, empty = 0;
        var reasons = new Dictionary<string, int>();
        foreach (var m in upcoming)
        {
            var input = new AnalysisInput(m.Id, m.HomeTeam!.Name, m.AwayTeam!.Name, m.MatchDate, m.League,
                Facts(m.HomeTeamId, m.LeagueId, m.MatchDate), Facts(m.AwayTeamId, m.LeagueId, m.MatchDate));
            var ev = MatchEvidenceBuilder.Build(input);
            var seen = new HashSet<string>(accepted.SelectMany(a => a.Doc.AllSentences().Where(s => !ReferenceEquals(s, a.Doc.Uncertainty)).Select(s => s.Text)));
            var rej = new List<(string, string)>();
            MatchAnalysisDocument Prep(int v)
            {
                var r = MatchAnalysisValidator.Validate(MatchAnalysisComposer.Compose(input, ev, v), ev, input.HomeName, input.AwayName);
                rej.AddRange(r.Rejected);
                return MatchAnalysisGenerator.DropRepeated(r.Accepted, seen, rej);
            }
            var doc = Prep(0);
            var flat = AnalysisSimilarity.Flatten(doc);
            var sim = accepted.Count == 0 ? 0 : accepted.Max(a => AnalysisSimilarity.Jaccard(flat, a.Flat));
            var gen = "v0";
            if (sim > AnalysisSimilarity.MaxAllowed)
            {
                var d1 = Prep(1); var f1 = AnalysisSimilarity.Flatten(d1);
                var s1 = accepted.Count == 0 ? 0 : accepted.Max(a => AnalysisSimilarity.Jaccard(f1, a.Flat));
                if (s1 < sim) { doc = d1; flat = f1; sim = s1; gen = "v1"; }
            }
            if (llmDocs < llmN && !doc.IsEmpty)
            {
                llmDocs++;
                var before = rej.Count;
                var polished = await MatchAnalysisVerbalizer.VerbalizeAsync(llm, doc, ev, input.HomeName, input.AwayName, rej,
                    TimeSpan.FromSeconds(120), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, default);
                var llmRej = rej.Count - before;
                llmRejected += llmRej;
                if (polished != null)
                {
                    var changed = polished.AllSentences().Zip(doc.AllSentences()).Count(p => p.First.Text != p.Second.Text);
                    llmAccepted += changed;
                    doc = polished; flat = AnalysisSimilarity.Flatten(doc);
                    gen = $"llm(değişen={changed},ret={llmRej})";
                }
                else gen = "llm(boş cevap)";
            }
            rejected += rej.Count;
            foreach (var (_, reason) in rej) reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
            if (doc.IsEmpty) { empty++; continue; }
            accepted.Add((m.Id, flat, doc));
            _out.WriteLine($"\n#{m.Id} [{m.League}] {m.HomeTeam.Name}–{m.AwayTeam.Name} sim={sim:0.000} {gen} rej={rej.Count}");
            _out.WriteLine("  NEDEN: " + string.Join(" | ", doc.WhyWatch.Select(s => s.Text)));
            _out.WriteLine("  KİLİT: " + string.Join(" | ", doc.KeyBattle.Select(s => s.Text)));
            _out.WriteLine("  BELİRSİZLİK: " + doc.Uncertainty?.Text);
            foreach (var sc in doc.Scenarios.Take(4)) _out.WriteLine($"  [{sc.Market}] + {sc.Support?.Text} / − {sc.Risk?.Text}");
            foreach (var r in rej.Take(8)) _out.WriteLine($"  RET {r.Item2}: {r.Item1}");
        }

        double pairMax = 0;
        for (var i = 0; i < accepted.Count; i++)
            for (var j = i + 1; j < accepted.Count; j++)
                pairMax = Math.Max(pairMax, AnalysisSimilarity.Jaccard(accepted[i].Flat, accepted[j].Flat));
        var repeated = accepted.SelectMany(a => a.Doc.AllSentences().Where(s => !ReferenceEquals(s, a.Doc.Uncertainty)).Select(s => (s.Text, a.Id)))
            .GroupBy(x => x.Text).Count(g => g.Select(x => x.Id).Distinct().Count() > 1);

        _out.WriteLine($"\nÖZET: maç={upcoming.Count} analiz={accepted.Count} boş={empty} lig={upcoming.Select(u => u.League).Distinct().Count()} " +
                       $"ret={rejected} ({string.Join(", ", reasons.Select(kv => kv.Key + "=" + kv.Value))}) enYüksekÇiftBenzerlik={pairMax:0.000} tekrarCümle={repeated} " +
                       $"teknik={accepted.Count(a => MatchAnalysisValidator.HasTechnicalText(a.Flat))} yasak={accepted.Count(a => MatchAnalysisValidator.HasForbiddenPhrase(a.Flat))} " +
                       $"llmBelge={llmDocs} llmKabulEdilenCümle={llmAccepted} llmReddedilenCümle={llmRejected} " +
                       $"kanıtsız={accepted.Sum(a => a.Doc.AllSentences().Count(s => s.EvidenceKeys.Count == 0))}");
    }
}
