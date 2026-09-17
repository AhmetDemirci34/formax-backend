using System.Text.Json;
using Formax.Application.DTOs.Recommendations;
using Formax.Application.Interfaces;
using Formax.Application.Interfaces.Repositories;
using Formax.Application.UseCases.Home;
using Formax.Application.DTOs.Home;
using Formax.Application.AI.Context;
using Formax.Application.DTOs.Matches;
using Formax.Application.Services.Odds;
using Formax.Application.Services.Radar.Feed;
using Formax.Application.Services.Radar.Intelligence.Match;
using Formax.Application.Services.Radar.Intelligence.Scenarios;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

public sealed class GetRecommendationFeedUseCase
{
    private const double TEAM_FOLLOW_BOOST = 0.15;

    // ── Phase 7: cross-user global trend boost ──────────────────────────────
    private const double GLOBAL_BOOST_CAP        = 0.20;  // max contribution (pre-sigmoid)
    private const double GLOBAL_K                = 0.40;  // slope on (globalScore - 0.5)
    private const double HIGH_INTEREST_THRESHOLD = 0.10;  // personal-interest priority gate
    private const int    MIN_DISTINCT_USERS      = 3;     // single-user is not a "global trend"

    // Distinct-user guard intentionally DISABLED in dev (single seed user only).
    // Flip to true once a real multi-user base exists (beta) to enforce the gate.
    private const bool   ENABLE_DISTINCT_USER_GUARD = false;

    private readonly GetHomeRadarUseCase _radarUseCase;
    private readonly IRecommendationEngine _engine;
    private readonly IUserRepository _userRepository;
    private readonly IUserActionRepository _actionRepository;
    private readonly IUserPreferenceRepository _prefRepo;
    private readonly IMatchBanditRepository _banditRepo;
    private readonly IMatchReadRepository _matchRepo;
    private readonly IUserTeamFollowRepository _teamFollowRepo;
    private readonly IFeedInsightQueryService _feedInsightQuery;
    private readonly IRadarFeedAdapter _radarFeedAdapter;
    private readonly IRadarRankingService _radarRanking;
    private readonly IMatchIntelligenceRepository _intelRepo;
    private readonly IMatchLiveStatsRepository _liveStatsRepo;
    private readonly ITeamReadRepository _teamReadRepo;
    private readonly IMatchAiContextBuilder _aiContextBuilder;
    private readonly MarketProbabilityEngine _decisionEngine;
    private readonly IMatchOddsRepository _oddsRepo;
    /// <summary>AI Olası Sonuçlar — arka planda üretilmiş snapshot (Maç Detayı ile aynı kayıt).</summary>
    private readonly IMatchOutcomeSnapshotReader? _outcomeReader;
    private readonly IMemoryCache _cache;

    /// <summary>
    /// Anlatı deposu — YALNIZ OKUMA. Keşfet kartına, daha önce üretilmiş Match
    /// Intelligence anlatısı varsa taşınır; burada üretim BAŞLATILMAZ.
    /// </summary>
    private readonly Formax.Application.AI.Radar.IRadarNarrativeStore _narrativeStore;

    /// <summary>Decision paketi deterministiktir; kısa TTL yalnız tekrar-kurulumu önler.</summary>
    private static readonly TimeSpan DecisionCacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// DISCOVER AI KALİTE KAPISI — bir maçın ana Discover sunumuna girebilmesi için gereken
    /// en düşük <c>DecisionConfidence.Score</c>.
    ///
    /// Bu sayı KEYFİ DEĞİLDİR, <see cref="AI.Decision.Modules.ConfidenceEngine"/>'in kendi
    /// matematiğinden gelir:
    ///   coverage = Clamp(activeSignalCount / 8.0, 0.3, 1.0)
    /// activeSignalCount 1 ve 2 iken coverage ALT SINIRA (0.3) kelepçelenir — yani motor bu iki
    /// durumu birbirinden ayırt EDEMEZ, ikisi de "taban" muamelesi görür. Kelepçeden çıkılan ilk
    /// seviye activeSignalCount = 3'tür (0.375) ve gerçek ölçümde bu seviyenin karşılığı 12'dir.
    /// 217 maçlık gerçek ölçümde skorlar {0, 5..9, 12} kümesinde toplandı; 11 ile 13 arasında
    /// hiç değer yok — 12 ölçülmüş bir kırılma noktasıdır.
    ///
    /// 12 "YÜKSEK GÜVEN" DEĞİLDİR (motorun kendi sınıflandırmasında hâlâ DÜŞÜK: eşikler 50/68).
    /// Yalnızca AI sinyal kapsamının anlamlı ölçülmeye başladığı minimum teknik kalite kapısıdır.
    /// Confidence formülüne, aralığına veya seviye etiketlerine DOKUNULMAZ.
    /// </summary>
    private const int MinDiscoverConfidence = 12;

    /// <summary>
    /// Aday havuzu boyu (en yakın kickoff'tan itibaren). AI kalite kapısı eklenince 100'lük
    /// pencere yetersiz kaldı: gerçek ölçümde kapıyı geçen maçlar (aktif sinyali 3+ olanlar)
    /// bu pencerenin DIŞINDA kalıyor ve Discover gereksiz yere boşalıyordu. Havuz genişletmek
    /// sıralamayı DEĞİŞTİRMEZ — yalnız aynı sıralamanın değerlendirdiği aday sayısını artırır.
    /// Decision paketleri cache'li olduğu için maliyet yalnız ilk isteğe düşer.
    /// </summary>
    private const int CandidatePoolSize = 250;

    /// <summary>
    /// Zaman cezasının TABANI: en uzak maç bile keşif sinyalinin bu oranını korur.
    /// 1.0 = ceza yok. Eleme değil, ağırlıklandırma — hiçbir maç listeden çıkmaz.
    /// </summary>
    private const double TimeProximityFloor = 0.55;

