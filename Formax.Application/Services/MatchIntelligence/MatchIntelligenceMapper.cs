using Formax.Application.DTOs.Matches;
using Formax.Application.DTOs.Live;
using Formax.Application.DTOs.Lineup;
using Formax.Application.DTOs.Nabiz;
using Formax.Application.DTOs.MatchIntelligence;
// Ad çakışması: MarketIntelligenceDto hem DTOs.Matches (legacy) hem DTOs.MatchIntelligence'ta var.
using MarketIntelDto = Formax.Application.DTOs.MatchIntelligence.MarketIntelligenceDto;

namespace Formax.Application.Services.MatchIntelligence;

/// <summary>
/// Backend MatchDetailDto → Match Intelligence ViewModel (TEK YÖNLÜ) — saf mapping, I/O yok.
///
/// Backend tek doğruluk kaynağıdır: burada yalnız mevcut domain verisinden TÜRETİLEBİLEN alanlar
/// üretilir (backend iş kuralı meşrudur). Domain'de KARŞILIĞI OLMAYAN alanlar uydurulmaz; null/boş
/// bırakılır ve `// MISSING (domain)` ile işaretlenir (Görev #011 gap raporu).
///
/// Experience sunum iskeleti (label/question/hero/ask/next) docs 05/07–14 ürün sabitleridir.
/// </summary>
public static class MatchIntelligenceMapper
{
    public static MatchIntelligenceDto Map(
        MatchDetailDto d,
        ExpectedLineupDto homeExpected, ExpectedLineupDto awayExpected,
        LivingLineupDto homeLiving, LivingLineupDto awayLiving,
        NewsIntelligenceDto newsIntel,
        MatchOutlookDto outlook,
        MarketIntelDto market,
        WatchLiveDto watchLive,
        LiveIntelligenceDto liveIntel)
    {
        var status = MapStatus(d.Status);
        var homeName = d.HomeTeam?.Name ?? "Ev sahibi";
        var awayName = d.AwayTeam?.Name ?? "Deplasman";
        var nar = d.AiNarrative;
        var shared = FirstNonEmpty(nar?.MatchReport, d.Insight?.Summary, d.Ai?.Summary);

        var experiences = new List<MiExperienceDto>
        {
            BuildExperience("expected-lineup", "Beklenen Kadro",
                "Takımların maça nasıl başlaması bekleniyor?",
                new MiHeroDto { Title = "Beklenen İlk 11", Caption = "Rol bazlı yerleşim", Motif = "expected-lineup" },
                "Canlı kadroyu görmek ister misin?", "living-lineup",
                FirstNonEmpty(nar?.StatisticalSummary, shared),
                ExpectedLineupEvidence(homeExpected, awayExpected),
                lineup: BuildLineups(homeExpected, awayExpected)),

            BuildExperience("living-lineup", "Canlı Kadro",
                "Beklenen kadro ile açıklanan kadro arasında ne değişti?",
                new MiHeroDto { Title = "Açıklanan İlk 11", Caption = "Resmi kadro", Motif = "living-lineup" },
                "H2H karşılaştırmasını görmek ister misin?", "h2h",
                LivingInsight(homeLiving, awayLiving, shared),
                LivingEvidence(homeLiving, awayLiving),
                // Görev #014: beklenen↔resmi karşılaştırma backend'de (LivingLineupEngine).
                lineupChange: BuildLineupChange(homeLiving, awayLiving),
                living: new MiMatchLivingDto { Home = homeLiving, Away = awayLiving }),

            BuildExperience("h2h", "H2H",
                "Bu eşleşmenin geçmiş hikayesi nedir?",
                new MiHeroDto { Title = "Geçmiş Karşılaşmalar", Caption = "Kronolojik", Motif = "h2h" },
                "Son gelişmeleri görmek ister misin?", "news",
                FirstNonEmpty(nar?.StatisticalSummary, shared),
                H2HEvidence(d),
                h2h: BuildH2H(d, homeName, awayName)),

            BuildExperience("news", "Haberler",
                "Bu maç öncesinde bilinmesi gereken güncel gelişmeler neler?",
                new MiHeroDto { Title = "Öne Çıkan Haber", Caption = "Kulüp & basın", Motif = "news" },
                "Maç görünümünü görmek ister misin?", "match-outlook",
                newsIntel.Articles.Count > 0
                    ? newsIntel.AiSummary
                    : FirstNonEmpty(nar?.NewsSummary, nar?.SocialSummary, shared),
                NewsEvidence(newsIntel),
                news: BuildNews(newsIntel)),

            BuildExperience("match-outlook", "Maç Görünümü",
                "Bu maçın genel görünümü nasıl?",
                new MiHeroDto { Title = "Genel Görünüm", Caption = "Form · gol eğilimi", Motif = "match-outlook" },
                "Market Intelligence bölümüne geçmek ister misin?", "market-intelligence",
                outlook.AiSummary,
                OutlookEvidence(outlook),
                matchOutlook: BuildOutlook(outlook)),

            BuildExperience("market-intelligence", "Market",
                "Market bu maç hakkında ne söylüyor?",
                new MiHeroDto { Title = "Market Görünümü", Caption = "Yön · eğilim", Motif = "market-intelligence" },
                "Canlı yayın seçeneklerini görmek ister misin?", "watch-live",
                market.AiSummary,
                MarketEvidence(market),
                market: BuildMarket(market)),

            BuildExperience("watch-live", "Nereden İzlerim",
                "Bu maçı nereden izleyebilirim?",
                new MiHeroDto { Title = "Yayın", Caption = "Resmi yayıncı", Motif = "watch-live" },
                "Canlı maç zekasını görmek ister misin?", "live-intelligence",
                watchLive.AiSummary,
                WatchLiveEvidence(watchLive),
                watchLive: BuildWatchLive(watchLive)),

            BuildExperience("live-intelligence", "Canlı Zeka",
                "Şu anda maçta bilinmesi gereken en önemli gelişme nedir?",
                new MiHeroDto { Title = "Canlı Maç", Caption = "Gerçek zamanlı", Motif = "live-intelligence" },
                "Bu maç hakkında başka bir Experience'e dönmek ister misin?", "expected-lineup",
                liveIntel.AiSummary,
                LiveEvidence(liveIntel),
                live: BuildLive(liveIntel)),
        };

        return new MatchIntelligenceDto
        {
            MatchId = d.MatchId,
            Header = new MiHeaderDto
            {
                Competition = string.Join(" · ", new[] { d.League, d.Round }.Where(s => !string.IsNullOrWhiteSpace(s))),
                HomeTeam = homeName,
                AwayTeam = awayName,
                KickoffLabel = d.MatchDate.ToString("dd MMM HH:mm"),
                Status = status,
                MinuteLabel = status == "LIVE" && d.Live?.Stats?.Minute != null ? $"{d.Live.Stats.Minute}'" : null,
            },
            Session = new MiSessionDto
            {
                Greeting = "Bu maça birlikte bakalım.",
                Message = FirstNonEmpty(nar?.RadarSummary, d.Ai?.Summary, d.Insight?.Summary,
                    "Bu maçı adım adım birlikte inceleyelim."),
            },
            InitialExperienceId = "expected-lineup",
            Experiences = experiences,
        };
    }

