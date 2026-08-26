using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;
using Formax.Application.Services.Matches;
using Formax.Domain.Entities;

namespace Formax.Application.AI.Context
{
    /// <summary>
    /// FORMAX AI Evolution — GDP-türevli maç sinyallerini <see cref="UnifiedMatchAiContext"/>'e
    /// birleştiren tek katman (GDP → AI Signals seam). Motor artık bu context'i okur; ham veri
    /// toplamaz. Yeni provider/sinyal eklendiğinde YALNIZ bu builder gelişir; motor değişmez (OCP).
    ///
    /// FAZ 1: gol/H2H/GücSkoru haritalandı (davranış korundu).
    /// FAZ 2: gerçek gol verisinden Strength/DataQuality üretildi.
    /// FAZ 3: GERÇEK provider verisinden Player Availability (MatchPlayerStatuses) dolduruldu.
    ///        Veri yoksa HasData=false (asla fake üretilmez).
    /// </summary>
    public sealed class MatchAiContextBuilder : IMatchAiContextBuilder
    {
        private readonly IMatchPlayerStatusRepository _playerStatusRepo;
        private readonly ITeamReadRepository _teamRepo;
        private readonly IMatchReadRepository _matchRepo;
        private readonly ILeagueStandingRepository _standingRepo;
        private readonly IMatchEvidenceRepository _evidenceRepo;
        private readonly ITeamSeasonStatisticRepository _teamStatRepo;
        private readonly IMatchPredictionSignalRepository _predictionRepo;
        private readonly ITeamProfileSignalRepository _teamProfileRepo;
        private readonly ITeamPlayerIntelligenceRepository _playerIntelRepo; // Football Intelligence v1.0
        private readonly ISocialPostRepository _socialRepo;
        private readonly ICompetitionContextRepository _competitionCtxRepo; // v2.5 — MEVCUT repo (yeni değil)
        private readonly IMatchLiveStatsRepository _liveStatsRepo;          // v3 — MEVCUT canlı stats repo (yeni değil)
        private readonly FormaxMatchIdFactory _matchIdFactory;

        // FAZ 1 — Mevcut veriyi AI'a bağlama: TeamComparison/H2H tek ortak kaynaktan (yeni hesap YOK),
        // GücSkoru mevcut hesaplayıcıdan. İkisi de yalnız Matches tablosunu okur → ek API/job YOK.
        private readonly Services.Matches.MatchComparisonFactory _comparisonFactory;
        private readonly IGucSkoruCalculator _gucSkoruCalculator;

        // İstek-içi külçe: canonical Team.Id → external (api-football) team id. null da saklanır.
        // Builder Scoped olduğu için istek bitince düşer. Bkz. ResolveExternalId.
        private readonly Dictionary<int, int?> _externalTeamIdCache = new();

        // AI Signal Factory — durumsuz (stateless); context alt-sinyallerini tek sinyal listesine çevirir.
        private static readonly Formax.Application.AI.Signals.AiSignalFactory _signalFactory = new();

        public MatchAiContextBuilder(
            IMatchPlayerStatusRepository playerStatusRepo,
            ITeamReadRepository teamRepo,
            IMatchReadRepository matchRepo,
            ILeagueStandingRepository standingRepo,
            IMatchEvidenceRepository evidenceRepo,
            ITeamSeasonStatisticRepository teamStatRepo,
            IMatchPredictionSignalRepository predictionRepo,
            ITeamProfileSignalRepository teamProfileRepo,
            ITeamPlayerIntelligenceRepository playerIntelRepo,
            ISocialPostRepository socialRepo,
            ICompetitionContextRepository competitionCtxRepo,
            IMatchLiveStatsRepository liveStatsRepo,
            FormaxMatchIdFactory matchIdFactory,
            MatchComparisonFactory comparisonFactory,
            IGucSkoruCalculator gucSkoruCalculator)
        {
            _comparisonFactory = comparisonFactory;
            _gucSkoruCalculator = gucSkoruCalculator;
            _playerStatusRepo = playerStatusRepo;
            _teamRepo = teamRepo;
            _matchRepo = matchRepo;
            _standingRepo = standingRepo;
            _evidenceRepo = evidenceRepo;
            _teamStatRepo = teamStatRepo;
            _predictionRepo = predictionRepo;
            _teamProfileRepo = teamProfileRepo;
            _playerIntelRepo = playerIntelRepo;
            _socialRepo = socialRepo;
            _competitionCtxRepo = competitionCtxRepo;
            _liveStatsRepo = liveStatsRepo;
            _matchIdFactory = matchIdFactory;
        }

        /// <summary>
        /// SELF-SERVE giriş — TeamComparison / H2H / GücSkoru çağırandan gelmez, burada üretilir.
        ///
        /// ÖNCESİ: Discover, Decision, Voice, Narrative ve Signals yüzeyleri <c>new TeamComparisonDto()</c>,
        /// <c>new H2HDto()</c> ve sabit <c>50</c> geçiyordu → beklenen goller takım verisi yerine lig
        /// baseline'ına düşüyor, DataQuality 0 kalıyordu. Artık beşi de bu yolu kullanır.
        ///
        /// Detail use-case'in dolu veriyle çağırdığı 9-parametreli overload AYNEN korunur.
        /// Veri gerçekten yoksa (yeni takım / maçsız kayıt) alt hesaplar boş DTO döner — uydurma YOK.
        /// </summary>
        public UnifiedMatchAiContext Build(
            int matchId,
            int homeTeamId,
            int awayTeamId,
            string homeName,
            string awayName)
        {
            var home = _comparisonFactory.BuildTeamComparison(homeTeamId, homeOnly: true);
            var away = _comparisonFactory.BuildTeamComparison(awayTeamId, awayOnly: true);
            var h2h = _comparisonFactory.BuildH2H(homeTeamId, awayTeamId);
            var gucSkoru = _gucSkoruCalculator.CalculateForMatch(matchId).GucSkoru;

            return Build(matchId, homeTeamId, awayTeamId, home, away, h2h, gucSkoru, homeName, awayName);
        }