    /// <summary>
    /// DISCOVER ZAMAN YAKINLIĞI (0–100) — Hero/Feed sıralamasının zaman terimi.
    ///
    /// Neden Discover'a özel bir merdiven: mevcut
    /// <see cref="GetHomeLiveSignalsUseCase"/> hesabı 48 saatten sonra 20'de DÜZLEŞİR;
    /// +3 gün ile +19 gün aynı puanı alır. Discover'ın sorusu "şimdi/çok yakında
    /// keşfetmeye değer ne var?" olduğu için ayrım tam orada gerekiyor.
    ///
    /// Aday havuzu kapsam allow-list'i küçük olduğundan 250 maç ≈ 19 güne yayılıyor
    /// (ölçüldü: 17 Ağu → 5 Eyl). Bu merdiven hiçbir maçı ELEMEZ — uzak maç yalnız
    /// sıralamada geriler; "Maçlar" ekranı ve DB etkilenmez.
    /// </summary>
    private static int ComputeDiscoverTimeProximity(DateTime kickoffUtc, DateTime utcNow)
    {
        var hours = (kickoffUtc - utcNow).TotalHours;
        if (hours <= 0)  return 100;  // başlamak üzere
        if (hours <= 24) return 95;   // bugün / 24 saat içinde
        if (hours <= 48) return 80;   // yarın
        if (hours <= 72) return 60;   // +2 gün
        if (hours <= 96) return 40;   // +3 gün
        if (hours <= 120) return 20;  // +4 gün — düşük ama mümkün
        if (hours <= 168) return 8;   // +5..+7 gün — ciddi dezavantaj
        return 0;                     // +7 gün ötesi — normal şartlarda Hero dışı
    }

    private static readonly JsonSerializerOptions _signalJsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public GetRecommendationFeedUseCase(
        GetHomeRadarUseCase radarUseCase,
        IRecommendationEngine engine,
        IUserRepository userRepository,
        IUserActionRepository actionRepository,
        IUserPreferenceRepository prefRepo,
        IMatchBanditRepository banditRepo,
        IMatchReadRepository matchRepo,
        IUserTeamFollowRepository teamFollowRepo,
        IFeedInsightQueryService feedInsightQuery,
        IRadarFeedAdapter radarFeedAdapter,
        IRadarRankingService radarRanking,
        IMatchIntelligenceRepository intelRepo,
        IMatchLiveStatsRepository liveStatsRepo,
        ITeamReadRepository teamReadRepo,
        IMatchAiContextBuilder aiContextBuilder,
        MarketProbabilityEngine decisionEngine,
        IMatchOddsRepository oddsRepo,
        IMemoryCache cache,
        Formax.Application.AI.Radar.IRadarNarrativeStore narrativeStore,
        IMatchOutcomeSnapshotReader? outcomeReader = null)
    {
        _narrativeStore = narrativeStore;
        _outcomeReader = outcomeReader;
        _teamReadRepo = teamReadRepo;
        _aiContextBuilder = aiContextBuilder;
        _decisionEngine = decisionEngine;
        _oddsRepo = oddsRepo;
        _cache = cache;
        _radarUseCase = radarUseCase;
        _engine = engine;
        _userRepository = userRepository;
        _actionRepository = actionRepository;
        _prefRepo = prefRepo;
        _banditRepo = banditRepo;
        _matchRepo = matchRepo;
        _teamFollowRepo = teamFollowRepo;
        _feedInsightQuery = feedInsightQuery;
        _radarFeedAdapter = radarFeedAdapter;
        _radarRanking = radarRanking;
        _intelRepo = intelRepo;
        _liveStatsRepo = liveStatsRepo;
    }

