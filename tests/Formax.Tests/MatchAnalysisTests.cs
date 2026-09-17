using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Formax.Application.AI.LLM;
using Formax.Application.AI.Radar;
using Formax.Application.Services.MatchAnalysis;
using Formax.Application.UseCases;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Infrastructure.Data;
using Formax.Infrastructure.MatchAnalysis;
using Formax.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KANITA DAYALI AI MAÇ ANALİZİ — sayılar backend'de, her cümle kanıt anahtarlı, doğrulayıcı
/// boş/uydurma/teknik cümleyi reddeder, tekrar engellenir, sayfa açılışı LLM üretmez.
/// </summary>
public class MatchAnalysisTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 18, 0, 0, DateTimeKind.Utc);

    private static TeamMatchFact F(int daysAgo, bool home, int gf, int ga, int id = 0)
        => new(id == 0 ? 1000 + daysAgo : id, Kickoff.AddDays(-daysAgo), home, gf, ga);

    private static List<TeamMatchFact> Played(int n, bool alternateHome = true)
        => Enumerable.Range(1, n).Select(i => F(i * 7, alternateHome ? i % 2 == 1 : true, i % 3, 1)).ToList();

    private static AnalysisInput Input(List<TeamMatchFact> home, List<TeamMatchFact> away)
        => new(1, "Sevilla", "Valencia", Kickoff, "La Liga", home, away);

    private static (AnalysisInput In, IReadOnlyList<EvidenceItem> Ev, MatchAnalysisDocument Doc) Run(
        List<TeamMatchFact> home, List<TeamMatchFact> away)
    {
        var input = Input(home, away);
        var ev = MatchEvidenceBuilder.Build(input);
        var doc = MatchAnalysisValidator.Validate(MatchAnalysisComposer.Compose(input, ev), ev, "Sevilla", "Valencia").Accepted;
        return (input, ev, doc);
    }

    // ═══ SAYILAR BACKEND'DE ══════════════════════════════════════════════════

    [Fact]
    public void GBM_VeGoller_BackendKanitindanGelir_EnFazlaSon5_KickoffSonrasiGiremez()
    {
        var home = new List<TeamMatchFact>
        {
            F(3, true, 2, 0), F(10, false, 1, 1), F(17, true, 0, 2), F(24, false, 3, 1), F(31, true, 1, 0),
            F(38, false, 0, 4), // 6. maç — son 5'e girmez
            new(99, Kickoff.AddHours(2), true, 9, 0) // kickoff SONRASI — hiç girmez
        };
        var ev = MatchEvidenceBuilder.Build(Input(home, Played(3)));
        var form = MatchEvidenceBuilder.Find(ev, "form:home")!.Values;
        Assert.Equal(5, form["n"]);
        Assert.Equal((3, 1, 1), ((int)form["w"], (int)form["d"], (int)form["l"]));
        Assert.Equal(7, form["gf"]);
        Assert.Equal(4, form["ga"]);
        Assert.Equal(1.4, form["gfAvg"]);
        Assert.Equal(2, form["cs"]);

        var doc = MatchAnalysisComposer.Compose(Input(home, Played(3)), ev);
        Assert.Contains(doc.AllSentences(), s => s.Text.Contains("Sevilla") && s.EvidenceKeys.Count > 0);
    }

    // ═══ 0 / 1–2 / 3–4 / 5 MAÇ KURALLARI ═════════════════════════════════════

    [Fact]
    public void SifirMac_GenellemeYok_YalnizDurustNot()
    {
        var (_, _, doc) = Run(new(), Played(4));
        Assert.Empty(doc.KeyBattle);
        Assert.Empty(doc.WhyWatch);
        Assert.Empty(doc.Scenarios);
        Assert.Contains("form karşılaştırması yapılamıyor", doc.Uncertainty!.Text);
        Assert.Contains("Sevilla", doc.Uncertainty.Text);
    }

    [Fact]
    public void BirIkiMac_YalnizGercekToplamlar_OrtalamaYok()
    {
        var (_, _, doc) = Run(Played(2), Played(2));
        var all = string.Join(" ", doc.AllSentences().Select(s => s.Text));
        Assert.DoesNotContain("maç başına", all);
        Assert.DoesNotContain("ortalama", all);
        Assert.NotEmpty(doc.KeyBattle);
        Assert.Contains("Örneklem sınırlı", doc.Uncertainty!.Text);
    }

    [Fact]
    public void UcDortMac_SinirliOrneklemAcikcaBelirtilir()
    {
        var (_, _, doc) = Run(Played(4), Played(3));
        Assert.Equal("Örneklem sınırlı: Sevilla için 4, Valencia için 3 tamamlanmış lig maçı var.", doc.Uncertainty!.Text);
    }

    [Fact]
    public void BesMac_Son5UzerindenDegerlendirme_BelirsizlikNotuYok()
    {
        var home = Enumerable.Range(1, 5).Select(i => F(i * 4, true, 2, 1)).ToList();
        var away = Enumerable.Range(1, 5).Select(i => F(i * 4, false, 1, 2)).ToList();
        var (_, _, doc) = Run(home, away);
        Assert.Null(doc.Uncertainty);
        Assert.Contains(doc.KeyBattle, s => s.Text.Contains("iç sahada son 5 lig maçında maç başına 2 gol üretti"));
        Assert.Contains(doc.WhyWatch, s => s.Text == "Sevilla ligde son 5 maçında yenilmedi: 5 galibiyet.");
    }

    // ═══ DOĞRULAYICI ═════════════════════════════════════════════════════════

    private static readonly IReadOnlyList<EvidenceItem> Ev = MatchEvidenceBuilder.Build(Input(Played(4), Played(4)));
    private static readonly IReadOnlyDictionary<string, EvidenceItem> ByKey = Ev.ToDictionary(e => e.Key);

    private static string? Check(string text, params string[] keys)
        => MatchAnalysisValidator.Reason(new AnalysisSentence(text, keys), ByKey, "Sevilla", "Valencia", false, true);

    [Fact]
    public void KanitsizCumle_Reddedilir()
    {
        Assert.Equal("NoEvidence", Check("Sevilla güçlü görünüyor."));
        Assert.Equal("UnknownEvidence", Check("Sevilla güçlü görünüyor.", "vibes:home"));
    }

    [Theory]
    [InlineData("Sevilla ev sahibi olarak Valencia'yı ağırlıyor.")]
    [InlineData("Sevilla maçı öncesi maç çevresinde konuşulacak gelişmeler var.")]
    [InlineData("Sevilla için hücum tarafı savunmadan daha güçlü görünüyor.")]
    [InlineData("Valencia için savunma istikrarı düşük.")]
    [InlineData("Sevilla maçlarında geçmiş maçlar gollü bu yönü destekliyor.")]
    [InlineData("Sevilla kesin kazanır, bunu oyna.")]
    public void YasakBosCumleler_Reddedilir(string text)
        => Assert.Equal("ForbiddenPhrase", Check(text, "form:home"));

    [Fact]
    public void KanittaOlmayanSayi_Reddedilir_TeknikMetinReddedilir_TakimAdiZorunlu()
    {
        Assert.Equal("NumberNotInEvidence", Check("Sevilla bu sezon 17 gol attı.", "form:home"));
        Assert.Equal("TechnicalText", Check("Sevilla O4 G2 B1 M1 AG 6 YG 3", "form:home"));
        Assert.Equal("MissingTeamName", Check("Ev sahibi bu sezon 4 maç oynadı.", "form:home"));
        Assert.Null(Check("Sevilla bu sezon oynadığı 4 lig maçında oynadı.", "form:home"));
        // "oynadığı" içinde "oyna" geçmesi yasak kalıp sayılmaz (sözcük sınırı).
        Assert.Null(Check("Sevilla bu sezon oynadığı 4 lig maçında sahaya çıktı.", "form:home"));
    }

    [Fact]
    public void GercekMaclarUzerindeUretilenHerCumle_Kanitli_TeknikVeYasakIcermez()
    {
        foreach (var (h, a) in new[] { (0, 3), (1, 2), (3, 4), (5, 5), (2, 5) })
        {
            var (_, _, doc) = Run(Played(h), Played(a));
            foreach (var s in doc.AllSentences())
            {
                Assert.NotEmpty(s.EvidenceKeys);
                Assert.False(MatchAnalysisValidator.HasTechnicalText(s.Text), s.Text);
                Assert.False(MatchAnalysisValidator.HasForbiddenPhrase(s.Text), s.Text);
            }
            // Aynı genel cümle bütün marketlerde kullanılmaz.
            var reasons = doc.Scenarios.SelectMany(r => new[] { r.Support?.Text }).Where(t => t != null).ToList();
            Assert.True(reasons.Count <= 1 || reasons.Distinct().Count() > 1);
        }
    }

    // ═══ TEKRAR / BENZERLİK ENGELİ ═══════════════════════════════════════════

    [Fact]
    public void AyniMetinFarkliMaclarda_KabulEdilmez_OrneklemUyarisiIstisna()
    {
        var (_, _, doc) = Run(Played(4), Played(4));
        var seen = new HashSet<string>(doc.AllSentences().Select(s => s.Text));
        var rejections = new List<(string, string)>();
        var second = MatchAnalysisGenerator.DropRepeated(doc, seen, rejections);
        Assert.Empty(second.KeyBattle);
        Assert.Empty(second.Scenarios);
        Assert.NotNull(second.Uncertainty); // zorunlu örneklem uyarısı tekrar edebilir
        Assert.All(rejections, r => Assert.Equal("RepeatedFromOtherMatch", r.Item2));

        var flat = AnalysisSimilarity.Flatten(doc);
        Assert.Equal(1.0, AnalysisSimilarity.Jaccard(flat, flat));
        Assert.True(AnalysisSimilarity.Jaccard(flat, "Tamamen başka bir metin burada yazıyor") < 0.1);
    }

    [Fact]
    public void LlmCevirisi_SayiBirlestiremez_TakimAdiDusuremez()
    {
        const string original = "Le Mans maçlarında ortalama 3,7, Lens maçlarında ortalama 3 gol çıktı.";
        Assert.Equal("NumbersChanged", MatchAnalysisVerbalizer.Fidelity(original, "Le Mans ve Lens maçlarında ortalama 3,7 gol çıktı.", "Le Mans", "Lens"));
        Assert.Equal("TeamNameDropped", MatchAnalysisVerbalizer.Fidelity(original, "Le Mans maçlarında ortalama 3,7, rakibinde ortalama 3 gol çıktı.", "Le Mans", "Lens"));
        Assert.Null(MatchAnalysisVerbalizer.Fidelity(original, "Le Mans maçlarında ortalama 3,7 gol görülürken Lens maçlarında bu sayı 3 oldu.", "Le Mans", "Lens"));
    }

    // ═══ SEZON / LİG / DURUM KAPSAMI ═════════════════════════════════════════

    [Fact]
    public void FormListesi_OncekiSezon_Kupa_Hazirlik_Canli_Gelecek_Disarida()
    {
        var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"analysis-scope-{Guid.NewGuid():N}").Options);
        db.Teams.AddRange(new Team { Id = 1, Name = "Sevilla" }, new Team { Id = 2, Name = "Valencia" });
        var seasonStart = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        void M(int id, int league, DateTime date, string status)
            => db.Matches.Add(new Match { Id = id, LeagueId = league, MatchDate = date, Status = status, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 1, AwayScore = 0 });
        M(1, 140, Kickoff.AddDays(-7), MatchStatuses.Finished);     // ✓ aynı sezon, aynı lig
        M(2, 140, seasonStart.AddDays(-60), MatchStatuses.Finished); // önceki sezon
        M(3, 143, Kickoff.AddDays(-4), MatchStatuses.Finished);      // kupa (Copa del Rey)
        M(4, 667, Kickoff.AddDays(-20), MatchStatuses.Finished);     // hazırlık
        M(5, 140, Kickoff.AddHours(-1), MatchStatuses.Live);         // canlı
        M(6, 140, Kickoff.AddDays(7), MatchStatuses.NotStarted);     // gelecek
        db.SaveChanges();

        var repo = new MatchReadRepository(db, new ConfigurationBuilder().Build());
        var list = repo.GetSeasonLeagueMatchesForTeam(1, 140, seasonStart, Kickoff);
        Assert.Equal(new[] { 1 }, list.Select(m => m.Id).ToArray());
    }

    // ═══ SAYFA AÇILIŞI VE BİTMİŞ MAÇ LLM ÜRETMEZ ═════════════════════════════

    [Fact]
    public void SayfaAcilisi_LlmUretmez_DetayYoluUreticiyeBagliDegil()
    {
        var ctor = typeof(GetMatchDetailAIContextUseCase).GetConstructors()
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();
        Assert.DoesNotContain(typeof(ILLMClient), ctor);
        Assert.DoesNotContain(typeof(RadarNarrativePipeline), ctor);
        Assert.DoesNotContain(typeof(MatchAnalysisGenerator), ctor);
        Assert.Contains(typeof(IMatchAnalysisReader), ctor);

        var readerCtor = typeof(MatchAnalysisReader).GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
        // 17.09.2026: okuyucu yalnız DB ve snapshot OKUYUCUSUNA bağlı (çelişki kapısı); üretici/LLM yok.
        Assert.Equal(new[] { typeof(FormaxDbContext), typeof(Formax.Application.Interfaces.IMatchOutcomeSnapshotReader) }, readerCtor);

        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx"))) dir = Path.GetDirectoryName(dir);
        var source = File.ReadAllText(Path.Combine(dir!, "Formax.Application", "UseCases", "GetMatchDetailAIContextUseCase.cs"));
        Assert.DoesNotContain("GenerateAsync(", source);
    }

    [Fact]
    public async Task BitmisMac_AnalizUretilmez_LlmCagrilmaz()
    {
        var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"analysis-finished-{Guid.NewGuid():N}").Options);
        db.Teams.AddRange(new Team { Id = 1, Name = "Sevilla" }, new Team { Id = 2, Name = "Valencia" });
        db.Matches.Add(new Match { Id = 104455, LeagueId = 140, MatchDate = Kickoff, Status = MatchStatuses.Finished, HomeTeamId = 1, AwayTeamId = 2 });
        db.SaveChanges();

        var llm = new CountingLlm();
        var generator = new MatchAnalysisGenerator(db, null!, llm, new ConfigurationBuilder().Build(),
            NullLogger<MatchAnalysisGenerator>.Instance);
        var r = await generator.GenerateAsync(104455, force: true, allowLlm: true);
        Assert.StartsWith("SkippedStatus", r.Outcome);
        Assert.Equal(0, llm.Calls);
        Assert.Empty(db.MatchAnalysisSnapshots);
    }

    [Fact]
    public async Task Okuyucu_HazirKayitYoksa_Hazirlaniyor_KanitAnahtariKullaniciyaTasinmaz()
    {
        var db = new FormaxDbContext(new DbContextOptionsBuilder<FormaxDbContext>()
            .UseInMemoryDatabase($"analysis-reader-{Guid.NewGuid():N}").Options);
        var reader = new MatchAnalysisReader(db);
        Assert.Equal("Preparing", (await reader.GetAsync(5)).Status);

        var (_, _, doc) = Run(Played(4), Played(4));
        db.MatchAnalysisSnapshots.Add(new MatchAnalysisSnapshot
        {
            MatchId = 5, Status = "Ready", InputHash = "h", Generator = "Deterministic", EvidenceJson = "[]",
            ContentJson = System.Text.Json.JsonSerializer.Serialize(doc), FlatText = AnalysisSimilarity.Flatten(doc),
            GeneratedAtUtc = Kickoff, KickoffUtc = Kickoff
        });
        db.SaveChanges();
        var dto = await reader.GetAsync(5);
        Assert.Equal("Ready", dto.Status);
        Assert.Equal(doc.KeyBattle.Select(s => s.Text), dto.KeyBattle);
        Assert.DoesNotContain("form:", System.Text.Json.JsonSerializer.Serialize(dto));
    }

    private sealed class CountingLlm : ILLMClient
    {
        public int Calls;
        public Task<string> GenerateAsync(string systemPrompt, string userPrompt, System.Threading.CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(string.Empty);
        }
    }
}