        public UnifiedMatchAiContext Build(
            int matchId,
            int homeTeamId,
            int awayTeamId,
            TeamComparisonDto home,
            TeamComparisonDto away,
            H2HDto h2h,
            int gucSkoru,
            string homeName,
            string awayName)
        {
            var match = _matchRepo.GetById(matchId);
            var standings = BuildStandings(match, homeTeamId, awayTeamId);
            var teamStats = BuildTeamStats(match, homeTeamId, awayTeamId);
            // v2.5 — CompetitionContext (MEVCUT canonical satır) tek kez okunur; boşsa HasData=false.
            var compCtx = match != null ? _competitionCtxRepo.GetByMatchId(match.Id) : null;

            var context = new UnifiedMatchAiContext
            {
                MatchId  = matchId,
                HomeName = string.IsNullOrWhiteSpace(homeName) ? "Ev sahibi" : homeName,
                AwayName = string.IsNullOrWhiteSpace(awayName) ? "Deplasman" : awayName,
                GucSkoru = gucSkoru,
                Home     = MapTeam(home),
                Away     = MapTeam(away),
                H2H      = new H2HAiSignals
                {
                    TotalMatches = h2h?.TotalMatches ?? 0,
                    HomeWins     = h2h?.HomeWins     ?? 0,
                    AwayWins     = h2h?.AwayWins     ?? 0,
                    Draws        = h2h?.Draws        ?? 0
                },
                // İKİ AKIŞIN AYRILDIĞI YER: motor dar kapsamı, anlatı tam kaydı görür.
                Availability     = BuildAvailability(matchId, homeTeamId, awayTeamId, engineScope: true),
                AvailabilityFull = BuildAvailability(matchId, homeTeamId, awayTeamId, engineScope: false),
                Standings    = standings,
                News         = BuildNewsEvidence(match, homeName, awayName),
                TeamStats    = teamStats,
                Prediction   = BuildPrediction(matchId),
                TeamProfile  = BuildTeamProfiles(homeTeamId, awayTeamId),
                Referee      = BuildReferee(match),
                Social       = BuildSocial(match, homeName, awayName),
                Strength     = BuildStrength(home, away, standings, teamStats),
                DataQuality  = ComputeDataQuality(home, away),

                // ── v2.5 READ-ONLY bağlam blokları (mevcut canonical kaynaklardan; yeni veri YOK) ──
                Competition      = BuildCompetition(compCtx),
                Tournament       = BuildTournament(compCtx),
                Season           = BuildSeason(match, standings),
                StandingsContext = BuildStandingsContext(match, homeTeamId, awayTeamId),
                LiveState        = BuildLiveState(match),
                // v2 Timeline Intelligence — takımın geçmiş+gelecek fikstüründen türetilir (mevcut Matches).
                Timeline         = BuildTimeline(match, homeTeamId, awayTeamId),
                // Football Intelligence v1.0 — oyuncu/kadro zekâsı (GERÇEK TeamPlayerIntelligence deposundan).
                PlayerIntelligence = BuildPlayerIntelligence(homeTeamId, awayTeamId)
            };

            // AI Signal Factory — tüm bloklar doldu; ham veriyi futbol anlamı sinyallerine dönüştür
            // (füzyon + çelişki çözümü). Çıktı YALNIZ context.Signals'a yazılır (motora/LLM'e değil).
            context.Signals = _signalFactory.Produce(context);
            // Unified AI Context kalite/özet paketi (agregat + Conflict Management + reasoning hints).
            context.Quality = _signalFactory.BuildQuality(context.Signals,
                new System.Collections.Generic.List<string> { context.HomeName, context.AwayName });
            return context;
        }

        // v2 Timeline Intelligence — YALNIZ mevcut Matches tablosundan (yeni provider/migration YOK).
        // Ham liste taşımaz; dinlenme/sonraki maç/yoğunluk/rotasyon TÜRETİR. Komşu maç yoksa HasData=false.
        private TimelineSignals BuildTimeline(Match? match, int homeTeamId, int awayTeamId)
        {
            if (match == null) return new TimelineSignals { HasData = false };
            var home = BuildTeamTimeline(match, homeTeamId);
            var away = BuildTeamTimeline(match, awayTeamId);
            return new TimelineSignals { Home = home, Away = away, HasData = home.HasData || away.HasData };
        }

        private TeamTimelineSignals BuildTeamTimeline(Match match, int teamId)
        {
            var date = match.MatchDate;
            var league = match.LeagueId;
            var q = _matchRepo.Query().Where(m => m.Id != match.Id && m.LeagueId > 0
                        && (m.HomeTeamId == teamId || m.AwayTeamId == teamId));

            var prev = q.Where(m => m.MatchDate < date).OrderByDescending(m => m.MatchDate).FirstOrDefault();
            var next = q.Where(m => m.MatchDate > date).OrderBy(m => m.MatchDate).FirstOrDefault();
            if (prev == null && next == null) return new TeamTimelineSignals { HasData = false };

            var restDays = prev != null ? (int)System.Math.Round((date - prev.MatchDate).TotalDays) : 0;
            var daysToNext = next != null ? (int)System.Math.Round((next.MatchDate - date).TotalDays) : 0;
            var nextDiffComp = next != null && league > 0 && next.LeagueId > 0 && next.LeagueId != league;
            var last14 = q.Count(m => m.MatchDate < date && m.MatchDate >= date.AddDays(-14));
            var next14 = q.Count(m => m.MatchDate > date && m.MatchDate <= date.AddDays(14));

            var shortRest = prev != null && restDays >= 0 && restDays <= 3;
            // Uzun dinlenme AVANTAJI yalnız makul aralıkta (6-14g) anlamlı; >14g = program boşluğu/ara
            // (özellikle milli takımlar), dinlenme avantajı DEĞİL → çıkarım üretme (yanıltıcı olmasın).
            var longRest = prev != null && restDays >= 6 && restDays <= 14;
            var congested = (last14 + next14) >= 2 || (next != null && daysToNext <= 3 && daysToNext >= 0) || shortRest;
            var upcomingPriority = next != null && daysToNext <= 4 && daysToNext >= 0 && nextDiffComp;
            var rotationRisk = upcomingPriority || (congested && next != null && daysToNext <= 4 && daysToNext >= 0);

            return new TeamTimelineSignals
            {
                HasData = true,
                HasPrevMatch = prev != null,
                RestDaysBefore = restDays,
                HasNextMatch = next != null,
                DaysToNextMatch = daysToNext,
                NextIsDifferentCompetition = nextDiffComp,
                MatchesLast14 = last14,
                MatchesNext14 = next14,
                ShortRest = shortRest,
                LongRestAdvantage = longRest,
                CongestedSchedule = congested,
                UpcomingPriorityMatch = upcomingPriority,
                RotationRisk = rotationRisk
            };
        }

        // Phase 7 — Resmi sosyal medya sinyalleri GERÇEK Canonical SocialPost'tan (verified hesaplar).
        // Identity: SocialPost FORMAX_MATCH_ID ile bağlıdır (news ile aynı kimlik) → aynı hesaplama.
        // Coverage yoksa HasData=false (fake yok). Motor bu fazda okumaz (hazır bulunur).
        private SocialAiSignals BuildSocial(Match? match, string homeName, string awayName)
        {
            if (match == null) return new SocialAiSignals { HasData = false };

            string formaxMatchId;
            try { formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName); }
            catch { return new SocialAiSignals { HasData = false }; }