    /// <param name="maxHorizonDays">
    /// HERO/SWIPE UFKU (opsiyonel). Verilirse aday havuzu bugünden itibaren bu kadar takvim
    /// günüyle sınırlanır (bugün dahil, gün sonuna kadar). null = sınır yok (mevcut davranış).
    ///
    /// Neden parametre: Discover Hero/swipe kuyruğu ile "Sana Özel", "Günün AI Kombini",
    /// /tumu ve Trending AYNI ucu okuyor. Ufku global uygulamak Sana Özel'in geniş tarih
    /// evrenini de daraltırdı. Yalnız Hero yüzeyi bu parametreyi gönderir; diğer yüzeyler
    /// parametresiz çağırır ve HİÇ ETKİLENMEZ.
    /// </param>
    public async Task<List<RecommendationCardDto>> Execute(
        int userId, int page = 1, int pageSize = 10, int? maxHorizonDays = null)
    {
        // ── DISCOVER FILTER (MVP KİLİTLİ KARAR) ───────────────────────────────
        // Discover ekranının amacı canlı skor göstermek DEĞİL, kullanıcının izleyeceği
        // maçları KEŞFETMESİDİR. Bu nedenle Discover YALNIZ henüz başlamamış (NotStarted)
        // maçları döndürür. Live/Halftime/ExtraTime/Penalties/Finished/AfterPenalties/
        // Cancelled/Postponed/Abandoned/Suspended durumlarının HEPSİ elenir — hepsi coarse
        // Status "Live"/"Finished"/"Postponed"/"Cancelled"'a düştüğü için allow-list dışında
        // kalır. Tek yetkili filtre BURASIDIR; frontend filtre uygulamaz, durum ÜRETMEZ.
        // (allow-list = güvenli: gelecekte yeni bir "oynanmış" durum sızamaz.)
        var discoverableStatuses = new[] { "NotStarted", MatchStatuses.PreMatch };

        // HERO UFKU — aday üretiminde uygulanır (sıralamada değil): ufuk dışı maç Hero/swipe
        // kuyruğuna HİÇ GİRMEZ. Takvim günü mantığı: bugün + N gün, günün sonuna kadar dahil.
        // (Zaman yakınlığı cezası ayrı mekanizmadır ve korunur; o sıralar, bu havuzu belirler.)
        // maxHorizonDays null ise sınır yoktur → Sana Özel/Kombin/Trending aynı evreni görür.
        var horizonCutoff = maxHorizonDays.HasValue
            ? DateTime.UtcNow.Date.AddDays(maxHorizonDays.Value + 1)
            : (DateTime?)null;

        var matches = _matchRepo.Query()
            .Where(x => discoverableStatuses.Contains(x.Status) && x.MatchDate >= DateTime.UtcNow)
            .Where(x => horizonCutoff == null || x.MatchDate < horizonCutoff)
            .OrderBy(x => x.MatchDate)   // keşif önceliği: en yakın kickoff önce
            .Take(CandidatePoolSize)
            .ToList();

        var matchById = matches.ToDictionary(m => m.Id);

        // #1 Canlı skor: canlı maçlar için MatchLiveStats'tan batch oku (feed'de skor gösterilsin).
        var liveStatsByMatch = _liveStatsRepo
            .GetByMatchIds(matches.Select(m => m.Id).ToHashSet())
            .ToDictionary(s => s.MatchId);

        var followedTeamIds = (await _teamFollowRepo.GetActiveByUserAsync(userId))
            .Select(x => x.TeamId)
            .ToHashSet();

        // ── Phase 7: distinct-user guard. A single user's repeated actions
        // must not count as a "global trend" (audit rule 4).
        // DEV: guard disabled → layer active for testing with the single seed user.
        // PROD: flip ENABLE_DISTINCT_USER_GUARD = true. When enabled, a real
        // distinct-user count source is required (e.g. an IUserActionRepository
        // CountDistinctUsersAsync) — until that exists the guard stays closed,
        // keeping the layer off rather than trusting a single-user signal.
        var globalLayerEnabled = !ENABLE_DISTINCT_USER_GUARD;

        // 🔥 ENTITY → DTO MAP
        var radarMatches = matches.Select(m => new HomeRadarMatchDto
        {
            MatchId = m.Id,

            Teams = new HomeTeamsDto
            {
                Home = m.HomeTeam.Name,
                Away = m.AwayTeam.Name
            },

            League = m.League,
            LeagueName = m.League,

            // bunlar sende yoksa 0 ver (engine zaten normalize eder)
            RadarScore = 0,
            TeamInterestScore = 0,
            LeagueInterestScore = 0,
            ContentInterestScore = 0,
            BehaviorMomentumScore = 0,
            MatchHeatScore = 0,
            LeagueBaselineScore = 0,
            // ZAMAN YAKINLIĞI — eskiden sabit 0'dı, yani ranking'in zaman terimi ÖLÜYDÜ:
            // RecommendationEngine `freshness = TimeProximityScore / 100.0` okuyor ve
            // bugünkü maç ile 19 gün sonraki maç zaman açısından eşit yarışıyordu.
            // Artık gerçek kickoff uzaklığından besleniyor (yeni skor TÜRÜ değil, mevcut
            // alanın doğru değeri). Hiçbir maç ELENMEZ — yalnız sırada aşağı iner.
            TimeProximityScore = ComputeDiscoverTimeProximity(m.MatchDate, DateTime.UtcNow),

            Freshness = "NEW",
            Explainability = "",
            Reasons = new List<string>(),

            PlayRate = 0,
            TrendDelta = 0,
            OddsMovementScore = 0,
            ViewDurationMs = 0,
            UserSwipeScore = 0,
            OpenedDetail = false,
            Followed = false
        }).ToList();

        // 🔥 ARTIK DOĞRU TİP
        var result = await _engine.BuildRecommendationFeed(userId, radarMatches);

        var actions = await _actionRepository.GetByUserIdAsync(userId);
        var weights = await _prefRepo.GetOrCreate(userId);

        // N+1 FIX (perf): 100 aday maçın bandit istatistiğini TEK sorguda önceden çek (döngü içinde
        // maç-başına sıralı await yerine). Değerler birebir aynı → RANKING/skor DEĞİŞMEZ.
        var banditByMatch = await _banditRepo.GetOrCreateMany(result.Select(r => r.MatchId).ToList());

        foreach (var x in result)
        {
            // RANKING davranışsal bileşeni: floor'suz RawUserTrendScore (gerçek UserTrendService
            // çıktısı) okunur — böylece UserInterestScores etkisi sıralamada floor'la maskelenmez.
            // UI hâlâ x.UserTrendScore (display, floor'lu) gösterir. Weight/formül DEĞİŞMEDİ (0.5).
            var baseScore =
                (0.5 * x.RawUserTrendScore) +
                (0.3 * x.MarketTrendScore) +
                (0.2 * x.MomentumScore);

            double decay = 1.0;
            if (actions.Count > 0)
            {
                var last = actions.First().CreatedAt;
                var days = (DateTime.UtcNow - last).TotalDays;
                decay = Math.Exp(-0.1 * days);
            }

            double userBoost = 0;

            var sameMatch = actions.FirstOrDefault(a => a.MatchId == x.MatchId);

            if (sameMatch != null)
            {
                if (sameMatch.ActionType == 1) userBoost += weights.LikeWeight;
                if (sameMatch.ActionType == -1) userBoost += weights.SkipWeight;
            }

            var teamLikes = actions.Count(a =>
                a.ActionType == 1 &&
                (a.Team == x.TeamA || a.Team == x.TeamB));

            var teamSkips = actions.Count(a =>
                a.ActionType == -1 &&
                (a.Team == x.TeamA || a.Team == x.TeamB));

            userBoost += Math.Min(0.3, teamLikes * weights.TeamWeight);
            userBoost -= Math.Min(0.3, teamSkips * weights.TeamWeight);

            userBoost = Math.Max(-0.4, Math.Min(0.4, userBoost));
            userBoost *= decay;

            var stats = banditByMatch[x.MatchId];

            double ucb = 0;

            if (stats.Impressions > 0)
            {
                var ctr = (double)stats.Likes / stats.Impressions;
                var exploration =
                    Math.Sqrt(2 * Math.Log(actions.Count + 1) / stats.Impressions);

                ucb = Math.Min(1.0, ctr + exploration);
            }

            var rawScore = baseScore + userBoost + (ucb * 0.03);

            matchById.TryGetValue(x.MatchId, out var matchEntity);

            // League rank — importance signal only, does not affect scoring.
            x.HomeRank = matchEntity?.HomeTeam?.LeagueRank;
            x.AwayRank = matchEntity?.AwayTeam?.LeagueRank;

            var followsTeam =
                followedTeamIds.Count > 0
                && matchEntity != null
                && (followedTeamIds.Contains(matchEntity.HomeTeamId)
                    || followedTeamIds.Contains(matchEntity.AwayTeamId));

            if (followsTeam)
                rawScore += TEAM_FOLLOW_BOOST;

            // ── Phase 7: cross-user global trend boost (capped, personal-priority) ──
            if (globalLayerEnabled)
            {
                var globalBoost = Math.Clamp(
                    (x.GlobalTrendScore - 0.5) * GLOBAL_K, 0.0, GLOBAL_BOOST_CAP);

                // Personal interest always wins: dampen global when the user
                // follows a team or already shows strong interest in this card.
                if (followsTeam || userBoost >= HIGH_INTEREST_THRESHOLD)
                    globalBoost *= 0.3;

                rawScore += globalBoost;
            }

            x.RecommendationScore = rawScore / (1 + rawScore);

            x.ConfidenceScore = Math.Min(1.0,
                (x.RecommendationScore * 0.7) +
                (x.GlobalTrendScore * 0.3)
            );

            x.ConfidenceLabel = x.ConfidenceScore switch
            {
                > 0.75 => "HIGH",
                > 0.50 => "MEDIUM",
                _ => "LOW"
            };

            x.AiSummary = "";

            x.PersonalReason =
                $"Boost:{Math.Round(userBoost, 2)} UCB:{Math.Round(ucb, 2)}";

            var matchIsHot = x.DirectionScore > 0;

            x.StoryHeadline = BuildStoryHeadline(
                x.TeamA, x.TeamB,
                x.HomeRank, x.AwayRank,
                followsTeam, matchIsHot,
                matchEntity?.League);

            x.StoryBody = BuildStoryBody(
                x.StoryHeadline, x.TeamA, x.TeamB,
                matchEntity?.HomeTeam?.AvgGoalsFor,
                matchEntity?.AwayTeam?.AvgGoalsFor,
                matchEntity?.HomeTeam?.IsStableTeam,
                matchIsHot);

            x.Tags = BuildMatchTags(
                x.TeamA, x.TeamB,
                x.HomeRank, x.AwayRank,
                matchEntity?.HomeTeam?.AvgGoalsFor,
                matchEntity?.AwayTeam?.AvgGoalsFor,
                matchEntity?.HomeTeam?.IsStableTeam);

            // Deterministic explanation — does NOT affect scoring, only labels
            // the dominant factor behind this card's ranking.
            x.RecommendationReason = ResolveRecommendationReason(
                followsTeam,
                userBoost,
                x.UserTrendScore,
                x.MarketTrendScore,
                x.GlobalTrendScore,
                x.MomentumScore);

            x.Score = x.RecommendationScore * 100;

            // NOT: AiTrustScore burada DOLDURULMAZ. ConfidenceScore bir ÖNERİ/İLGİ skorudur
            // (RecommendationScore + GlobalTrend), AI'ın maça duyduğu güven DEĞİLDİR. İkisini
            // aynı etikette göstermek "maç kartı 37 / kombin %69" tutarsızlığını doğuruyordu.
            // Gerçek AI güveni aşağıda, sayfalanmış kartlar için Decision paketinden gelir.
        }

        // ── R.13.4/R.13.5: Radar insights — overlay content + support ranking ─
        // RecommendationScore / UCB / Bandit are NOT modified. Radar only contributes a
        // low, capped (≤15%) support weight to the SORT KEY and fills content fields.
        // PERF (MVP freeze): eskiden GetFeedAsync(int.MaxValue) çağrılıyordu — bu, 120 günlük
        // pencerede ~13.6k maçın TAMAMINI her istekte yeniden kuruyordu. Feed zaten yalnız
        // yukarıdaki aday maçları puanlıyor; yalnız onların insight'ı okunur. Aynı kartlar için
        // aynı insight → RADAR OVERLAY / SIRALAMA DEĞİŞMEZ.
        var candidateMatchIds = result.Select(r => r.MatchId).Distinct().ToList();
        var insights = await _feedInsightQuery.GetFeedByMatchIdsAsync(candidateMatchIds);
        var radarByMatch = insights
            .GroupBy(i => i.MatchId)
            .ToDictionary(g => g.Key, g => g.First());

        // SIRALAMA — mevcut Radar destekli sıra matematiği DEĞİŞMEDİ.
        var ranked = result
            .Select(card =>
            {
                var radarScore = radarByMatch.TryGetValue(card.MatchId, out var ins)
                    ? _radarRanking.NormalizeRadarScore(ins)
                    : 0.0;
                card.RadarScore = radarScore;   // surface the already-computed Radar score
                var sortKey = _radarRanking.ComputeFinalScore(card.RecommendationScore, radarScore);

                // ── ZAMAN YAKINLIĞI CEZASI (Discover ürün kuralı) ──────────────────
                // Discover Hero'nun sorusu "şimdi/çok yakında keşfetmeye değer ne var?"dır;
                // haftalık takvim "Maçlar" ekranının işidir. RecommendationEngine'in kendi
                // zaman terimi (freshness * 0.03) bu ayrımı yapamıyor: ÖLÇÜLDÜ — en fazla
                // 0,03 oynatabiliyor, oysa 1. ve 2. kart arasındaki fark 0,0415'ti; +12 günlük
                // maç Hero'da 1. sırada kalıyordu.
                //
                // Bu bir FİLTRE DEĞİL, çarpansal ceza: taban 0,55 → en uzak maç bile keşif
                // sinyalinin %55'ini korur, hiçbir maç listeden ÇIKMAZ (güçlü uzak maç listede
                // aşağıda kalır, "Maçlar" ekranı ve DB hiç etkilenmez).
                // Taban değeri gerçek feed skorlarıyla seçildi: 0,70 yetersiz (+12g maç 2. sırada
                // kalıyordu), 0,40 gereksiz sert; 0,55 ile ilk 5 kart +3 gün içine giriyor.
                var proximity = matchById.TryGetValue(card.MatchId, out var mEntity)
                    ? ComputeDiscoverTimeProximity(mEntity.MatchDate, DateTime.UtcNow)
                    : 0;
                sortKey *= TimeProximityFloor + (1 - TimeProximityFloor) * (proximity / 100.0);

                // Discovery Engine sıralama skoru (Hero/Feed sıralamasının tek kaynağı, 0-100).
                card.DiscoveryScore = Math.Round(sortKey * 100, 2);
                return (card, sortKey);
            })
            // P0-2: canlı maçlar (gerçek skor/dakikalı) feed'in BAŞINDA görünsün — aksi halde
            // RecommendationScore düşükse gömülüp "izlenebilir canlı maç" kullanıcıya ulaşmıyor.
            .OrderByDescending(t => matchById.TryGetValue(t.card.MatchId, out var mm) && mm.Status == MatchStatuses.Live)
            .ThenByDescending(t => t.sortKey)
            .ThenBy(t => t.card.MatchId)   // deterministic tie-break → stable pagination
            .Select(t => t.card)
            .ToList();

        // ── AI KALİTE KAPISI — Discover YALNIZ AI'ın anlamlı ölçebildiği maçları sunar ──
        // SIRALAMADAN SONRA, SAYFALAMADAN ÖNCE uygulanır: sıralama matematiğine dokunulmaz,
        // yalnız AI açısından yetersiz aday elenir. Bir maç sıralamada ne kadar üstte olursa
        // olsun, kapıyı geçemiyorsa Discover'a GİRMEZ. (Frontend'de "confidence düşükse gizle"
        // ile kapatmak yetmez — kart yine kuyruğa girer ve swipe sırasında boş AI ekranı olur.)
        var packagesByMatch = new Dictionary<int, Formax.Application.AI.Decision.AiDecisionPackage>();
        var eligible = new List<RecommendationCardDto>();
        foreach (var card in ranked)
        {
            var package = TryBuildDecisionPackage(card, matchById);
            if (package == null) continue;                              // Decision verisi yok
            if (package.Probabilities.Count == 0) continue;             // olasılık üretilmemiş
            if (package.Confidence.Score < MinDiscoverConfidence) continue;

            packagesByMatch[card.MatchId] = package;
            eligible.Add(card);
        }

        // Yeterli AI verili maç yoksa feed KISA döner — sahte maç/AI üretilmez.
        var finalList = eligible
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // R.13.4 — overlay Radar content (StoryHeadline/StoryBody/AiSummary/InsightLabel)
        // onto cards that have an insight. Runs after sort/paginate; scores untouched.
        // N+1 FIX (perf): sayfadaki kartların intelligence snapshot'ı TEK sorguda çekilir
        // (döngü içinde kart-başına GetByMatchIdAsync yerine). Değerler birebir aynı.
        var intelByMatch = await _intelRepo.GetByMatchIdsAsync(
            finalList.Select(c => c.MatchId).Distinct().ToList());

        foreach (var card in finalList)
        {
            if (radarByMatch.TryGetValue(card.MatchId, out var insight))
                _radarFeedAdapter.Apply(card, insight);

            // Surface the already-computed Match Importance + Key Signals from the
            // intelligence snapshot. No new computation — read-and-map only.
            if (intelByMatch.TryGetValue(card.MatchId, out var snap) && snap != null)
            {
                card.MatchImportance = snap.ImportanceScore;
                card.KeySignals = MapKeySignals(snap.SignalsJson);
            }

            // Surface the existing Team.LogoUrl through the existing TeamDto contract.
            // The engine only set Name; the match entity already carries the logo.
            if (matchById.TryGetValue(card.MatchId, out var matchEntity))
            {
                // Mevcut Match.MatchDate'i feed'e taşı (yeni veri üretilmez). MatchDate DB'de UTC
                // saklanır (provider .ToUniversalTime()); ancak EF okuduğunda Kind=Unspecified olur
                // ve System.Text.Json 'Z' EKLEMEZ → frontend değeri YEREL sanıp saati 3 saat erken
                // gösterir (maç "başlamış" görünür). UTC Kind işaretleyerek ISO'ya 'Z' eklenir →
                // frontend doğru yerel kickoff'u gösterir. Saat/karşılaştırma değişmez, yalnız Kind.
                card.MatchDate = DateTime.SpecifyKind(matchEntity.MatchDate, DateTimeKind.Utc);

                // Discovery durum alanları (gerçek Match entity'sinden — yeni hesaplama yok).
                card.Status = matchEntity.Status;
                card.IsLive = matchEntity.Status == MatchStatuses.Live;
                card.LiveMinute = card.IsLive ? ParseLiveMinute(matchEntity.MatchMinute) : null;

                // #1/P0-2 Canlı skor + dakika — yalnız canlı maçta, MatchLiveStats'tan (tek gerçek kaynak).
                // Dakika Match.MatchMinute'te DEĞİL (o boş), MatchLiveStats.Minute'te. Yoksa null (uydurulmaz).
                if (card.IsLive && liveStatsByMatch.TryGetValue(card.MatchId, out var ls))
                {
                    card.HomeScore = ls.HomeScore;
                    card.AwayScore = ls.AwayScore;
                    if (ls.Minute != null) card.LiveMinute = ls.Minute;
                }

                if (matchEntity.HomeTeam != null)
                {
                    card.HomeTeam.Id = matchEntity.HomeTeam.Id;
                    card.HomeTeam.LogoUrl = matchEntity.HomeTeam.LogoUrl;
                }
                if (matchEntity.AwayTeam != null)
                {
                    card.AwayTeam.Id = matchEntity.AwayTeam.Id;
                    card.AwayTeam.LogoUrl = matchEntity.AwayTeam.LogoUrl;
                }
            }
        }

        await ApplyDecisionSurfaceAsync(finalList, packagesByMatch);
        ApplyDiscoverNarrative(finalList);

        return finalList;
    }

