using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Formax.Application.Interfaces;
using Formax.Application.Services.Lineups;
using Formax.Application.Services.OfficialSources;
using Formax.Application.Services.Outcomes;
using Formax.Domain.Constants;
using Formax.Infrastructure.Lineups;
using Formax.Infrastructure.Outcomes;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// KADRO → OYUNCU ETKİSİ → TAHMİN. Görev şartnamesindeki 24 kabul testi.
///
/// Hiçbiri ağa çıkmaz. Oyuncu etkisi sentetik ama GERÇEKÇİ artıklardan öğrenilir: testler
/// katmanın davranışını (daraltma, tavan, mevki kanalı, sızıntı, değişmezlik) sınar, bir
/// veri kümesinin sonucunu değil.
/// </summary>
public class LineupImpactTests
{
    private static readonly DateTime Day0 = new(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc);

    private const int HomeTeam = 101;
    private const int AwayTeam = 202;

    private static readonly string[] Positions =
        { "G", "D", "D", "D", "D", "M", "M", "M", "F", "F", "F" };

    /// <summary>11 kişilik ilk 11 — ad öneki squad'ı, sıra mevkii belirler.</summary>
    private static List<LineupPlayerObservation> Xi(int teamId, string squad, bool bench = false)
        => Enumerable.Range(0, 11).Select(i => new LineupPlayerObservation(
            PlayerIdentity.Key(teamId, $"{squad}{i + 1}"), $"{squad}{i + 1}", Positions[i], i + 1, !bench)).ToList();

    private static MatchLineupObservation Obs(int matchId, int day, string homeSquad, string awaySquad,
        string status = "Verified", int leagueId = 135)
        => new(matchId, Day0.AddDays(day), leagueId, HomeTeam, AwayTeam,
               Xi(HomeTeam, homeSquad), Xi(AwayTeam, awaySquad), status, "seriea-sdp", Day0.AddDays(day));

    /// <summary>Takım-maç artıkları — hücum/savunma log farkı doğrudan verilir.</summary>
    private static IEnumerable<TeamMatchResidual> Res(int matchId, int day,
        double homeAtt, double homeDef, double awayAtt, double awayDef, int leagueId = 135)
    {
        yield return new TeamMatchResidual(matchId, Day0.AddDays(day), leagueId, HomeTeam, true, homeAtt, homeDef);
        yield return new TeamMatchResidual(matchId, Day0.AddDays(day), leagueId, AwayTeam, false, awayAtt, awayDef);
    }

    /// <summary>
    /// A kadrosu takımı ileri taşıyor, B kadrosu geriye: n maç A, n maç B. Takım normu 0'dır,
    /// böylece oyuncu etkisi tam olarak "kendi kadrosunun sapması" kadardır.
    /// </summary>
    private static (List<MatchLineupObservation> Obs, List<TeamMatchResidual> Res) Split(
        int perSquad, double magnitude = 1.0, string squadB = "B")
    {
        var obs = new List<MatchLineupObservation>();
        var res = new List<TeamMatchResidual>();
        var id = 1;
        for (var i = 0; i < perSquad; i++, id++)
        {
            obs.Add(Obs(id, id, "A", "X"));
            res.AddRange(Res(id, id, +magnitude, -magnitude, 0, 0));
        }
        for (var i = 0; i < perSquad; i++, id++)
        {
            obs.Add(Obs(id, id, squadB, "X"));
            res.AddRange(Res(id, id, -magnitude, +magnitude, 0, 0));
        }
        return (obs, res);
    }

    private static PlayerImpactParameters Params(int minPlayer = 8, int minTeam = 6)
        => new() { MinPlayerMatches = minPlayer, MinTeamMatches = minTeam };