            var posts = _socialRepo.GetByMatch(formaxMatchId, 40);
            if (posts == null || posts.Count == 0)
                return new SocialAiSignals { HasData = false };

            int Sig(params string[] types) => posts.Count(p =>
                types.Any(t => string.Equals(p.SignalType, t, StringComparison.OrdinalIgnoreCase)));

            var now = DateTime.UtcNow;
            var lineup = Sig("Lineup");
            var sourceTrust = posts.Max(p => p.SourceTrust);

            return new SocialAiSignals
            {
                HasData                      = true,
                OfficialAnnouncement         = posts.Count,
                OfficialSquadAnnouncement    = lineup,
                OfficialLineupAnnouncement   = lineup,
                OfficialInjuryAnnouncement   = Sig("Injury"),
                OfficialTransferAnnouncement = Sig("Transfer"),
                OfficialCoachStatement       = Sig("Coach"),
                OfficialClubStatement        = Sig("Club Statement"),
                OfficialCompetitionStatement = 0, // lig/turnuva hesabı seed'lenince dolar
                OfficialFederationStatement  = 0, // federasyon hesabı seed'lenince dolar
                OfficialPlayerStatement      = 0, // oyuncu-kapsamlı hesap yok (fake yok)
                BreakingOfficialNews         = posts.Any(p => p.PublishedUtc >= now.AddHours(-6)),
                SourceTrust                  = sourceTrust,
                EvidenceScore                = sourceTrust, // resmi kaynak = kanıt gücü
                Confidence                   = Math.Clamp(70 + Math.Min(29, posts.Count * 3), 0, 99),
                LatestPublishedUtc           = posts.Max(p => p.PublishedUtc).ToString("u"),
                HasOfficialSource            = true,
                HasSocialSource              = true
            };
        }

        // Phase 6 Final — Takım profili (coach/venue/squad/transfers) GERÇEK TeamProfileSignals'ten.
        // AI/GDP-only. Kayıt internal Team.Id ile saklandığından doğrudan home/away id ile okunur.
        // Her alt-blok bağımsız coverage-gated. Coverage yoksa HasData=false (fake yok).
        private TeamProfileAiSignals BuildTeamProfiles(int homeTeamId, int awayTeamId)
        {
            var home = MapTeamProfile(_teamProfileRepo.GetByTeam(homeTeamId));
            var away = MapTeamProfile(_teamProfileRepo.GetByTeam(awayTeamId));
            return new TeamProfileAiSignals
            {
                Home = home,
                Away = away,
                HasData = home.HasData && away.HasData
            };
        }

        private static TeamProfileTeamSignals MapTeamProfile(TeamProfileSignal? s)
        {
            if (s == null) return new TeamProfileTeamSignals { HasData = false };
            return new TeamProfileTeamSignals
            {
                HasCoach           = s.HasCoach,
                CoachName          = s.CoachName ?? string.Empty,
                CoachAge           = s.CoachAge,
                HasVenue           = s.HasVenue,
                VenueName          = s.VenueName ?? string.Empty,
                VenueCity          = s.VenueCity ?? string.Empty,
                VenueCapacity      = s.VenueCapacity,
                VenueSurface       = s.VenueSurface ?? string.Empty,
                HasSquad           = s.HasSquad,
                SquadSize          = s.SquadSize,
                SquadAvgAge        = s.SquadAvgAge,
                HasTransfers       = s.HasTransfers,
                RecentTransfersIn  = s.RecentTransfersIn,
                RecentTransfersOut = s.RecentTransfersOut,
                HasData            = s.HasCoach || s.HasVenue || s.HasSquad || s.HasTransfers
            };
        }

        // Football Intelligence v1.0 — Player/Squad Intelligence GERÇEK TeamPlayerIntelligence'ten.
        // Kayıt internal Team.Id ile saklandığından doğrudan home/away id ile okunur. Coverage yoksa
        // HasData=false (fake YOK). Motor bunu YALNIZ editoryal blok için okur; olasılığa GİRMEZ (hash sabit).
        private PlayerIntelligenceAiSignals BuildPlayerIntelligence(int homeTeamId, int awayTeamId)
        {
            var home = MapPlayerIntel(_playerIntelRepo.GetByTeam(homeTeamId));
            var away = MapPlayerIntel(_playerIntelRepo.GetByTeam(awayTeamId));
            return new PlayerIntelligenceAiSignals
            {
                Home = home,
                Away = away,
                HasData = home.HasData || away.HasData
            };
        }

        private static TeamPlayerSignals MapPlayerIntel(TeamPlayerIntelligence? s)
        {
            if (s == null || !s.HasData) return new TeamPlayerSignals { HasData = false };
            var injured = string.IsNullOrWhiteSpace(s.InjuredNames)
                ? new List<string>()
                : s.InjuredNames.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();
            return new TeamPlayerSignals
            {
                HasData              = true,
                SquadPlayerCount     = s.SquadPlayerCount,
                GkCount              = s.GkCount,
                DefCount             = s.DefCount,
                MidCount             = s.MidCount,
                AttCount             = s.AttCount,
                TopScorerName        = s.TopScorerName ?? "",
                TopScorerGoals       = s.TopScorerGoals,
                TopScorerAssists     = s.TopScorerAssists,
                TopScorerRating      = s.TopScorerRating,
                TopAssistName        = s.TopAssistName ?? "",
                TopAssistCount       = s.TopAssistCount,
                KeyPlayerName        = s.KeyPlayerName ?? "",
                KeyPlayerRating      = s.KeyPlayerRating,
                MinutesLeaderName    = s.MinutesLeaderName ?? "",
                MinutesLeaderMinutes = s.MinutesLeaderMinutes,
                InjuredCount         = s.InjuredCount,
                InjuredNames         = injured,
                InjuredDefCount      = s.InjuredDefCount,
                InjuredMidCount      = s.InjuredMidCount,
                InjuredAttCount      = s.InjuredAttCount,
                TeamTotalGoals        = s.TeamTotalGoals,
                TopScorerGoalSharePct = s.TopScorerGoalSharePct,
                Top2GoalSharePct      = s.Top2GoalSharePct,
                OneManDependency      = s.OneManDependency,
                DefenseLeaderName     = s.DefenseLeaderName ?? "",
                DefenseLeaderRating   = s.DefenseLeaderRating,
                MidfieldBrainName     = s.MidfieldBrainName ?? "",
                MidfieldBrainAssists  = s.MidfieldBrainAssists,
                MidfieldBrainRating   = s.MidfieldBrainRating,
                ShotsLeaderName       = s.ShotsLeaderName ?? "",
                ShotsLeaderCount      = s.ShotsLeaderCount,
                KeyPassLeaderName     = s.KeyPassLeaderName ?? "",
                KeyPassLeaderCount    = s.KeyPassLeaderCount,
                CardRiskName          = s.CardRiskName ?? "",
                CardRiskYellows       = s.CardRiskYellows
            };
        }