    /// <summary>
    /// KEŞFET ANLATISI — hazır snapshot varsa karta taşınır, YOKSA HİÇBİR ŞEY YAPILMAZ.
    ///
    /// Neden: Keşfet kartı, teaser metnini göstermek için maç detayı ucunu çağırıyordu.
    /// O uç TEK istekte ÜÇ yüzeyi birden ürettiği için tek bir kartı görüntülemek üç LLM
    /// çağrısına mal oluyor, kullanıcı kartlar arasında gezindikçe bu tekrarlanıyordu.
    /// Anlatı artık feed yanıtıyla taşınır ve Keşfet maç detayı ucunu ÇAĞIRMAZ.
    ///
    /// ÜRETİM YOK: burada yalnız <see cref="AI.Radar.IRadarNarrativeStore"/> okunur —
    /// yani daha önce (kullanıcı o maçın detayını açtığında) üretilmiş anlatı. Depoda
    /// kayıt yoksa alanlar boş kalır ve kart AI yorumu bölümünü göstermez. Böylece aynı
    /// maç için ikinci bir üretim asla oluşmaz.
    /// </summary>
    private void ApplyDiscoverNarrative(List<RecommendationCardDto> cards)
    {
        foreach (var card in cards)
        {
            if (!_narrativeStore.TryGetLatestForMatch(
                    Formax.Application.AI.Radar.RadarSurface.Discover, card.MatchId, out var narrative))
                continue;

            card.RadarSummary = narrative.RadarSummary ?? "";
            card.RadarHighlights = narrative.Highlights ?? new List<string>();
        }
    }