    // ── Experience factory ───────────────────────────────────────────────────
    private static MiExperienceDto BuildExperience(
        string id, string label, string question, MiHeroDto hero, string ask, string? next,
        string insight, List<MiEvidenceDto> evidence,
        MiLineupsDto? lineup = null, MiLineupChangeDto? lineupChange = null, MiH2HDto? h2h = null,
        MiNewsDto? news = null, MiMatchOutlookDto? matchOutlook = null, MiMarketDto? market = null,
        MiWatchLiveDto? watchLive = null, MiLiveDto? live = null, MiMatchLivingDto? living = null)
        => new()
        {
            Id = id, Label = label, Question = question, Hero = hero,
            StageSummary = insight, Insight = insight, Evidence = evidence, Ask = ask, Next = next,
            Lineup = lineup, LineupChange = lineupChange, H2H = h2h, News = news,
            MatchOutlook = matchOutlook, Market = market, WatchLive = watchLive, Live = live, Living = living,
        };

    // ── 07 Expected Lineup ─────────────────────────────────────────────────────
    // Görev #013: ExpectedLineupEngine çıktısından beslenir (diziliş + oyuncular + güven + kadro dışı).
    private static MiLineupsDto? BuildLineups(ExpectedLineupDto home, ExpectedLineupDto away)
    {
        // İkisi de boşsa (yetersiz geçmiş veri) → null → frontend generic Hero fallback.
        if (home.Players.Count == 0 && away.Players.Count == 0) return null;

        static MiTeamLineupDto Team(ExpectedLineupDto e) => new()
        {
            TeamName = e.TeamName,
            Formation = e.Formation,
            Players = e.Players.Select(p => new MiLineupPlayerDto
            {
                Number = p.Number, Name = p.Name, Position = p.Position, Role = p.Role, Confidence = p.Confidence
            }).ToList(),
            Confidence = e.Confidence,
            Source = e.Source,
            UnavailablePlayers = e.UnavailablePlayers
                .Select(u => new MiUnavailablePlayerDto { Name = u.Name, Status = u.Status, Reason = u.Reason })
                .ToList(),
            Reasons = e.Reasons,
        };

        return new MiLineupsDto { Home = Team(home), Away = Team(away) };
    }