        // Phase 6 Final — Hakem kimlik sinyali mevcut Match.Referee'den (ek API çağrısı yok).
        // Atanmadıysa HasData=false.
        private static RefereeAiSignals BuildReferee(Match? match)
        {
            var name = match?.Referee;
            if (string.IsNullOrWhiteSpace(name))
                return new RefereeAiSignals { HasData = false };
            return new RefereeAiSignals { HasData = true, RefereeName = name.Trim() };
        }

        // Phase 6 / Slice 2 — Provider öngörüsü GERÇEK /predictions verisinden (MatchPredictionSignals).
        // YALNIZ AI sinyali; kullanıcıya gösterilmez, motor bu fazda okumaz (hazır bulunur).
        // Identity: satır zaten canonical Match.Id'ye bağlı (ingestion ExternalMatchId→Match.Id çözdü) →
        // doğrudan matchId ile okunur. Coverage yoksa HasData=false (fake yok).
        private PredictionAiSignals BuildPrediction(int matchId)
        {
            var p = _predictionRepo.GetByMatchId(matchId);
            if (p == null)
                return new PredictionAiSignals { HasData = false };

            var confidence = Math.Max(p.PercentHome, Math.Max(p.PercentDraw, p.PercentAway));

            // Provider'ın öne çıkardığı sonuç: açık kazanan tarafı, yoksa en yüksek yüzde.
            string side = p.WinnerSide;
            if (string.IsNullOrEmpty(side))
            {
                if (p.PercentHome >= p.PercentDraw && p.PercentHome >= p.PercentAway) side = "Home";
                else if (p.PercentAway >= p.PercentHome && p.PercentAway >= p.PercentDraw) side = "Away";
                else side = "Draw";
            }

            // DataQuality: yüzdeler ~100 toplanıyor mu (0.7) + karşılaştırma güç verisi var mı (0.3).
            var pctSum = p.PercentHome + p.PercentDraw + p.PercentAway;
            var quality = 0.0;
            if (pctSum is >= 90 and <= 110) quality += 0.7;
            if (p.ComparisonTotalHome > 0 || p.ComparisonTotalAway > 0) quality += 0.3;

            return new PredictionAiSignals
            {
                HasData                = true,
                PredictionConfidence   = confidence,
                ProviderPrediction     = side,
                ProviderRecommendation = p.Advice ?? string.Empty,
                ProviderTrendHome      = p.ComparisonTotalHome,
                ProviderTrendAway      = p.ComparisonTotalAway,
                PercentHome            = p.PercentHome,
                PercentDraw            = p.PercentDraw,
                PercentAway            = p.PercentAway,
                WinOrDraw              = p.WinOrDraw,
                UnderOver              = p.UnderOver ?? string.Empty,
                DataQuality            = quality
            };
        }

        // Phase 6 — Takım sezon istatistikleri GERÇEK /teams/statistics verisinden (TeamSeasonStatistics).
        // IDENTITY: TeamSeasonStatistic.TeamId = provider EXTERNAL id (standings/player-status ile aynı) →
        // canonical Team → ExternalTeamId ile çözülür; lig = Match.LeagueId (external), sezon aynı kuralla.
        // Coverage yoksa satır bulunmaz → HasData=false (fake yok).
        private TeamStatsAiSignals BuildTeamStats(Match? match, int homeTeamId, int awayTeamId)
        {
            if (match == null)
                return new TeamStatsAiSignals();

            var season = match.MatchDate.Month >= 7 ? match.MatchDate.Year : match.MatchDate.Year - 1;
            var homeExt = ResolveExternalId(homeTeamId);
            var awayExt = ResolveExternalId(awayTeamId);

            var home = homeExt.HasValue
                ? MapTeamStats(_teamStatRepo.GetByTeam(match.LeagueId, season, homeExt.Value))
                : new TeamSeasonAiSignals { HasData = false };
            var away = awayExt.HasValue
                ? MapTeamStats(_teamStatRepo.GetByTeam(match.LeagueId, season, awayExt.Value))
                : new TeamSeasonAiSignals { HasData = false };

            return new TeamStatsAiSignals
            {
                Home = home,
                Away = away,
                HasData = home.HasData && away.HasData
            };
        }

        private static TeamSeasonAiSignals MapTeamStats(TeamSeasonStatistic? s)
        {
            if (s == null) return new TeamSeasonAiSignals { HasData = false };
            return new TeamSeasonAiSignals
            {
                Played               = s.PlayedTotal,
                Wins                 = s.WinsTotal,
                Draws                = s.DrawsTotal,
                Loses                = s.LosesTotal,
                GoalsForAvgTotal     = s.GoalsForAvgTotal,
                GoalsForAvgHome      = s.GoalsForAvgHome,
                GoalsForAvgAway      = s.GoalsForAvgAway,
                GoalsAgainstAvgTotal = s.GoalsAgainstAvgTotal,
                GoalsAgainstAvgHome  = s.GoalsAgainstAvgHome,
                GoalsAgainstAvgAway  = s.GoalsAgainstAvgAway,
                CleanSheets          = s.CleanSheetTotal,
                FailedToScore        = s.FailedToScoreTotal,
                Form                 = s.Form ?? string.Empty,
                HasData              = true
            };
        }