    /// <summary>
    /// Bir kartın Decision paketini kurar. Deterministik olduğu için (aynı context → aynı paket)
    /// süreç-içi cache'lenir; Discover AI kapısı 100 adayın hepsini bu yolla değerlendirir ve
    /// aynı istek içinde ikinci kez kurulmaz. Paket kurulamıyorsa null döner — bu maçın AI
    /// verisi YOK demektir ve feed'e girmez (uydurma değer üretilmez).
    /// </summary>
    private Formax.Application.AI.Decision.AiDecisionPackage? TryBuildDecisionPackage(
        RecommendationCardDto card,
        Dictionary<int, Match> matchById)
    {
        if (!matchById.TryGetValue(card.MatchId, out var match)) return null;

        var cacheKey = $"decision:pkg:{card.MatchId}";
        if (_cache.TryGetValue(cacheKey, out Formax.Application.AI.Decision.AiDecisionPackage? cached)
            && cached != null)
            return cached;

        try
        {
            var homeName = _teamReadRepo.GetById(match.HomeTeamId)?.Name ?? card.TeamA;
            var awayName = _teamReadRepo.GetById(match.AwayTeamId)?.Name ?? card.TeamB;

            // FAZ 1 — TeamComparison/H2H/GücSkoru artık builder'ın kendi ürettiği GERÇEK veri
            // (mevcut Matches tablosundan; ek API çağrısı/job YOK). Boş DTO + sabit 50 kalktı.
            var ctx = _aiContextBuilder.Build(
                match.Id, match.HomeTeamId, match.AwayTeamId, homeName, awayName);

            var package = _decisionEngine.BuildDecisionPackage(ctx);
            _cache.Set(cacheKey, package, DecisionCacheTtl);
            return package;
        }
        catch
        {
            // Paket kurulamadı → AI verisi yok. Feed'in tamamı düşmez, yalnız bu kart elenir.
            return null;
        }
    }