    private static List<MiEvidenceDto> ExpectedLineupEvidence(ExpectedLineupDto home, ExpectedLineupDto away) => new()
    {
        new() { Label = "Diziliş", Detail = $"{Dash(home.Formation)} · {Dash(away.Formation)}" },
        new() { Label = "Güven", Detail = $"Ev %{home.Confidence} · Dep %{away.Confidence}" },
        new() { Label = "Kadro dışı", Detail = (home.UnavailablePlayers.Count + away.UnavailablePlayers.Count).ToString() },
    };

    private static string Dash(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

    // ── 08 Living Lineup (Görev #014) — backend karşılaştırmasından ────────────
    private static string LivingInsight(LivingLineupDto home, LivingLineupDto away, string shared)
    {
        var parts = new List<string>();
        if (home.Announced) parts.Add(home.AiSummary);
        if (away.Announced) parts.Add(away.AiSummary);
        return parts.Count > 0
            ? string.Join(" ", parts)
            : "Resmi kadrolar henüz açıklanmadı; açıklandığında beklenenle karşılaştırılacak.";
    }

    private static List<MiEvidenceDto> LivingEvidence(LivingLineupDto home, LivingLineupDto away)
    {
        var added = home.AddedPlayers.Concat(away.AddedPlayers).Select(p => p.Name).ToList();
        var removed = home.RemovedPlayers.Concat(away.RemovedPlayers).Select(p => p.Name).ToList();
        var formationChanged = home.FormationChanged || away.FormationChanged;
        return new()
        {
            new() { Label = "Değişiklik", Detail = (home.ChangeCount + away.ChangeCount).ToString() },
            new() { Label = "Giren", Detail = added.Count == 0 ? "yok" : string.Join(", ", added) },
            new() { Label = "Çıkan", Detail = removed.Count == 0 ? "yok" : string.Join(", ", removed) },
            new() { Label = "Formasyon değişimi", Detail = formationChanged ? "var" : "yok" },
        };
    }

    private static MiLineupChangeDto? BuildLineupChange(LivingLineupDto home, LivingLineupDto away)
    {
        // Frontend #003, lineupChange.{expected, official}'i client-side kıyaslar. Yalnız iki takım da
        // açıklanmış + beklenen baz çizgisi varsa doldurulur; yoksa null → generic fallback.
        if (!home.Announced || !away.Announced) return null;
        if (home.ExpectedPlayers.Count == 0 || away.ExpectedPlayers.Count == 0) return null;

        return new MiLineupChangeDto
        {
            Expected = new MiLineupsDto
            {
                Home = ToMiTeam(home.TeamName, home.ExpectedFormation, home.ExpectedPlayers),
                Away = ToMiTeam(away.TeamName, away.ExpectedFormation, away.ExpectedPlayers),
            },
            Official = new MiLineupsDto
            {
                Home = ToMiTeam(home.TeamName, home.OfficialFormation, home.OfficialPlayers),
                Away = ToMiTeam(away.TeamName, away.OfficialFormation, away.OfficialPlayers),
            },
        };
    }

    private static MiTeamLineupDto ToMiTeam(string teamName, string formation, List<ExpectedPlayerDto> players) => new()
    {
        TeamName = teamName,
        Formation = formation,
        Players = players.Select(p => new MiLineupPlayerDto
        {
            Number = p.Number, Name = p.Name, Position = p.Position, Role = p.Role, Confidence = p.Confidence
        }).ToList(),
    };

    // ── 09 H2H (backend TAM türetebilir) ───────────────────────────────────────
    private static MiH2HDto BuildH2H(MatchDetailDto d, string homeName, string awayName)
    {
        var h = d.H2H;
        var matches = h?.Matches ?? new List<H2HMatchDto>();

        int homeGoals = 0, awayGoals = 0;
        var hv = new int[3]; // W,D,L for fixture-home AT home
        var av = new int[3]; // W,D,L for fixture-away AT away
        var recent = new List<MiH2HMeetingDto>();
        H2HMatchDto? biggest = null; int biggestMargin = -1;

        foreach (var m in matches)
        {
            var homeIsFixtureHome = string.Equals(m.HomeTeamName, homeName, StringComparison.OrdinalIgnoreCase);
            var gfHome = homeIsFixtureHome ? m.HomeScore : m.AwayScore; // fixture-home team's goals
            var gfAway = homeIsFixtureHome ? m.AwayScore : m.HomeScore; // fixture-away team's goals
            homeGoals += gfHome; awayGoals += gfAway;

            var outcome = gfHome > gfAway ? "home" : gfHome < gfAway ? "away" : "draw";

            if (homeIsFixtureHome) { if (outcome == "home") hv[0]++; else if (outcome == "draw") hv[1]++; else hv[2]++; }
            else { if (outcome == "away") av[0]++; else if (outcome == "draw") av[1]++; else av[2]++; }

            var margin = Math.Abs(m.HomeScore - m.AwayScore);
            if (margin > biggestMargin) { biggestMargin = margin; biggest = m; }

            recent.Add(new MiH2HMeetingDto
            {
                Date = m.MatchDate, Competition = m.Competition ?? "",
                HomeTeam = m.HomeTeamName, AwayTeam = m.AwayTeamName,
                HomeScore = m.HomeScore, AwayScore = m.AwayScore, Outcome = outcome,
            });
        }

        return new MiH2HDto
        {
            HomeTeam = homeName, AwayTeam = awayName,
            Played = h?.TotalMatches ?? matches.Count,
            HomeWins = h?.HomeWins ?? 0, Draws = h?.Draws ?? 0, AwayWins = h?.AwayWins ?? 0,
            HomeGoals = homeGoals, AwayGoals = awayGoals,
            HomeVenue = new MiVenueRecordDto { Played = hv[0] + hv[1] + hv[2], Wins = hv[0], Draws = hv[1], Losses = hv[2] },
            AwayVenue = new MiVenueRecordDto { Played = av[0] + av[1] + av[2], Wins = av[0], Draws = av[1], Losses = av[2] },
            BiggestScore = biggest != null
                ? new MiScoreNoteDto { Score = $"{biggest.HomeScore}-{biggest.AwayScore}", Note = biggest.Competition ?? "" }
                : new MiScoreNoteDto(),
            UnbeatenStreak = ComputeUnbeaten(recent, homeName),
            LastMeeting = recent.Count > 0 ? recent[0] : new MiH2HMeetingDto(),
            Recent = recent,
        };
    }

    private static MiStreakDto ComputeUnbeaten(List<MiH2HMeetingDto> recent, string homeName)
    {
        // recent[0] en yeni varsayımıyla, fikstür ev sahibinin güncel yenilgisiz serisi.
        int count = 0;
        foreach (var m in recent)
        {
            var homeLost = (m.Outcome == "home" && !string.Equals(m.HomeTeam, homeName, StringComparison.OrdinalIgnoreCase))
                        || (m.Outcome == "away" && string.Equals(m.HomeTeam, homeName, StringComparison.OrdinalIgnoreCase));
            if (homeLost) break;
            count++;
        }
        return new MiStreakDto { Team = homeName, Count = count };
    }

    private static List<MiEvidenceDto> H2HEvidence(MatchDetailDto d) => new()
    {
        new() { Label = "Galibiyet", Detail = $"Ev {d.H2H?.HomeWins ?? 0} · Dep {d.H2H?.AwayWins ?? 0}" },
        new() { Label = "Beraberlik", Detail = (d.H2H?.Draws ?? 0).ToString() },
        new() { Label = "Son karşılaşmalar", Detail = (d.H2H?.Matches?.Count ?? 0).ToString() },
    };

    // ── 10 News ────────────────────────────────────────────────────────────────
    // Görev #015: News Intelligence Engine çıktısından (importance/team/impact/hero gerçek).
    private static MiNewsDto? BuildNews(NewsIntelligenceDto intel)
    {
        if (intel.Articles.Count == 0) return null;

        return new MiNewsDto
        {
            HomeTeam = intel.HomeTeam,
            AwayTeam = intel.AwayTeam,
            Articles = intel.Articles.Select(a => new MiNewsArticleDto
            {
                Id = a.Id, Title = a.Title, Summary = a.Summary, Source = a.Source, Time = a.Time,
                Type = a.Type, Importance = a.Importance, Team = a.Team, Impact = a.Impact, Hero = a.Hero
            }).ToList(),
            AiSummary = intel.AiSummary,
            Confidence = intel.Confidence,
            ImportanceScore = intel.ImportanceScore,
            Impact = intel.Impact,
        };
    }

    private static List<MiEvidenceDto> NewsEvidence(NewsIntelligenceDto intel)
        => intel.Articles.Count == 0
            ? new() { new() { Label = "Haber sayısı", Detail = "0" } }
            : intel.Evidence.Select(e => new MiEvidenceDto { Label = e.Label, Detail = e.Detail }).ToList();

    // ── 11 Match Outlook (backend türetir) ─────────────────────────────────────
    // Görev #016: MatchOutlookEngine çıktısından (form/performans/gol/tempo/denge/momentum/güven).
    private static MiMatchOutlookDto BuildOutlook(MatchOutlookDto o) => new()
    {
        Home = ToMiOutlookTeam(o.Home),
        Away = ToMiOutlookTeam(o.Away),
        GoalTrend = o.GoalTrend,
        Tempo = o.Tempo,
        Balance = o.Balance,
        Momentum = o.Momentum,
        ConfidenceSummary = new MiConfidenceDto { Label = o.ConfidenceSummary.Label, Value = o.ConfidenceSummary.Value },
        HeroOutlook = o.HeroOutlook,
        AiSummary = o.AiSummary,
    };

    private static MiTeamOutlookDto ToMiOutlookTeam(OutlookTeamDto t) => new()
    {
        Team = t.Team, Form = t.Form, Performance = t.Performance,
        PerformanceScore = t.PerformanceScore, GoalsFor = t.GoalsFor, GoalsAgainst = t.GoalsAgainst,
    };

    private static List<MiEvidenceDto> OutlookEvidence(MatchOutlookDto o)
        => o.Evidence.Select(e => new MiEvidenceDto { Label = e.Label, Detail = e.Detail }).ToList();

    // ── 12 Market Intelligence (kısmi) ─────────────────────────────────────────
    // Görev #017: MarketIntelligenceEngine çıktısından (eğilim/açılış→güncel/volatilite/kararlılık/sapma).
    private static MiMarketDto BuildMarket(MarketIntelDto o) => new()
    {
        HomeTeam = o.HomeTeam, AwayTeam = o.AwayTeam,
        MarketTrend = o.MarketTrend,
        OpeningState = o.OpeningState,
        CurrentState = o.CurrentState,
        Direction = o.Direction,
        Lean = o.Lean,
        Stability = o.Stability,
        Volatility = o.Volatility,
        SignificantChange = new MiMarketChangeDto
        {
            Label = o.SignificantChange.Label, Note = o.SignificantChange.Note, Direction = o.SignificantChange.Direction,
        },
        AiConfidence = new MiConfidenceDto { Label = o.ConfidenceSummary.Label, Value = o.ConfidenceSummary.Value },
        HeroSummary = o.HeroSummary,
        AiSummary = o.AiSummary,
    };

    private static List<MiEvidenceDto> MarketEvidence(MarketIntelDto o)
        => o.Evidence.Select(e => new MiEvidenceDto { Label = e.Label, Detail = e.Detail }).ToList();

    // Görev #018: WatchLiveEngine çıktısından (resmi yayıncı/platform/durum/bölge/kalite).
    private static MiWatchLiveDto BuildWatchLive(WatchLiveDto w) => new()
    {
        Broadcaster = w.Broadcaster,
        Platform = w.Platform,
        Availability = w.Availability,
        AvailabilityLabel = w.AvailabilityLabel,
        Region = w.Region,
        StreamQuality = w.StreamQuality,
        MatchCoverage = w.MatchCoverage,
        AiRecommendation = w.AiRecommendation,
        HeroSummary = w.HeroSummary,
        AiSummary = w.AiSummary,
    };

    private static List<MiEvidenceDto> WatchLiveEvidence(WatchLiveDto w)
        => w.Evidence.Select(e => new MiEvidenceDto { Label = e.Label, Detail = e.Detail }).ToList();

    // ── 14 Live Intelligence (backend türetir; canlı değilse null) ─────────────
    // Görev #019: LiveIntelligenceEngine çıktısından. Pre (canlı veri yok) → null → generic fallback.
    private static MiLiveDto? BuildLive(LiveIntelligenceDto o)
    {
        if (o.MatchStatus == "pre") return null;

        return new MiLiveDto
        {
            HomeTeam = o.HomeTeam, AwayTeam = o.AwayTeam,
            MatchMinute = o.MatchMinute,
            Score = new MiLiveScoreDto { Home = o.Score.Home, Away = o.Score.Away },
            MatchStatus = o.MatchStatus,
            StatusLabel = o.StatusLabel,
            Momentum = o.Momentum,
            MomentumValue = o.MomentumValue,
            MatchRhythm = o.MatchRhythm,
            KeyEvent = new MiLiveKeyEventDto
            {
                Minute = o.KeyEvent.Minute, Type = o.KeyEvent.Type,
                Description = o.KeyEvent.Description, Team = o.KeyEvent.Team,
            },
            LiveSummary = o.HeroSummary,
            AiConfidence = new MiConfidenceDto { Label = "Güven", Value = o.Confidence },
            AiSummary = o.AiSummary,
        };
    }

    private static List<MiEvidenceDto> LiveEvidence(LiveIntelligenceDto o)
        => o.Evidence.Select(e => new MiEvidenceDto { Label = e.Label, Detail = e.Detail }).ToList();

    // ── Utilities ──────────────────────────────────────────────────────────────
    private static string MapStatus(string? raw)
    {
        var s = (raw ?? "").ToLowerInvariant();
        if (s.Contains("live") || s.Contains("1h") || s.Contains("2h") || s.Contains("ht")) return "LIVE";
        if (s.Contains("fin") || s.Contains("ft") || s.Contains("end") || s.Contains("aet")) return "FT";
        return "PRE";
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
}