    private static string Source(params string[] parts)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Formax.slnx")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(new[] { dir! }.Concat(parts).ToArray()));
    }

    private static OutcomeExpectation Expectation(double lh = 1.5, double la = 1.1)
        => new(lh, la, 1.45, 1.15, 40, 40, 1.0, true, 1.5, 1.1, 1.1, 1.5, 10, 10, Day0, Day0);

    // ════════ 1. Kadro süreci API-Football'a 0 istek atar ════════════════════════

    [Fact]
    public void T01_KadroVeEtkiYolu_ApiFootballaCikmaz()
    {
        // Yükleyici ve ölçüm servisi sağlayıcı ALMAZ: derleme zamanında dış istek imkânsız.
        Assert.DoesNotContain(typeof(ISportsDataProvider),
            typeof(LineupHistoryLoader).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
        Assert.DoesNotContain(typeof(ISportsDataProvider),
            typeof(LineupImpactBacktestService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));

        // Kaynak dosyalarında api-football izi yok.
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "Lineups", "LineupHistoryLoader.cs"),
                     Source("Formax.Infrastructure", "Lineups", "LineupImpactBacktestService.cs"),
                     Source("Formax.Application", "Services", "Lineups", "PlayerImpactModel.cs"),
                     Source("Formax.Application", "Services", "Lineups", "LineupImpactCalculator.cs")
                 })
        {
            Assert.DoesNotContain("ApiFootball", file);
            Assert.DoesNotContain("ISportsDataProvider", file);
            Assert.DoesNotContain("HttpClient", file);
        }
    }

    // ════════ 2. Slotlar restart sonrası tekrar tüketilmez ══════════════════════

    [Fact]
    public void T02_Slotlar_RestartSonrasi_YenidenTuketilmez()
    {
        var kickoff = Day0;
        // Kalıcı defterdeki son GERÇEK kontrol T−30 slotuna düşüyorsa, süreç yeniden başlasa bile
        // T−30 ve öncesi harcanmıştır; yalnız bir sonraki slot açılır.
        var lastReal = kickoff.AddMinutes(-30);
        Assert.False(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-29), false, lastReal));
        Assert.False(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-25), false, lastReal));
        Assert.True(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-20), false, lastReal));

        // Kaçırılan slotlar BİRİKMEZ: T−60'tan T−10'a atlayan bir tur tek istek üretir.
        Assert.True(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-10), false, null));
        Assert.Equal(5, OfficialLineupSchedule.SlotOfCheck(kickoff, kickoff.AddMinutes(-10))); // T−10 slotu (0 tabanlı)
    }

    // ════════ 3 / 14. Aynı içerik / aynı kadro ikinci kez snapshot üretmez ══════

    [Fact]
    public void T03_T14_AyniKadro_AyniGirdiOzeti_YeniSnapshotYok()
    {
        var e = Expectation();
        var fp = "fingerprint-abc";
        var sig = "lineup-impact-1:Verified:High:False:0.012:-0.008";
        var first = MatchPredictionSnapshotService.InputHash("run-1", e, "Ev", "Dep", "Enabled", fp + "|" + sig);
        var second = MatchPredictionSnapshotService.InputHash("run-1", e, "Ev", "Dep", "Enabled", fp + "|" + sig);
        Assert.Equal(first, second);
    }

    // ════════ 13. Kadro geldikten sonra yeni snapshot üretilir ══════════════════

    [Fact]
    public void T13_KadroGelince_GirdiOzeti_Degisir()
    {
        var e = Expectation();
        var before = MatchPredictionSnapshotService.InputHash("run-1", e, "Ev", "Dep", "Enabled",
            "fp-no-lineup|lineup-impact-1:Missing:None:False:0:0");
        var after = MatchPredictionSnapshotService.InputHash("run-1", e, "Ev", "Dep", "Enabled",
            "fp-with-lineup|lineup-impact-1:Verified:High:True:0.021:-0.014");
        Assert.NotEqual(before, after);
    }

    // ════════ 4. Takım yönü ters çevrilmez ══════════════════════════════════════

    [Fact]
    public void T04_TakimYonu_TersCevrilmez()
    {
        // Ev 3 attı (beklenti 1), deplasman 0 attı (beklenti 1).
        var (home, away) = PlayerImpactModel.Residuals(7, Day0, 135, HomeTeam, AwayTeam, 3, 0, 1.0, 1.0);

        Assert.Equal(HomeTeam, home.TeamId);
        Assert.True(home.IsHome);
        Assert.Equal(AwayTeam, away.TeamId);
        Assert.False(away.IsHome);

        // Ev sahibi beklenenden ÇOK attı → hücum artığı pozitif; AZ yedi → savunma artığı negatif.
        Assert.True(home.AttackResidual > 0);
        Assert.True(home.DefenceResidual < 0);
        // Deplasmanın hücumu evin savunmasıdır; deplasmanın savunması evin hücumudur.
        Assert.Equal(home.DefenceResidual, away.AttackResidual, 12);
        Assert.Equal(home.AttackResidual, away.DefenceResidual, 12);
    }

    // ════════ 5. Aynı oyuncu iki takımda bulunamaz ══════════════════════════════

    [Fact]
    public void T05_AyniOyuncu_IkiTakimda_Reddedilir()
    {
        var home = Xi(HomeTeam, "A");
        // Aynı ADI deplasmanda da başlatalım ama ANAHTAR takımı taşıdığı için çakışma olmaz:
        // gerçek çakışma aynı anahtarın iki tarafta görünmesidir.
        var away = Xi(HomeTeam, "A"); // bilerek aynı takım anahtarıyla üretildi
        var clash = new MatchLineupObservation(1, Day0, 135, HomeTeam, AwayTeam, home, away, "Verified", "s", Day0);
        Assert.False(LineupVerificationRule.Check(clash).Accepted);

        // Aynı ADLI ama FARKLI takımın oyuncuları çakışma değildir (kimlik takımı taşır).
        var ok = new MatchLineupObservation(2, Day0, 135, HomeTeam, AwayTeam,
            Xi(HomeTeam, "A"), Xi(AwayTeam, "A"), "Verified", "s", Day0);
        Assert.True(LineupVerificationRule.Check(ok).Accepted);
        Assert.NotEqual(PlayerIdentity.Key(HomeTeam, "A1"), PlayerIdentity.Key(AwayTeam, "A1"));
    }

    // ════════ 6. Eksik ilk 11 modele girmez ═════════════════════════════════════

    [Fact]
    public void T06_EksikIlk11_ModeleGirmez()
    {
        var home = Xi(HomeTeam, "A").Take(10).ToList();
        var obs = new MatchLineupObservation(1, Day0, 135, HomeTeam, AwayTeam, home, Xi(AwayTeam, "X"), "Verified", "s", Day0);
        var verdict = LineupVerificationRule.Check(obs);
        Assert.False(verdict.Accepted);
        Assert.Equal(LineupSourceStatuses.Partial, verdict.SourceStatus);

        var adj = LineupImpactCalculator.Compute(obs, PlayerImpactModel.Empty(), HomeTeam, AwayTeam);
        Assert.False(adj.Applied);
        Assert.Equal(0, adj.HomeLineupDelta);
        Assert.Equal(0, adj.AwayLineupDelta);
    }

    // ════════ 7. Belirsiz eşleşme reddedilir ════════════════════════════════════

    [Fact]
    public void T07_BelirsizEslesme_Reddedilir_YanlisOyuncuylaBirlestirilmez()
    {
        // Çözülemeyen ad → boş anahtar → satır unresolved, etkiye GİRMEZ.
        Assert.Equal(string.Empty, PlayerIdentity.Key(HomeTeam, "."));
        Assert.Equal(string.Empty, PlayerIdentity.Key(HomeTeam, null));
        var unresolved = new LineupPlayerObservation(PlayerIdentity.Key(HomeTeam, "."), ".", "M", 8, true);
        Assert.False(unresolved.Resolved);

        // Mevki bilinmeyen satır da etkiye girmez (mevki UYDURULMAZ).
        var noPos = new LineupPlayerObservation(PlayerIdentity.Key(HomeTeam, "Ali Veli"), "Ali Veli", "???", 8, true);
        Assert.False(noPos.Resolved);

        // Türkçe diakritik tuzağı: "İlkay" ve "ilkay" AYNI oyuncudur, "Ilkay Gündoğan" farklı takımda değil.
        Assert.Equal(PlayerIdentity.Key(HomeTeam, "İlkay Gündoğan"), PlayerIdentity.Key(HomeTeam, "ilkay gundogan"));
    }

    // ════════ 8. Küçük örneklem shrink edilir ═══════════════════════════════════

    [Fact]
    public void T08_KucukOrneklem_Shrink_AltindaTamSifir()
    {
        // n = 2 (kapının altında) → etki TAM 0.
        var small = Split(perSquad: 2);
        var mSmall = PlayerImpactModel.BuildAsOf(small.Obs, small.Res, Day0.AddYears(1), Params());
        Assert.False(mSmall.Impact(PlayerIdentity.Key(HomeTeam, "A9")).Sufficient);
        Assert.Equal(0, mSmall.Impact(PlayerIdentity.Key(HomeTeam, "A9")).AttackImpact);

        // n = 10 ve n = 40 — AYNI ham sapma, daha büyük örneklem daha az daraltılır.
        var mid = Split(perSquad: 10);
        var big = Split(perSquad: 40);
        var iMid = PlayerImpactModel.BuildAsOf(mid.Obs, mid.Res, Day0.AddYears(2), Params()).Impact(PlayerIdentity.Key(HomeTeam, "A9"));
        var iBig = PlayerImpactModel.BuildAsOf(big.Obs, big.Res, Day0.AddYears(2), Params()).Impact(PlayerIdentity.Key(HomeTeam, "A9"));
        Assert.True(iMid.Sufficient && iBig.Sufficient);
        Assert.True(iMid.Shrinkage < iBig.Shrinkage, $"daraltma büyük örneklemde gevşemeli: {iMid.Shrinkage} < {iBig.Shrinkage}");
    }

    // ════════ 9. Oyuncu etkisi takım tavanını aşmaz ═════════════════════════════

    [Fact]
    public void T09_TakimTavani_Asilmaz()
    {
        // Aşırı büyüklükte sapma (log ölçeğinde ±3) — tavan olmasa λ katlanırdı.
        var d = Split(perSquad: 30, magnitude: 3.0);
        var model = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddYears(3), Params());
        var p = model.Parameters;

        var adj = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), model, HomeTeam, AwayTeam);
        Assert.True(Math.Abs(adj.HomeLineupDelta) <= p.MaxLambdaLogDelta + 1e-9,
            $"ev deltası tavanı aştı: {adj.HomeLineupDelta}");
        Assert.True(Math.Abs(adj.AwayLineupDelta) <= p.MaxLambdaLogDelta + 1e-9,
            $"deplasman deltası tavanı aştı: {adj.AwayLineupDelta}");
        Assert.True(Math.Abs(adj.HomeAttackDelta) <= p.MaxSideLogDelta + 1e-9);
        Assert.Contains(LineupReasonCodes.ImpactCapped, adj.AdjustmentReasonCodes);

        // λ en fazla ~%10,5 oynar: temel model DEĞİŞMEZ, üstüne sınırlı delta biner.
        var e = Expectation();
        var adjusted = LineupImpactCalculator.Apply(e, adj);
        Assert.InRange(adjusted.LambdaHome / e.LambdaHome, Math.Exp(-p.MaxLambdaLogDelta), Math.Exp(p.MaxLambdaLogDelta));
    }

    // ════════ 10. Yerine giren oyuncunun kalitesi deltayı değiştirir ════════════

    [Fact]
    public void T10_YedekKalitesi_DeltayiDegistirir()
    {
        // İki senaryo: AYNI A kadrosu, farklı kalitede B kadrosu (yedek havuzu).
        var weakBench = Split(perSquad: 30, magnitude: 1.0, squadB: "B");
        var modelWeak = PlayerImpactModel.BuildAsOf(weakBench.Obs, weakBench.Res, Day0.AddYears(3), Params());
        var deltaWeak = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), modelWeak, HomeTeam, AwayTeam).HomeLineupDelta;

        // Yedek havuzu KALDIRILIRSA (yalnız A oynamış) net etki başka çıkar.
        var onlyA = (Obs: new List<MatchLineupObservation>(), Res: new List<TeamMatchResidual>());
        for (var i = 1; i <= 30; i++)
        {
            onlyA.Obs.Add(Obs(i, i, "A", "X"));
            onlyA.Res.AddRange(Res(i, i, i % 2 == 0 ? +1.0 : -1.0, 0, 0, 0));
        }
        var modelNoBench = PlayerImpactModel.BuildAsOf(onlyA.Obs, onlyA.Res, Day0.AddYears(3), Params());
        var deltaNoBench = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), modelNoBench, HomeTeam, AwayTeam).HomeLineupDelta;

        Assert.NotEqual(deltaWeak, deltaNoBench);

        // Havuzda yalnız oyuncunun KENDİSİ varsa (takımın o mevkideki tek ölçülebilir oyuncusu)
        // yedek kalitesi ÜRETİLMEZ: 0 döner, oyuncu kendisiyle kıyaslanmaz.
        var soloKeeper = modelNoBench.Replacement(HomeTeam, "G", PlayerIdentity.Key(HomeTeam, "A1"));
        Assert.Equal(0, soloKeeper.PoolSize);
        Assert.Equal(0, soloKeeper.Attack);
        Assert.Equal(0, soloKeeper.Defence);

        // Havuzda başkaları varsa kalite gerçekten ölçülür ve deltaya girer.
        var forwardPool = modelNoBench.Replacement(HomeTeam, "F", PlayerIdentity.Key(HomeTeam, "A9"));
        Assert.Equal(2, forwardPool.PoolSize);
    }

    // ════════ 11. Kaleci etkisi hücum metriği gibi hesaplanmaz ══════════════════

    [Fact]
    public void T11_KaleciEtkisi_HucumMetrigiyleHesaplanmaz()
    {
        var d = Split(perSquad: 30, magnitude: 1.5);
        var model = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddYears(3), Params());

        var keeper = model.Impact(PlayerIdentity.Key(HomeTeam, "A1"));   // G
        var defender = model.Impact(PlayerIdentity.Key(HomeTeam, "A2")); // D
        var forward = model.Impact(PlayerIdentity.Key(HomeTeam, "A9"));  // F

        Assert.Equal("G", keeper.Position);
        Assert.Equal(0, keeper.AttackImpact);            // kaleciye hücum KANALI kapalı
        Assert.NotEqual(0, keeper.DefenceImpact);
        Assert.Equal(0, defender.AttackImpact);          // savunmacı gol/asistle ölçülmez
        Assert.NotEqual(0, defender.DefenceImpact);
        Assert.Equal(0, forward.DefenceImpact);          // forvet savunma kanalından okunmaz
        Assert.NotEqual(0, forward.AttackImpact);
    }

    // ════════ 12. Kadro yokken temel olasılıklar birebir korunur ════════════════

    [Fact]
    public void T12_KadroYokken_TemelOlasiliklar_BirebirKorunur()
    {
        var e = Expectation();
        var none = LineupImpactCalculator.Compute(null, PlayerImpactModel.Empty(), HomeTeam, AwayTeam);
        Assert.False(none.Applied);
        Assert.Equal(LineupSourceStatuses.Missing, none.LineupSourceStatus);
        Assert.Equal(LineupConfidenceLevels.None, none.LineupConfidence);
        Assert.Contains(LineupReasonCodes.LineupMissing, none.AdjustmentReasonCodes);

        // Beklenti aynen döner; λ'lar bit bit aynıdır.
        var after = LineupImpactCalculator.Apply(e, none);
        Assert.Equal(e.LambdaHome, after.LambdaHome);
        Assert.Equal(e.LambdaAway, after.LambdaAway);

        var p = new OutcomeModelParameters();
        var basePr = OutcomePredictor.Predict(e, 135, p);
        var dto = OutcomeLineupDto.Build(none, basePr.Calibrated, null, false);
        Assert.Equal(dto.BaseHomeProbability, dto.LineupAdjustedHomeProbability);
        Assert.Equal(dto.BaseDrawProbability, dto.LineupAdjustedDrawProbability);
        Assert.Equal(dto.BaseAwayProbability, dto.LineupAdjustedAwayProbability);
    }

    // ════════ 15. Olasılıkların toplamı 1'dir ═══════════════════════════════════

    [Fact]
    public void T15_UcOlasilikToplami_TamBir()
    {
        var d = Split(perSquad: 30, magnitude: 2.0);
        var model = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddYears(3), Params());
        var adj = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), model, HomeTeam, AwayTeam);

        var p = new OutcomeModelParameters();
        foreach (var (lh, la) in new[] { (1.5, 1.1), (0.4, 2.9), (3.2, 0.2) })
        {
            var e = Expectation(lh, la);
            var basePr = OutcomePredictor.Predict(e, 135, p);
            var adjPr = OutcomePredictor.Predict(LineupImpactCalculator.Apply(e, adj), 135, p);
            var dto = OutcomeLineupDto.Build(adj, basePr.Calibrated, adjPr.Calibrated, true);
            Assert.True(dto.SumsToOne(), $"toplam 1 değil: {dto.BaseHomeProbability + dto.BaseDrawProbability + dto.BaseAwayProbability}");
        }
    }

    // ════════ 16. Gelecek veri sızıntısı yoktur ═════════════════════════════════

    [Fact]
    public void T16_GelecekVeriSizintisi_Yok()
    {
        var d = Split(perSquad: 30);
        var lastKickoff = d.Obs.Max(o => o.KickoffUtc);

        // Kesim = son maçın başlama anı → O MAÇ modele GİRMEZ (kesin eşitsizlik).
        var withLast = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, lastKickoff.AddSeconds(1), Params());
        var withoutLast = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, lastKickoff, Params());
        Assert.Equal(d.Obs.Count, withLast.ObservedMatches);
        Assert.Equal(d.Obs.Count - 1, withoutLast.ObservedMatches);

        // Kesim geçmişte ise sonraki bütün maçlar görünmez.
        var early = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddDays(5), Params());
        Assert.Equal(4, early.ObservedMatches);
        Assert.Equal(0, early.SufficientPlayers); // 4 maçla kimse kapıyı geçemez
    }

    // ════════ 17. Discover ve Detail aynı SnapshotId'yi gösterir ════════════════

    [Fact]
    public void T17_DiscoverVeDetail_AyniSnapshotId_VeAyniKadroDurumu()
    {
        var e = Expectation();
        var p = new OutcomeModelParameters();
        var pr = OutcomePredictor.Predict(e, 135, p);
        var dto = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep");
        dto.SnapshotId = "snp-test-0001";
        dto.PredictionEligibility = PredictionEligibilities.Enabled;
        dto.Lineup = OutcomeLineupDto.Build(
            LineupAdjustment.None(LineupSourceStatuses.Missing, new[] { LineupReasonCodes.LineupMissing }),
            pr.Calibrated, null, false);

        // İki yüzey AYNI nesneden türer; kullanıcı görünümü SnapshotId'yi korur.
        var discover = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        var detail = OutcomeSnapshotBuilder.ForUser(Clone(dto));
        Assert.Equal(dto.SnapshotId, discover.SnapshotId);
        Assert.Equal(discover.SnapshotId, detail.SnapshotId);
        Assert.Equal(discover.Lineup?.LineupSourceStatus, detail.Lineup?.LineupSourceStatus);

        // Uygun olmayan maçta da SnapshotId ve kadro durumu taşınır, yüzde TAŞINMAZ.
        var blocked = Clone(dto);
        blocked.PredictionEligibility = PredictionEligibilities.Limited;
        var blockedView = OutcomeSnapshotBuilder.ForUser(blocked);
        Assert.Equal(dto.SnapshotId, blockedView.SnapshotId);
        Assert.NotNull(blockedView.Lineup);
        Assert.Contains("PROBABILITIES_WITHHELD", blockedView.Lineup!.AdjustmentReasonCodes);
        Assert.Equal(0, blockedView.Lineup.BaseHomeProbability);
    }

    private static OutcomeSnapshotDto Clone(OutcomeSnapshotDto s)
        => System.Text.Json.JsonSerializer.Deserialize<OutcomeSnapshotDto>(
               System.Text.Json.JsonSerializer.Serialize(s))!;

    // ════════ 18 / 19 / 20. Kart semantiği kadro katmanıyla bozulmaz ════════════

    [Fact]
    public void T18_T19_T20_KartSemantigi_KadroKatmaniyla_Bozulmaz()
    {
        var d = Split(perSquad: 30, magnitude: 2.0);
        var model = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddYears(3), Params());
        var adj = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), model, HomeTeam, AwayTeam);
        var p = new OutcomeModelParameters();

        foreach (var (lh, la) in new[] { (1.9, 0.9), (0.8, 1.9), (1.3, 1.3) })
        {
            var e = LineupImpactCalculator.Apply(Expectation(lh, la), adj);
            var pr = OutcomePredictor.Predict(e, 135, p);

            // 18. yalnız yayımlanabilir aileden kart çıkar: 1X2 kapalıysa maç sonucu kartı YOK.
            var onlyGoals = new OutcomeSnapshotBuilder.MarketPublication(new[]
            {
                Metric(MarketFamilies.MatchResult, MarketEligibilityStatuses.WorseThanBaseline),
                Metric(MarketFamilies.DoubleChance, MarketEligibilityStatuses.WorseThanBaseline),
                Metric(MarketFamilies.TotalGoals15, MarketEligibilityStatuses.Eligible),
                Metric(MarketFamilies.TotalGoals25, MarketEligibilityStatuses.Eligible),
                Metric(MarketFamilies.TotalGoals35, MarketEligibilityStatuses.Eligible),
                Metric(MarketFamilies.BothTeamsToScore, MarketEligibilityStatuses.Eligible)
            });
            var gated = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep", onlyGoals);
            Assert.DoesNotContain(gated.MainCards, c => c.MeasuredFamily == MarketFamilies.MatchResult);

            // 19. 1X2 uygunken İLK kart Home/Draw/Away argmax'ıdır.
            var all = OutcomeSnapshotBuilder.Build(1, pr, "Ev", "Dep",
                new OutcomeSnapshotBuilder.MarketPublication(MarketFamilies.All
                    .Select(f => Metric(f, MarketEligibilityStatuses.Eligible))));
            var first = all.MainCards.First();
            Assert.Equal(MarketFamilies.MatchResult, first.MeasuredFamily);
            var cal = pr.Calibrated;
            var expected = cal.HomeWin >= cal.Draw && cal.HomeWin >= cal.AwayWin ? OddsMarketKeys.Ms1
                         : cal.AwayWin >= cal.Draw ? OddsMarketKeys.Ms2 : OddsMarketKeys.MsX;
            Assert.Equal(expected, first.MarketKey);

            // 20. çifte şans ANA KARTIN YERİNE GEÇEMEZ ve TEK BAŞINA yayımlanamaz:
            // hiçbir zaman ilk kart değildir ve tek kart olduğu durum yoktur.
            Assert.False(OutcomeFamilies.IsCompound(all.MainCards[0].MarketKey ?? string.Empty));
            var dc = all.MainCards.FirstOrDefault(c => OutcomeFamilies.IsCompound(c.MarketKey ?? string.Empty));
            if (dc != null) Assert.True(all.MainCards.IndexOf(dc) > 0 && all.MainCards.Count > 1);
            // 1X2 kapalıyken çifte şans hiç çıkamaz (uygunluğu 1X2'den miras alır).
            Assert.DoesNotContain(gated.MainCards, c => OutcomeFamilies.IsCompound(c.MarketKey ?? string.Empty));
        }
    }

    private static MarketFamilyMetrics Metric(string family, string status) => new()
    {
        LeagueId = 135, Family = family, Status = status, Matches = 500,
        ReasonCodes = status == MarketEligibilityStatuses.Eligible ? new List<string>() : new List<string> { "TEST" }
    };

    // ════════ 21. Kadro bloğu 0 / eksik / tam durumlarda tutarlı ════════════════

    [Fact]
    public void T21_SifirEksikTamKadro_BlokTutarli()
    {
        var p = new OutcomeModelParameters();
        var basePr = OutcomePredictor.Predict(Expectation(), 135, p);

        // 0 kadro
        var zero = OutcomeLineupDto.Build(
            LineupAdjustment.None(LineupSourceStatuses.Missing, new[] { LineupReasonCodes.LineupMissing }),
            basePr.Calibrated, null, false);
        Assert.Equal(LineupSourceStatuses.Missing, zero.LineupSourceStatus);
        Assert.Equal(LineupConfidenceLevels.None, zero.LineupConfidence);
        Assert.Empty(zero.ImpactfulStarters);
        Assert.True(zero.SumsToOne());

        // eksik kadro (tek taraf)
        var partialObs = new MatchLineupObservation(1, Day0, 135, HomeTeam, AwayTeam,
            Xi(HomeTeam, "A"), new List<LineupPlayerObservation>(), "PartiallyVerified", "s", Day0);
        var partial = OutcomeLineupDto.Build(
            LineupImpactCalculator.Compute(partialObs, PlayerImpactModel.Empty(), HomeTeam, AwayTeam),
            basePr.Calibrated, null, false);
        Assert.Equal(LineupSourceStatuses.Partial, partial.LineupSourceStatus);
        Assert.Equal(LineupConfidenceLevels.Insufficient, partial.LineupConfidence);
        Assert.True(partial.SumsToOne());

        // tam kadro
        var d = Split(perSquad: 30, magnitude: 2.0);
        var model = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddYears(3), Params());
        var adj = LineupImpactCalculator.Compute(Obs(999, 400, "A", "X"), model, HomeTeam, AwayTeam);
        var adjPr = OutcomePredictor.Predict(LineupImpactCalculator.Apply(Expectation(), adj), 135, p);
        var full = OutcomeLineupDto.Build(adj, basePr.Calibrated, adjPr.Calibrated, true);
        Assert.Equal(LineupSourceStatuses.Verified, full.LineupSourceStatus);
        Assert.Equal(22, full.TotalStarters);
        Assert.True(full.SumsToOne());
    }

    // ════════ 22. Sonuç ve fikstür botları kadro katmanına BAĞLI DEĞİL ══════════

    [Fact]
    public void T22_SonucVeFiksturBotlari_KadroKatmanindanBagimsiz()
    {
        foreach (var file in new[]
                 {
                     Source("Formax.Infrastructure", "BackgroundJobs", "FixtureSyncJob.cs"),
                     Source("Formax.Infrastructure", "BackgroundJobs", "OfficialResultBotJobs.cs"),
                     Source("Formax.Infrastructure", "OfficialSources", "OfficialResultWriter.cs")
                 })
        {
            Assert.DoesNotContain("PlayerImpactModel", file);
            Assert.DoesNotContain("LineupImpactCalculator", file);
            Assert.DoesNotContain("LineupHistoryLoader", file);
        }
    }

    // ════════ 23. Uzun takılma yok — etki hesabı saf ve sınırlı ═════════════════

    [Fact]
    public void T23_EtkiHesabi_Saf_VeSinirliSurede()
    {
        // 300 maçlık gözlem kümesi (üretimdeki kapsamın çok üstünde) — model kurulumu ve
        // 300 ayrı kesimde yeniden kurulum saniyeler içinde bitmeli, DB'ye HİÇ gitmemeli.
        var d = Split(perSquad: 150);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 300; i += 10)
        {
            var m = PlayerImpactModel.BuildAsOf(d.Obs, d.Res, Day0.AddDays(i), Params());
            LineupImpactCalculator.Compute(Obs(9999, i, "A", "X"), m, HomeTeam, AwayTeam);
        }
        sw.Stop();
        Assert.True(sw.Elapsed.TotalSeconds < 15, $"etki hesabı çok yavaş: {sw.Elapsed.TotalSeconds:0.0} sn");

        // Yükleyici izlemesiz okur (change tracker şişmesi ve kilit yok).
        Assert.Contains("AsNoTracking", Source("Formax.Infrastructure", "Lineups", "LineupHistoryLoader.cs"));
    }

    // ════════ 24. Kaynak engeli ile "kadro yayımlanmadı" ayrışır ════════════════

    [Fact]
    public void T24_KaynakEngeli_KadroYayimlanmadi_AyriSiniflar()
    {
        // Sözlük ayrı: FetchFailed (kontrol SAYILMAZ) ≠ NotPublished (geçerli boş cevap).
        Assert.NotEqual(Formax.Infrastructure.OfficialSources.LineupOutcomes.FetchFailed,
                        Formax.Infrastructure.OfficialSources.LineupOutcomes.NotPublished);
        Assert.NotEqual(Formax.Infrastructure.OfficialSources.LineupOutcomes.Rejected,
                        Formax.Infrastructure.OfficialSources.LineupOutcomes.NotPublished);
        Assert.NotEqual(Formax.Infrastructure.OfficialSources.LineupOutcomes.NoOfficialSource,
                        Formax.Infrastructure.OfficialSources.LineupOutcomes.FetchFailed);

        // Başarısız okuma son kontrol damgası YAZMAZ → slot açık kalır (takvim kuralı).
        var kickoff = Day0;
        Assert.True(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-45), false, lastRealCheckUtc: null));
        // Geçerli boş cevap damga YAZAR → aynı slot ikinci istek üretmez.
        Assert.False(OfficialLineupSchedule.ShouldCheck(kickoff, kickoff.AddMinutes(-44), false,
            lastRealCheckUtc: kickoff.AddMinutes(-45)));
    }

    // ════════ EK: kabul kapısı iyileşme yoksa üretime AÇMAZ ═════════════════════

    [Fact]
    public void KabulKapisi_IyilesmeYoksa_UretimeAcmaz()
    {
        var baseOverall = MarketFamilies.Measured
            .Select(f => new MarketFamilyMetrics { Family = f, Matches = 500, LogLoss = 1.0, CalibrationError = 0.02 })
            .ToList();

        // Hiç düzeltilmiş maç yok → ölçülemez.
        var empty = new LineupAblationResult { Name = "A6_Candidate", AdjustedMatches = 0 };
        Assert.Equal(LineupImpactDecisions.NotMeasurable, LineupImpactPolicy.Decide(empty, baseOverall).Decision);

        // Düzeltme var ama tabandan kötü → gölgede kalır.
        var worse = new LineupAblationResult
        {
            Name = "A6_Candidate", AdjustedMatches = 300,
            Overall = MarketFamilies.Measured.Select(f => new MarketFamilyMetrics { Family = f, LogLoss = 1.05, CalibrationError = 0.02 }).ToList()
        };
        foreach (var f in MarketFamilies.Measured)
        {
            worse.AdjustedOnlyLogLossDiff[f] = 0.05;
            worse.AdjustedOnlyCiHigh[f] = 0.09;
        }
        var w = LineupImpactPolicy.Decide(worse, baseOverall);
        Assert.Equal(LineupImpactDecisions.Shadow, w.Decision);
        Assert.Contains(w.Reasons, r => r.StartsWith("WORSE_THAN_BASE"));

        // Anlamlı iyileşme + kalibrasyon korunuyor + yeterli örneklem → üretim adayı.
        var good = new LineupAblationResult
        {
            Name = "A6_Candidate", AdjustedMatches = 300,
            Overall = MarketFamilies.Measured.Select(f => new MarketFamilyMetrics { Family = f, LogLoss = 0.98, CalibrationError = 0.02 }).ToList()
        };
        foreach (var f in MarketFamilies.Measured)
        {
            good.AdjustedOnlyLogLossDiff[f] = -0.02;
            good.AdjustedOnlyCiHigh[f] = -0.005;
        }
        Assert.Equal(LineupImpactDecisions.Production, LineupImpactPolicy.Decide(good, baseOverall).Decision);
    }
}