    /// <summary>
    /// TEK AI KAYNAĞI — Keşfet kartlarına Decision paketinden gerçek AI yüzeyi yazar:
    /// AiTrustScore (gerçek güven endeksi), TopPrediction ve ilk 3 Prediction (+ gerçek oran).
    ///
    /// Neden burada: Maç Detay, AI Olası Sonuçlar ve Günün AI Kombini zaten aynı Decision
    /// paketini okuyor. Feed kartı farklı bir metrikten ("öneri skoru") beslendiğinde aynı maç
    /// iki ayrı sayı gösteriyordu. Artık üç yüzey de AYNI motordan gelir.
    ///
    /// Paketler AI kapısında zaten kurulmuştur; burada YENİDEN kurulmaz, taşınır.
    /// </summary>
    private async Task ApplyDecisionSurfaceAsync(
        List<RecommendationCardDto> cards,
        Dictionary<int, Formax.Application.AI.Decision.AiDecisionPackage> packagesByMatch)
    {
        if (cards.Count == 0) return;

        // Gerçek oranlar: sayfadaki tüm maçlar için TEK sorgu (N+1 yok).
        var oddsByMatch = (await _oddsRepo.GetByMatchIdsAsync(cards.Select(c => c.MatchId).ToList()))
            .GroupBy(o => o.MatchId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(o => o.MarketKey, StringComparer.Ordinal));

        var outcomes = _outcomeReader == null
            ? new Dictionary<int, Formax.Application.Services.Outcomes.OutcomeSnapshotDto>()
            : await _outcomeReader.GetCurrentForMatchesAsync(cards.Select(c => c.MatchId).ToList());

        foreach (var card in cards)
        {
            // AI BEKLENTİSİ + snapshot kimliği karar paketinden BAĞIMSIZ taşınır (Keşfet ve Detay aynı snapshot).
            outcomes.TryGetValue(card.MatchId, out var current);
            card.OutcomeSnapshotId = current?.SnapshotId;
            card.PredictionEligibility = current?.SnapshotId == null ? null : current.PredictionEligibility;
            card.AiExpectation = current is { Status: "Available" } && current.MainCards.Count > 0
                ? current.MainCards.FirstOrDefault(c => c.Family == Formax.Application.Services.Outcomes.OutcomeFamilies.Result)?.Probability
                : null;

            if (!packagesByMatch.TryGetValue(card.MatchId, out var package)) continue;

            // GERÇEK AI güveni — kombindeki/maç detayındaki ile aynı motor, aynı sayı.
            // AI kapısı sayesinde bu noktada Score > 0 garantidir.
            card.AiTrustScore = package.Confidence.Score;
            card.ConfidenceLabel = string.IsNullOrWhiteSpace(package.Confidence.Level)
                ? card.ConfidenceLabel
                : package.Confidence.Level;

            // AI OLASI SONUÇLAR (15.09.2026) — ham yüzdeye göre sıralama KALDIRILDI (çifte şans bileşik olasılık olduğu için
            // hep ilk sıraya çıkıyordu). Kartlar arka planda üretilen snapshot'ın üç farklı aileden ana kartlarıdır; Maç Detayı
            // aynı SnapshotId'yi okur. Snapshot yoksa tahmin gösterilmez (sayfa açılışında hesaplanmaz). Oran AI kartına eklenmez.
            if (!outcomes.TryGetValue(card.MatchId, out var snapshot) || snapshot.Status != "Available" || snapshot.MainCards.Count == 0)
            {
                card.TopPrediction = null;
                card.Predictions = new List<AiPredictionDto>();
                card.OutcomeSnapshotId = snapshot?.SnapshotId;
                continue;
            }
            card.OutcomeSnapshotId = snapshot.SnapshotId;
            var strongest = snapshot.MainCards.OrderByDescending(c => c.SelectionScore).First();
            card.TopPrediction = new TopPredictionDto { Market = strongest.Market, Probability = strongest.Probability, Odd = null };
            card.Predictions = snapshot.MainCards.Select(c => new AiPredictionDto
            {
                Market = c.Market,
                Probability = c.Probability,
                Confidence = c.SampleQuality,
                Family = c.Family,
                FamilyTitle = c.FamilyTitle,
                CurrentOdd = null,
                PreviousOdd = null,
                Movement = OddsMovement.None,
                UpdatedAt = snapshot.ComputedAtUtc ?? DateTime.UtcNow
            }).ToList();
        }
    }