        // Phase 5 — News/Evidence GERÇEK MatchEvidenceRecords'tan (Data Engine v2.1). Kanıt zaten
        // signal-typed + kaynak-kalite skorlu (SourceQuality 60+ = güvenilir; fan/dedikodu ingest'te
        // elenmiş). FORMAX_MATCH_ID ile maça bağlanır (identity matching). Kanıt yoksa HasData=false.
        // NOT: evidence repo yalnız async okuma sunar; senkron Build içinde ASP.NET Core'da
        // SynchronizationContext olmadığından GetAwaiter().GetResult() güvenli (deadlock yok).
        private NewsEvidenceSignals BuildNewsEvidence(Match? match, string homeName, string awayName)
        {
            if (match == null) return new NewsEvidenceSignals();

            string formaxMatchId;
            try { formaxMatchId = _matchIdFactory.Create(match.MatchDate, homeName, awayName); }
            catch { return new NewsEvidenceSignals { HasData = false }; }

            // Takım adları VE kickoff geçilir → okuma tarafı maç bağlama kapısı ve haberin
            // maça göre zaman konumu (maç öncesi / maç günü / eski) uygulanabilir; eski,
            // kapısız dönemde yazılmış kanıt AI'a sızmaz.
            var ev = _evidenceRepo.GetContextAsync(formaxMatchId, homeName, awayName, match.MatchDate)
                                  .GetAwaiter().GetResult();
            if (ev == null || ev.TotalEvidence <= 0)
                return new NewsEvidenceSignals { HasData = false };

            int Sig(params string[] types) =>
                types.Sum(t => ev.Signals.TryGetValue(t, out var c) ? c : 0);

            var sourceTrust = ev.TopEvidence.Count > 0 ? ev.TopEvidence.Max(e => e.SourceQuality) : 0;
            var signals = ev.Signals.Keys
                .Where(k => !k.Equals("General", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Global News Platform — breaking: son 6 saatte yüksek-kaliteli kaynaktan kanıt.
            var now = DateTime.UtcNow;
            var hasBreaking = ev.TopEvidence.Any(e =>
                e.SourceQuality >= 85 && e.PublishedUtc >= now.AddHours(-6));
            // Tier-1 resmi kaynak (federasyon/resmi kulüp) = SourceQuality ≥ 95.
            var hasOfficialSource = ev.TopEvidence.Any(e => e.SourceQuality >= 95);
            // NewsConfidence: kanıt gücü (EvidenceScore) + hacim harmanı (daha çok kanıt → daha yüksek).
            var newsConfidence = Math.Clamp(ev.Confidence + Math.Min(10, ev.TotalEvidence / 2), 0, 100);

            return new NewsEvidenceSignals
            {
                HasData          = true,
                TotalEvidence    = ev.TotalEvidence,
                EvidenceScore    = ev.Confidence,
                SourceTrust      = sourceTrust,
                PlayerNews       = Sig("Injury", "Suspension", "Lineup"),
                CoachNews        = Sig("Coach"),
                ClubNews         = Sig("Transfer", "Club Statement", "Schedule", "Derby"),
                // Granular kategoriler (evidence Type histogramından — gerçek, türetilmiş).
                InjuryNews       = Sig("Injury"),
                SuspensionNews   = Sig("Suspension"),
                TransferNews     = Sig("Transfer"),
                CompetitionNews  = Sig("Schedule", "Derby", "Referee"),
                OfficialAnnouncements = Sig("Club Statement"),
                NewsConfidence   = newsConfidence,
                HasBreakingNews  = hasBreaking,
                HasOfficialSource = hasOfficialSource,
                HasSocialSource  = false, // SocialDiscoveryJob (Tier-3 resmi sosyal) — gelecek faz
                HasOfficialAnnouncement = ev.Signals.ContainsKey("Club Statement"),
                HasOfficialNews  = sourceTrust >= 80,
                TopSignal        = signals
                    .OrderByDescending(k => ev.Signals[k]).FirstOrDefault() ?? string.Empty,
                NewsSignals      = signals.OrderByDescending(k => ev.Signals[k]).Take(6).ToList(),
                LatestPublishedUtc = ev.TopEvidence.Count > 0
                    ? ev.TopEvidence.Max(e => e.PublishedUtc).ToString("u")
                    : null
            };
        }

        // Phase 4 — League Standings GERÇEK LeagueStandings verisinden.
        // IDENTITY MATCHING (Identity & Coverage sprint): LeagueStandings.TeamId provider'ın
        // EXTERNAL takım id'sini tutar (WorldPerceptionDailyJob `TeamId = e.TeamId` yazar) —
        // player-status ile AYNI şema. Bu yüzden canonical Team → ExternalTeamId çözülür ve
        // standings external id üzerinden eşlenir. Season, match tarihinden aynı kuralla türetilir
        // (sync ile birebir). Coverage yoksa satır bulunmaz → HasData=false (fake yok).
        private StandingsAiSignals BuildStandings(Match? match, int homeTeamId, int awayTeamId)
        {
            if (match == null)
                return new StandingsAiSignals();

            var season = match.MatchDate.Month >= 7 ? match.MatchDate.Year : match.MatchDate.Year - 1;
            var homeExt = ResolveExternalId(homeTeamId);
            var awayExt = ResolveExternalId(awayTeamId);

            var home = homeExt.HasValue
                ? MapStanding(_standingRepo.GetByTeam(match.LeagueId, season, homeExt.Value))
                : new TeamStandingSignals { HasData = false };
            var away = awayExt.HasValue
                ? MapStanding(_standingRepo.GetByTeam(match.LeagueId, season, awayExt.Value))
                : new TeamStandingSignals { HasData = false };

            return new StandingsAiSignals
            {
                Home = home,
                Away = away,
                HasData = home.HasData && away.HasData
            };
        }

        private static TeamStandingSignals MapStanding(LeagueStanding? s)
        {
            if (s == null) return new TeamStandingSignals { HasData = false };
            return new TeamStandingSignals
            {
                Position       = s.Position,
                Played         = s.Played,
                Points         = s.Points,
                GoalsFor       = s.GoalsFor,
                GoalsAgainst   = s.GoalsAgainst,
                GoalDifference = s.GoalDifference,
                Form           = s.Form ?? string.Empty,
                HasData        = true
            };
        }

        // FAZ 3 — Player Availability YALNIZ gerçek provider verisinden (MatchPlayerStatuses).
        // Injured/Suspended = kesin eksik; Doubtful sayılmaz. Satır yoksa HasData=false (fake yok).
        //
        // IDENTITY MATCHING: MatchPlayerStatuses.TeamId provider'ın EXTERNAL takım id'sini tutar
        // (ör. 118/131); maçın home/away'i ise CANONICAL id'dir (ör. 3015/3016). Bu yüzden status'u
        // takıma bağlarken canonical Team → ExternalTeamId çözülür ve external id üzerinden eşlenir
        // (canonical id fallback'i de korunur). GDP identity-matching boşluğu burada kapatılır.
        /// <summary>
        /// MOTOR KAPSAMI (engineScope=true): kadro sinyali yalnız kickoff'a
        /// <see cref="EngineAvailabilityHorizon"/> kalan maçlarda motora verilir. Bu, sakatlık
        /// kaydı günler öncesinden toplanmaya başladığında mevcut Olası Sonuçların
        /// DEĞİŞMEMESİNİ garanti eder — motorun bugüne kadar fiilen gördüğü pencere budur.
        /// Formül, ceza katsayısı ve motor kodu DEĞİŞMEZ.
        ///
        /// ANLATI KAPSAMI (engineScope=false): pencere uygulanmaz; elde ne varsa o taşınır.
        /// </summary>
        private AvailabilityAiSignals BuildAvailability(
            int matchId, int homeTeamId, int awayTeamId, bool engineScope)
        {
            var statuses = _playerStatusRepo.GetByMatchId(matchId);
            if (statuses == null || statuses.Count == 0)
                return new AvailabilityAiSignals { HasData = false };

            if (engineScope && !WithinEngineHorizon(matchId))
                return new AvailabilityAiSignals { HasData = false };

            var homeExt = ResolveExternalId(homeTeamId);
            var awayExt = ResolveExternalId(awayTeamId);

            int Absences(int canonicalId, int? externalId) => statuses.Count(s =>
                IsAbsence(s.Status) &&
                (s.TeamId == canonicalId || (externalId.HasValue && s.TeamId == externalId.Value)));

            // EKSİĞİN NEDENİ (sakat / cezalı) — aynı kimlik çözümüyle, aynı kayıtlardan.
            // Yeni sorgu ya da kaynak YOK; motor bu alanları okumaz.
            int ByStatus(int canonicalId, int? externalId, string status) => statuses.Count(s =>
                (s.TeamId == canonicalId || (externalId.HasValue && s.TeamId == externalId.Value)) &&
                string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase));

            return new AvailabilityAiSignals
            {
                HomeKeyAbsences = Absences(homeTeamId, homeExt),
                AwayKeyAbsences = Absences(awayTeamId, awayExt),
                HomeInjured     = ByStatus(homeTeamId, homeExt, "Injured"),
                HomeSuspended   = ByStatus(homeTeamId, homeExt, "Suspended"),
                HomeDoubtful    = ByStatus(homeTeamId, homeExt, "Doubtful"),
                AwayInjured     = ByStatus(awayTeamId, awayExt, "Injured"),
                AwaySuspended   = ByStatus(awayTeamId, awayExt, "Suspended"),
                AwayDoubtful    = ByStatus(awayTeamId, awayExt, "Doubtful"),
                LineupConfirmed = false,
                HasData         = true
            };
        }

        /// <summary>
        /// Motorun kadro sinyalini gördüğü pencere — LineupIngestionJob'ın bugüne kadarki
        /// yoklama ufkuyla AYNI (60 dk). Değer burada sabittir ki motorun girdisi, veri
        /// toplama sıklığı değişse bile kaymasın.
        /// </summary>
        private static readonly TimeSpan EngineAvailabilityHorizon = TimeSpan.FromMinutes(60);

        private bool WithinEngineHorizon(int matchId)
        {
            var match = _matchRepo.Query().FirstOrDefault(m => m.Id == matchId);
            if (match == null) return false;

            var toKickoff = match.MatchDate - DateTime.UtcNow;
            // Başlamış/oynanmış maçta kayıt zaten kesinleşmiştir; ufuk yalnız ileriye bakar.
            return toKickoff <= EngineAvailabilityHorizon;
        }

        // KADRO FALLBACK'İ BİLEREK EKLENMEDİ (14.08 ölçümü): MatchPlayerStatuses yalnız
        // LineupIngestionJob tarafından, kickoff'a 60 dk kala yazılıyor; bu yüzden kilitli
        // kapsamdaki yaklaşan maçların tamamında boş. Takım-düzeyi TeamPlayerIntelligence
        // (InjuredCount) elde VAR, ama o SEZON GENELİ sakat listesidir (ör. Real Sociedad 29,
        // Rizespor 19) — "bu maçın kilit eksiği" DEĞİLDİR. Buradaki Availability'yi
        // MarketProbabilityEngine okuyor (absence başına ceza + kenar), dolayısıyla bu sayıyı
        // buraya koymak OLASI SONUÇLARI sessizce değiştirirdi. Doğru kaynak gelene kadar
        // HasData=false kalır; uydurma da yapılmaz, olasılık motoru da bozulmaz.

        /// <summary>
        /// Takımın external (api-football) id'si. Tek Build() içinde bu çözüm 8 kez istenir
        /// (Availability / TeamStats / StandingsContext / Standings blokları, ev+deplasman) ve
        /// <c>ITeamReadRepository.GetById</c> cache'siz olduğundan her biri ayrı SQL sorgusuydu.
        ///
        /// İSTEK-İÇİ MEMOIZASYON (yalnız perf; SONUÇ DEĞİŞMEZ): builder Scoped'tır → sözlük istek
        /// başına sıfırlanır, istekler arası sızma yoktur. Aynı desen GucSkoruCalculator'da da var.
        /// Bulunamayan takım (null) da cache'lenir; aksi halde kapsamı olmayan takımlarda
        /// sorgu tekrarı sürerdi.
        /// </summary>
        private int? ResolveExternalId(int canonicalTeamId)
        {
            if (_externalTeamIdCache.TryGetValue(canonicalTeamId, out var cached))
                return cached;

            var team = _teamRepo.GetById(canonicalTeamId);
            var ext = int.TryParse(team?.ExternalTeamId, out var parsed) ? parsed : (int?)null;

            _externalTeamIdCache[canonicalTeamId] = ext;
            return ext;
        }

        private static bool IsAbsence(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            var s = status.ToLowerInvariant();
            return s.Contains("injur") || s.Contains("sakat")
                || s.Contains("suspend") || s.Contains("ceza")
                || s.Contains("out");
        }

        // ══════════════════════ v2.5 READ-ONLY BAĞLAM BLOKLARI ══════════════════════
        // Hepsi MEVCUT canonical kaynaklardan (Match entity + CompetitionContext + LeagueStandings)
        // türetilir. Yeni provider/API/veri YOK. Kaynak boş/eksikse HasData=false (fake YOK).

        // Competition — MEVCUT CompetitionContext satırından. Tablo boşsa HasData=false (ilgili job
        // doldurunca otomatik aktif; motor değişmez — OCP). CompetitionType/Stage/Round CANONICAL'dir
        // (League adından TÜRETİLMEZ → hardcode yok).
        private static CompetitionContextSignals BuildCompetition(CompetitionContext? cc)
        {
            if (cc == null) return new CompetitionContextSignals { HasData = false };

            var stage = ClassifyStage(cc.StageName);
            var round = ParseTrailingInt(cc.StageName);
            var type = string.IsNullOrWhiteSpace(cc.CompetitionType) ? "League" : cc.CompetitionType;
            var (importance, elimination) = DeriveImportance(type, stage);

            return new CompetitionContextSignals
            {
                HasData         = true,
                CompetitionType = type,
                StageName       = cc.StageName ?? "",
                Stage           = stage,
                CurrentRound    = round,
                Importance      = importance,
                IsElimination   = elimination,
                Headline        = cc.ContextHeadline ?? "",
                Summary         = cc.ContextSummary ?? ""
            };
        }

        // Tournament — CompetitionType + bracket'ten. League tipinde anlamsız → HasData=false.
        // Knockout tek maç ise uzatma/penaltı olası (gerçek çıkarım). Leg bilgisi bracket olmadan
        // bilinmez → dürüstçe false.
        private static TournamentContextSignals BuildTournament(CompetitionContext? cc)
        {
            if (cc == null) return new TournamentContextSignals { HasData = false };
            var type = cc.CompetitionType ?? "League";
            if (type.Equals("League", StringComparison.OrdinalIgnoreCase))
                return new TournamentContextSignals { HasData = false };

            var hasBracket = !string.IsNullOrWhiteSpace(cc.BracketJson);
            // Knockout = tek maç eleme → uzatma/penaltı olası. Cup çift maçlı olabilir (leg bracket'te).
            var isKnockout = type.Equals("Knockout", StringComparison.OrdinalIgnoreCase);

            return new TournamentContextSignals
            {
                HasData           = true,
                IsTwoLegged       = false, // leg şeması bracket olmadan güvenilir çözülemez → dürüst
                IsFirstLeg        = false,
                IsSecondLeg       = false,
                AggregateMatters  = hasBracket,
                ExtraTimePossible = isKnockout,
                PenaltiesPossible = isKnockout,
                Summary           = $"{type} — eleme bağlamı" + (isKnockout ? " (uzatma/penaltı olası)." : ".")
            };
        }

        // Season — Match.MatchDate'ten (HER ZAMAN gerçek). Faz: sezon Temmuz'da başlar kabulü.
        // Round bazlı ilerleme CompetitionContext'e bağlı (bu blokta yalnız tarih tabanlı faz).
        private static SeasonContextSignals BuildSeason(Match? match, StandingsAiSignals standings)
        {
            if (match == null) return new SeasonContextSignals { HasData = false };
            var d = match.MatchDate;
            var seasonYear = d.Month >= 7 ? d.Year : d.Year - 1;

            // Sezon içi ay indeksi (Temmuz=0 ... Haziran=11).
            var idx = (d.Month - 7 + 12) % 12;
            var phase = idx <= 3 ? "Start" : idx <= 7 ? "Middle" : "End";

            var played = standings.HasData
                ? Math.Max(standings.Home.Played, standings.Away.Played)
                : 0;

            return new SeasonContextSignals
            {
                HasData              = true,
                SeasonYear           = seasonYear,
                SeasonPhase          = phase,
                MatchDateUtc         = d.ToString("u"),
                MatchesPlayedContext = played,
                Summary              = $"Sezon {seasonYear} — faz {phase}" +
                                       (played > 0 ? $", ~{played} maç oynanmış." : ".")
            };
        }

        // StandingsContext — TAM LeagueStandings tablosundan (lider farkı/title-race). Tablo kısmi ise
        // (yalnız üst sıralar) RelegationDataAvailable=false. GetByLeague MEVCUT repo metodu (yeni değil).
        private StandingsContextSignals BuildStandingsContext(Match? match, int homeTeamId, int awayTeamId)
        {
            if (match == null) return new StandingsContextSignals { HasData = false };
            var season = match.MatchDate.Month >= 7 ? match.MatchDate.Year : match.MatchDate.Year - 1;
            var table = _standingRepo.GetByLeague(match.LeagueId, season);
            if (table == null || table.Count == 0)
                return new StandingsContextSignals { HasData = false };

            var leader = table.OrderBy(s => s.Position).First();
            var leaderPts = table.Max(s => s.Points);

            var homeExt = ResolveExternalId(homeTeamId);
            var awayExt = ResolveExternalId(awayTeamId);
            LeagueStanding? Find(int canonical, int? ext) => table.FirstOrDefault(s =>
                s.TeamId == canonical || (ext.HasValue && s.TeamId == ext.Value));

            var homeRow = Find(homeTeamId, homeExt);
            var awayRow = Find(awayTeamId, awayExt);
            if (homeRow == null && awayRow == null)
                return new StandingsContextSignals { HasData = false };

            var homeGap = homeRow != null ? leaderPts - homeRow.Points : -1;
            var awayGap = awayRow != null ? leaderPts - awayRow.Points : -1;
            var minGap = new[] { homeGap, awayGap }.Where(g => g >= 0).DefaultIfEmpty(999).Min();

            return new StandingsContextSignals
            {
                HasData                 = true,
                KnownRows               = table.Count,
                LeaderPoints            = leaderPts,
                LeaderName              = leader.TeamName ?? "",
                HomeGapToLeader         = homeGap,
                AwayGapToLeader         = awayGap,
                TitleRace               = minGap <= 6, // ~2 galibiyet içinde → şampiyonluk yarışı
                RelegationDataAvailable = table.Count >= 18, // tam üst-lig tablosu değilse dürüstçe false
                Summary                 = $"Lider {leader.TeamName} {leaderPts}p; " +
                                          $"Ev -{(homeGap >= 0 ? homeGap.ToString() : "?")}p / Dep -{(awayGap >= 0 ? awayGap.ToString() : "?")}p " +
                                          $"({table.Count} satır kapsam)."
            };
        }

        // LiveState — YALNIZ in-play (Status=Live) maçlar için, MEVCUT MatchLiveStats'ten. Canlı satır
        // yoksa HasData=false. Zengin istatistik (şut/xG/topa sahip olma) ingest'te dolmadıysa
        // HasRichStats=false; skor/dakika yine gerçektir. Yeni API/job YOK.
        private LiveStateSignals BuildLiveState(Match? match)
        {
            if (match == null) return new LiveStateSignals { HasData = false };
            var isLive = string.Equals(match.Status, "Live", StringComparison.OrdinalIgnoreCase);
            if (!isLive) return new LiveStateSignals { HasData = false, IsLive = false };

            var s = _liveStatsRepo.GetByMatchId(match.Id);
            if (s == null) return new LiveStateSignals { HasData = false, IsLive = true };

            var rich = s.PossessionHome > 0 || s.PossessionAway > 0 || s.ShotsHome > 0 || s.ShotsAway > 0
                       || s.DangerousAttacksHome > 0 || s.DangerousAttacksAway > 0
                       || s.XgHome.HasValue || s.XgAway.HasValue;

            return new LiveStateSignals
            {
                HasData              = true,
                IsLive               = true,
                Minute               = s.Minute ?? 0,
                Phase                = s.Phase ?? "",
                HomeScore            = s.HomeScore,
                AwayScore            = s.AwayScore,
                HasRichStats         = rich,
                PossessionHome       = s.PossessionHome,
                PossessionAway       = s.PossessionAway,
                ShotsHome            = s.ShotsHome,
                ShotsAway            = s.ShotsAway,
                ShotsOnTargetHome    = s.ShotsOnTargetHome,
                ShotsOnTargetAway    = s.ShotsOnTargetAway,
                CornersHome          = s.CornersHome,
                CornersAway          = s.CornersAway,
                DangerousAttacksHome = s.DangerousAttacksHome,
                DangerousAttacksAway = s.DangerousAttacksAway,
                YellowHome           = s.YellowHome,
                YellowAway           = s.YellowAway,
                RedHome              = s.RedHome,
                RedAway              = s.RedAway,
                XgHome               = s.XgHome ?? 0,
                XgAway               = s.XgAway ?? 0
            };
        }

        // ── Bağlam yardımcıları (generic text parse — takım/lig HARDCODE'u YOK) ──
        private static string ClassifyStage(string? stageName)
        {
            var s = (stageName ?? "").ToLowerInvariant();
            if (s.Length == 0) return "Unknown";
            if (s.Contains("semi")) return "SemiFinal";
            if (s.Contains("quarter")) return "QuarterFinal";
            if (s.Contains("third")) return "ThirdPlace";
            if (s.Contains("final")) return "Final";
            if (s.Contains("16")) return "RoundOf16";
            if (s.Contains("group")) return "Group";
            if (s.Contains("playoff") || s.Contains("play-off")) return "Playoff";
            if (s.Contains("qualif")) return "Qualification";
            if (s.Contains("regular season") || s.Contains("league") || s.Contains("matchday")) return "RegularSeason";
            return "Unknown";
        }

        private static int ParseTrailingInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            int i = s.Length - 1, end = -1, start = -1;
            while (i >= 0 && !char.IsDigit(s[i])) i--;
            end = i;
            while (i >= 0 && char.IsDigit(s[i])) { start = i; i--; }
            if (start < 0 || end < 0) return 0;
            return int.TryParse(s.Substring(start, end - start + 1), out var n) ? n : 0;
        }

        private static (string importance, bool elimination) DeriveImportance(string type, string stage)
        {
            if (type.Equals("Knockout", StringComparison.OrdinalIgnoreCase))
            {
                if (stage == "Final") return ("Elimination", true);
                if (stage == "SemiFinal") return ("Critical", true);
                return ("High", true);
            }
            if (type.Equals("Cup", StringComparison.OrdinalIgnoreCase))
            {
                if (stage is "Final" or "SemiFinal") return ("Critical", false);
                return ("High", false);
            }
            return ("Normal", false); // League — sezon-sonu kritikliği Motivation'da standings/season ile
        }

        // FAZ 2 — güç/tempo sinyalleri yalnız GERÇEK gol geçmişinden türetilir (uydurma yok).
        // Phase 4 — standings (gerçek sezon GF/GA) VARSA baseline onunla iyileştirilir; motor
        // aynı alanı (LeagueGoalBaseline) okumaya devam eder → motor kodu DEĞİŞMEZ.
        private static MatchStrengthSignals BuildStrength(
            TeamComparisonDto? home, TeamComparisonDto? away, StandingsAiSignals standings,
            TeamStatsAiSignals teamStats)
        {
            var hFor = home?.AvgGoalsFor ?? 0; var hAg = home?.AvgGoalsAgainst ?? 0;
            var aFor = away?.AvgGoalsFor ?? 0; var aAg = away?.AvgGoalsAgainst ?? 0;

            var samples = new[] { hFor, hAg, aFor, aAg };
            var nonZero = 0; var sum = 0.0;
            foreach (var v in samples) { if (v > 0) { nonZero++; sum += v; } }
            var baseline = nonZero > 0 ? sum / nonZero : 0.0;

            // Standings sezon verisi daha güvenilir (tüm sezon, sadece son 10 değil) → varsa onu kullan.
            if (standings.HasData && standings.Home.Played > 0 && standings.Away.Played > 0)
            {
                var homeGpg = (standings.Home.GoalsFor + standings.Home.GoalsAgainst) / (double)standings.Home.Played;
                var awayGpg = (standings.Away.GoalsFor + standings.Away.GoalsAgainst) / (double)standings.Away.Played;
                var seasonBaseline = (homeGpg + awayGpg) / 4.0; // takım-başı-maç-başı beklenen gol
                if (seasonBaseline > 0) baseline = seasonBaseline;
            }

            // Phase 6 — EN GÜVENİLİR: /teams/statistics sezon ortalamaları VENUE-ÖZEL (ev takımının
            // EV ortalaması, deplasman takımının DEPLASMAN ortalaması) → bu maça daha uygun beklenti.
            // Motor aynı alanları (LeagueGoalBaseline + Attack/Defence index) okur; imza DEĞİŞMEZ.
            if (teamStats.HasData && teamStats.Home.Played > 0 && teamStats.Away.Played > 0)
            {
                // Venue-özel değer varsa onu, yoksa toplam ortalamayı kullan (erken sezon koruması).
                static double Pick(double venue, double total) => venue > 0 ? venue : total;

                var hForS = Pick(teamStats.Home.GoalsForAvgHome,     teamStats.Home.GoalsForAvgTotal);
                var hAgS  = Pick(teamStats.Home.GoalsAgainstAvgHome, teamStats.Home.GoalsAgainstAvgTotal);
                var aForS = Pick(teamStats.Away.GoalsForAvgAway,     teamStats.Away.GoalsForAvgTotal);
                var aAgS  = Pick(teamStats.Away.GoalsAgainstAvgAway, teamStats.Away.GoalsAgainstAvgTotal);

                var seasonSamples = new[] { hForS, hAgS, aForS, aAgS };
                var sNonZero = 0; var sSum = 0.0;
                foreach (var v in seasonSamples) { if (v > 0) { sNonZero++; sSum += v; } }
                if (sNonZero > 0)
                {
                    baseline = sSum / sNonZero;
                    hFor = hForS; hAg = hAgS; aFor = aForS; aAg = aAgS;
                }
            }

            return new MatchStrengthSignals
            {
                LeagueGoalBaseline = baseline,
                HomeAttackIndex    = hFor,
                AwayAttackIndex    = aFor,
                HomeDefenceIndex   = hAg,
                AwayDefenceIndex   = aAg
            };
        }

        // Veri tamlığı: iki takımın da gerçek gol geçmişi varsa 1.0; biri boşsa 0.5; ikisi boşsa 0.
        private static double ComputeDataQuality(TeamComparisonDto? home, TeamComparisonDto? away)
        {
            var q = 0.0;
            if (HasGoalData(home)) q += 0.5;
            if (HasGoalData(away)) q += 0.5;
            return q;
        }

        private static bool HasGoalData(TeamComparisonDto? t)
            => t != null && (t.AvgGoalsFor > 0 || t.AvgGoalsAgainst > 0 || t.GoalScoringRate > 0);

        private static TeamAiSignals MapTeam(TeamComparisonDto? t) => new()
        {
            AvgGoalsFor      = t?.AvgGoalsFor      ?? 0,
            AvgGoalsAgainst  = t?.AvgGoalsAgainst  ?? 0,
            GoalScoringRate  = t?.GoalScoringRate  ?? 0,
            CleanSheetRate   = t?.CleanSheetRate   ?? 0,
            FormScore        = t?.FormScore        ?? 0,
            LeagueRank       = t?.LeagueRank       ?? 0,
            HomeAwayAvgGoals = t?.HomeAwayAvgGoals ?? 0
        };
    }
}