    /// <summary>Oran hareket yönü — GERÇEK iki okuma arasında. Eksikse None.</summary>
    private static OddsMovement ResolveMovement(decimal? current, decimal? previous)
    {
        if (current == null || previous == null || current == previous) return OddsMovement.None;
        return current > previous ? OddsMovement.Up : OddsMovement.Down;
    }

    // Map the persisted MatchSignal[] JSON onto the frontend KeySignal contract.
    // Top 3 by weight; only real Label/Reason are carried (icon/value/tone stay null).
    private static List<KeySignalDto> MapKeySignals(string? signalsJson)
    {
        if (string.IsNullOrWhiteSpace(signalsJson)) return new();
        try
        {
            var signals = JsonSerializer.Deserialize<List<MatchSignal>>(signalsJson, _signalJsonOpts);
            if (signals == null) return new();

            return signals
                .OrderByDescending(s => s.Weight)
                .Take(3)
                .Select(s => new KeySignalDto { Title = s.Label, Caption = s.Reason })
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    // Canlı dakika: Match.MatchMinute (ör. "67", "67'", "45+2") → baştaki tam sayı.
    private static int? ParseLiveMinute(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.TrimStart().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var m) ? m : (int?)null;
    }

    // ── Deterministic explanation layer ──────────────────────────────────────
    // Maps the dominant ranking factor to a stable reason code.
    // Pure read of already-computed signals — no scoring change, no AI text.
    private static string ResolveRecommendationReason(
        bool followsTeam,
        double userBoost,
        double userTrend,
        double marketTrend,
        double globalTrend,
        double momentum)
    {
        if (followsTeam) return "FOLLOWED_TEAM";
        if (userBoost >= 0.10) return "HIGH_INTEREST";

        // Compare weighted contributions (same weights the score uses).
        var normMomentum = momentum > 1 ? momentum / 100.0 : momentum;

        var cUser     = 0.5 * userTrend;
        var cMarket   = 0.3 * marketTrend;
        var cGlobal   = 0.3 * globalTrend;
        var cTrending = 0.2 * normMomentum;

        var max = Math.Max(Math.Max(cUser, cMarket), Math.Max(cGlobal, cTrending));

        if (max <= 0) return "GLOBAL_SIGNAL";
        if (max == cUser) return "HIGH_INTEREST";
        if (max == cMarket) return "MARKET_SIGNAL";
        if (max == cTrending) return "TRENDING";
        return "GLOBAL_SIGNAL";
    }

    // ── Story Layer ───────────────────────────────────────────────────────────

    private static readonly HashSet<(string, string)> DerbyPairs = new()
    {
        ("galatasaray", "fenerbahçe"), ("fenerbahçe", "galatasaray"),
        ("galatasaray", "beşiktaş"),   ("beşiktaş",   "galatasaray"),
        ("fenerbahçe", "beşiktaş"),    ("beşiktaş",   "fenerbahçe"),
        ("galatasaray", "trabzonspor"),("trabzonspor", "galatasaray"),
        ("fenerbahçe", "trabzonspor"), ("trabzonspor", "fenerbahçe"),
    };

    private static bool IsDerby(string home, string away) =>
        DerbyPairs.Contains((home.Trim().ToLowerInvariant(), away.Trim().ToLowerInvariant()));

    private static string BuildStoryHeadline(
        string homeName, string awayName,
        int? homeRank, int? awayRank,
        bool followsTeam, bool isHot, string? league)
    {
        if (followsTeam)                                              return "⭐ Takip Ettiğin Takım";
        if (IsDerby(homeName, awayName))                             return "⚔️ Dev Derbi";
        if (homeRank != null && awayRank != null
            && homeRank + awayRank <= 3)                             return "🏆 Liderlik Yarışı";
        if (homeRank != null && awayRank != null
            && homeRank + awayRank <= 5)                             return "🔝 Zirve Maçı";
        if (isHot)                                                   return "🔥 Bu Hafta Çok Konuşuluyor";
        var leagueLabel = string.IsNullOrWhiteSpace(league) ? "Lig" : league;
        return $"🏟️ {leagueLabel} Maçı";
    }

    private static string BuildStoryBody(
        string headline, string homeName, string awayName,
        double? homeAvgGoals, double? awayAvgGoals,
        bool? homeIsStable, bool isHot)
    {
        // Bağlamsal başlıklar — futbol odaklı, kesinlik/bahis dili YOK.
        if (headline.StartsWith("⭐"))
            return $"Takip ettiğin {homeName}, {awayName} karşısında sahaya çıkıyor — formunu bu maçta yakından görebilirsin.";
        if (headline.StartsWith("⚔️"))
            return $"{homeName} - {awayName} derbisi kendi hikâyesini yazar; iki tarafın da geri adım atmadığı, temposu yüksek bir mücadele bekleniyor.";
        if (headline.StartsWith("🏆"))
            return $"Zirvenin iki ekibi karşı karşıya. {homeName} ile {awayName} arasındaki sonuç, yarışın yönünü değiştirebilir.";
        if (headline.StartsWith("🔝"))
            return "İki takım da üst sıralarda; puan tablosunda küçük farkların konuşulduğu kritik bir randevu.";

        // Veri odaklı (mevcut sinyaller) — gol üretimi / savunma / ev sahibi formu.
        var totalGoals = (homeAvgGoals ?? 0) + (awayAvgGoals ?? 0);
        if (homeAvgGoals is not null && awayAvgGoals is not null && totalGoals >= 3.0)
            return $"{homeName} ve {awayName} son dönemde gol üretimini yüksek tutuyor; bol pozisyonlu, tempolu bir maç profili öne çıkıyor.";
        if (homeAvgGoals is not null && awayAvgGoals is not null && totalGoals < 1.8)
            return $"{homeName} ve {awayName} savunma dengesine güveniyor; az gollü, satranç gibi ilerleyen sabırlı bir maç bekleniyor.";
        if (homeIsStable == true)
            return $"{homeName} evindeki istikrarlı çıkışını korumak istiyor; {awayName} bu düzeni bozmak için deplasmanda sürpriz peşinde.";
        if (isHot)
            return $"{homeName} - {awayName} karşılaşması, oyun temposu ve iki takımın gidişatıyla bu hafta öne çıkan maçlardan biri.";

        // Doğal varsayılan — kesinlik/bahis dili yok.
        return $"{homeName} sahasında {awayName} ile karşılaşıyor; iki taraf için de kritik bir 3 puanlık mücadele.";
    }

    private static List<string> BuildMatchTags(
        string homeName, string awayName,
        int? homeRank, int? awayRank,
        double? homeAvgGoals, double? awayAvgGoals,
        bool? homeIsStable)
    {
        var tags = new List<string>();

        if (homeRank != null && awayRank != null && homeRank + awayRank <= 3)
            tags.Add("🏆 Liderlik Yarışı");
        else if (homeRank != null && awayRank != null && homeRank + awayRank <= 5)
            tags.Add("🔝 Zirve Maçı");

        if (IsDerby(homeName, awayName))
            tags.Add("⚔️ Derbi");

        if (homeAvgGoals != null && awayAvgGoals != null)
        {
            var total = homeAvgGoals.Value + awayAvgGoals.Value;
            if (total >= 3.0)
                tags.Add("⚽ Yüksek Gol Beklentisi");
            else if (total < 1.8)
                tags.Add("🛡️ Düşük Skorlu Geçebilir");
        }

        if (homeIsStable == true)
            tags.Add("📈 Ev Sahibi Formda");

        return tags.Take(3).ToList();
    }
}