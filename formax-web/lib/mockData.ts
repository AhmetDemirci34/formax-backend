// ─────────────────────────────────────────────────────────────────────────────
// FORMAX Mock Data — mirrors real DTO shapes exactly
// Used only by /demo page — NOT imported by production code
// ─────────────────────────────────────────────────────────────────────────────

import type {
  RecommendationCardDto,
  MatchDetailDto,
  TeamSummaryDto,
  LastMatchDto,
  H2HDto,
  ComparisonDto,
  InsightDto,
  SapmaDto,
  AiDto,
  ProbabilityItemDto,
  MarketIntelligenceDto,
  RiskIntelligenceDto,
  TacticalMatchupDto,
  LineupSectionDto,
  PlayerStatusSectionDto,
  StandingSectionDto,
  LiveSectionDto,
  NabizSectionDto,
  UserProtectionDto,
} from "@/types/api";

// ── Shared teams ──────────────────────────────────────────────────────────────

const GS: TeamSummaryDto = { id: 1, name: "Galatasaray", shortName: "GS", rank: 1 };
const FB: TeamSummaryDto = { id: 2, name: "Fenerbahçe", shortName: "FB", rank: 2 };
const TS: TeamSummaryDto = { id: 3, name: "Trabzonspor", shortName: "TS", rank: 5 };
const BSK: TeamSummaryDto = { id: 4, name: "Başakşehir FK", shortName: "BAŞ", rank: 8 };
const BJK: TeamSummaryDto = { id: 5, name: "Beşiktaş", shortName: "BJK", rank: 3 };
const KON: TeamSummaryDto = { id: 6, name: "Konyaspor", shortName: "KON", rank: 14 };
const RM: TeamSummaryDto  = { id: 101, name: "Real Madrid", shortName: "RM", rank: 1 };
const BAR: TeamSummaryDto = { id: 102, name: "Barcelona", shortName: "BAR", rank: 2 };

// ── Shared sub-objects ─────────────────────────────────────────────────────────

const COMMON_USER_PROTECTION: UserProtectionDto = {
  responsibilityNote:
    "Bu analiz bilgi desteği amaçlıdır. Tüm kararlar tamamen size aittir. FORMAX sonuç garantisi vermez.",
  decisionIsYours: true,
};

const COMMON_NABIZ: NabizSectionDto = {
  items: [
    {
      type: "News",
      source: "Fanatik",
      author: "Spor Redaksiyonu",
      authorVerified: true,
      headline: "Galatasaray'da Icardi 11'de başlayacak",
      summary: "Teknik direktör antrenman öncesi kadroyu açıkladı.",
      publishedAt: "2026-05-29T10:30:00Z",
    },
    {
      type: "Social",
      source: "Twitter",
      author: "@gs_haber",
      authorVerified: false,
      headline: "Derbide sürpriz 11 bekleniyor",
      publishedAt: "2026-05-29T09:15:00Z",
    },
  ],
};

const GS_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 901, opponent: "Sivasspor",   result: "W", score: "3-0", date: "2026-05-25", competition: "Süper Lig", isHome: true  },
  { matchId: 902, opponent: "Trabzonspor", result: "D", score: "1-1", date: "2026-05-18", competition: "Süper Lig", isHome: false },
  { matchId: 903, opponent: "Konyaspor",   result: "W", score: "2-0", date: "2026-05-11", competition: "Süper Lig", isHome: true  },
  { matchId: 904, opponent: "Beşiktaş",    result: "W", score: "2-1", date: "2026-05-04", competition: "Süper Lig", isHome: false },
  { matchId: 905, opponent: "Kayserispor", result: "W", score: "4-1", date: "2026-04-27", competition: "Süper Lig", isHome: true  },
];

const FB_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 911, opponent: "Alanyaspor",  result: "W", score: "2-0", date: "2026-05-25", competition: "Süper Lig", isHome: true  },
  { matchId: 912, opponent: "Konyaspor",   result: "L", score: "0-1", date: "2026-05-18", competition: "Süper Lig", isHome: false },
  { matchId: 913, opponent: "Başakşehir",  result: "W", score: "3-1", date: "2026-05-11", competition: "Süper Lig", isHome: true  },
  { matchId: 914, opponent: "Sivasspor",   result: "D", score: "2-2", date: "2026-05-04", competition: "Süper Lig", isHome: true  },
  { matchId: 915, opponent: "Trabzonspor", result: "W", score: "1-0", date: "2026-04-27", competition: "Süper Lig", isHome: false },
];

const TS_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 921, opponent: "Galatasaray", result: "D", score: "1-1", date: "2026-05-18", competition: "Süper Lig", isHome: true  },
  { matchId: 922, opponent: "Alanyaspor",  result: "W", score: "2-0", date: "2026-05-11", competition: "Süper Lig", isHome: false },
  { matchId: 923, opponent: "Fenerbahçe",  result: "L", score: "0-1", date: "2026-05-04", competition: "Süper Lig", isHome: true  },
  { matchId: 924, opponent: "Sivasspor",   result: "W", score: "3-1", date: "2026-04-27", competition: "Süper Lig", isHome: false },
  { matchId: 925, opponent: "Konyaspor",   result: "W", score: "2-1", date: "2026-04-20", competition: "Süper Lig", isHome: true  },
];

const BSK_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 931, opponent: "Fenerbahçe",  result: "L", score: "1-3", date: "2026-05-11", competition: "Süper Lig", isHome: false },
  { matchId: 932, opponent: "Beşiktaş",    result: "D", score: "0-0", date: "2026-05-04", competition: "Süper Lig", isHome: true  },
  { matchId: 933, opponent: "Sivasspor",   result: "W", score: "2-1", date: "2026-04-27", competition: "Süper Lig", isHome: false },
  { matchId: 934, opponent: "Konyaspor",   result: "L", score: "0-2", date: "2026-04-20", competition: "Süper Lig", isHome: true  },
  { matchId: 935, opponent: "Alanyaspor",  result: "W", score: "1-0", date: "2026-04-13", competition: "Süper Lig", isHome: false },
];

const BJK_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 941, opponent: "Alanyaspor",  result: "W", score: "2-0", date: "2026-05-22", competition: "Süper Lig", isHome: true  },
  { matchId: 942, opponent: "Galatasaray", result: "L", score: "1-2", date: "2026-05-04", competition: "Süper Lig", isHome: true  },
  { matchId: 943, opponent: "Başakşehir",  result: "D", score: "0-0", date: "2026-04-27", competition: "Süper Lig", isHome: false },
  { matchId: 944, opponent: "Sivasspor",   result: "W", score: "3-0", date: "2026-04-20", competition: "Süper Lig", isHome: true  },
  { matchId: 945, opponent: "Trabzonspor", result: "W", score: "2-1", date: "2026-04-13", competition: "Süper Lig", isHome: false },
];

const KON_LAST_MATCHES: LastMatchDto[] = [
  { matchId: 951, opponent: "Trabzonspor", result: "L", score: "1-2", date: "2026-05-22", competition: "Süper Lig", isHome: false },
  { matchId: 952, opponent: "Fenerbahçe",  result: "W", score: "1-0", date: "2026-05-18", competition: "Süper Lig", isHome: true  },
  { matchId: 953, opponent: "Galatasaray", result: "L", score: "0-2", date: "2026-05-11", competition: "Süper Lig", isHome: false },
  { matchId: 954, opponent: "Alanyaspor",  result: "D", score: "1-1", date: "2026-05-04", competition: "Süper Lig", isHome: true  },
  { matchId: 955, opponent: "Sivasspor",   result: "L", score: "0-1", date: "2026-04-27", competition: "Süper Lig", isHome: false },
];

const H2H_GS_FB: H2HDto = {
  homeWins: 3,
  awayWins: 2,
  draws: 1,
  matches: [
    { matchDate: "2025-11-10", homeTeamName: "Galatasaray", awayTeamName: "Fenerbahçe", homeScore: 2, awayScore: 1, competition: "Süper Lig" },
    { matchDate: "2025-04-05", homeTeamName: "Fenerbahçe", awayTeamName: "Galatasaray", homeScore: 1, awayScore: 2, competition: "Süper Lig" },
    { matchDate: "2024-12-08", homeTeamName: "Galatasaray", awayTeamName: "Fenerbahçe", homeScore: 3, awayScore: 0, competition: "Süper Lig" },
    { matchDate: "2024-04-21", homeTeamName: "Fenerbahçe", awayTeamName: "Galatasaray", homeScore: 1, awayScore: 1, competition: "Süper Lig" },
    { matchDate: "2023-11-05", homeTeamName: "Galatasaray", awayTeamName: "Fenerbahçe", homeScore: 0, awayScore: 1, competition: "Süper Lig" },
  ],
};

const H2H_TS_BSK: H2HDto = {
  homeWins: 4,
  awayWins: 1,
  draws: 2,
  matches: [
    { matchDate: "2025-12-14", homeTeamName: "Trabzonspor", awayTeamName: "Başakşehir FK", homeScore: 2, awayScore: 0, competition: "Süper Lig" },
    { matchDate: "2025-04-19", homeTeamName: "Başakşehir FK", awayTeamName: "Trabzonspor", homeScore: 1, awayScore: 2, competition: "Süper Lig" },
    { matchDate: "2024-10-27", homeTeamName: "Trabzonspor", awayTeamName: "Başakşehir FK", homeScore: 1, awayScore: 1, competition: "Süper Lig" },
  ],
};

const H2H_BJK_KON: H2HDto = {
  homeWins: 5,
  awayWins: 1,
  draws: 1,
  matches: [
    { matchDate: "2025-11-23", homeTeamName: "Beşiktaş", awayTeamName: "Konyaspor", homeScore: 3, awayScore: 0, competition: "Süper Lig" },
    { matchDate: "2025-04-06", homeTeamName: "Konyaspor", awayTeamName: "Beşiktaş", homeScore: 0, awayScore: 1, competition: "Süper Lig" },
    { matchDate: "2024-12-15", homeTeamName: "Beşiktaş", awayTeamName: "Konyaspor", homeScore: 2, awayScore: 1, competition: "Süper Lig" },
  ],
};

const TACTICAL_GS_FB: TacticalMatchupDto = {
  attack:     { label: "Atak",       homeScore: 85, awayScore: 82 },
  defense:    { label: "Savunma",    homeScore: 78, awayScore: 81 },
  transition: { label: "Geçiş",      homeScore: 80, awayScore: 77 },
  setPiece:   { label: "Standart",   homeScore: 74, awayScore: 71 },
  form:       { label: "Form",       homeScore: 88, awayScore: 72 },
  discipline: { label: "Disiplin",   homeScore: 69, awayScore: 73 },
};

const TACTICAL_TS_BSK: TacticalMatchupDto = {
  attack:     { label: "Atak",       homeScore: 72, awayScore: 65 },
  defense:    { label: "Savunma",    homeScore: 68, awayScore: 71 },
  transition: { label: "Geçiş",      homeScore: 75, awayScore: 62 },
  setPiece:   { label: "Standart",   homeScore: 70, awayScore: 58 },
  form:       { label: "Form",       homeScore: 66, awayScore: 54 },
  discipline: { label: "Disiplin",   homeScore: 72, awayScore: 69 },
};

const TACTICAL_BJK_KON: TacticalMatchupDto = {
  attack:     { label: "Atak",       homeScore: 78, awayScore: 52 },
  defense:    { label: "Savunma",    homeScore: 75, awayScore: 60 },
  transition: { label: "Geçiş",      homeScore: 73, awayScore: 55 },
  setPiece:   { label: "Standart",   homeScore: 68, awayScore: 57 },
  form:       { label: "Form",       homeScore: 80, awayScore: 48 },
  discipline: { label: "Disiplin",   homeScore: 65, awayScore: 72 },
};

const LINEUP_ANNOUNCED: LineupSectionDto = {
  lineupsAnnounced: true,
  homeStartingXI: [
    { shirtNumber: 1,  playerName: "Muslera",    position: "GK", isCaptain: false },
    { shirtNumber: 2,  playerName: "Yedlin",     position: "RB", isCaptain: false },
    { shirtNumber: 5,  playerName: "Sanchez",    position: "CB", isCaptain: false },
    { shirtNumber: 6,  playerName: "Nelsson",    position: "CB", isCaptain: false },
    { shirtNumber: 3,  playerName: "Angeliño",   position: "LB", isCaptain: false },
    { shirtNumber: 8,  playerName: "Torreira",   position: "CM", isCaptain: true  },
    { shirtNumber: 7,  playerName: "Demirbay",   position: "CM", isCaptain: false },
    { shirtNumber: 23, playerName: "Ziyech",     position: "RW", isCaptain: false },
    { shirtNumber: 10, playerName: "Mertens",    position: "AM", isCaptain: false },
    { shirtNumber: 17, playerName: "Seferovic",  position: "LW", isCaptain: false },
    { shirtNumber: 9,  playerName: "Icardi",     position: "ST", isCaptain: false },
  ],
  homeBench: [
    { shirtNumber: 22, playerName: "Gunok",      position: "GK", isCaptain: false },
    { shirtNumber: 4,  playerName: "Alashe",     position: "CM", isCaptain: false },
    { shirtNumber: 11, playerName: "Kerem",      position: "LW", isCaptain: false },
  ],
  awayStartingXI: [
    { shirtNumber: 1,  playerName: "Livakovic",  position: "GK", isCaptain: false },
    { shirtNumber: 16, playerName: "Osayi",      position: "RB", isCaptain: false },
    { shirtNumber: 4,  playerName: "Djiku",      position: "CB", isCaptain: false },
    { shirtNumber: 25, playerName: "Çağlar",     position: "CB", isCaptain: false },
    { shirtNumber: 3,  playerName: "Ferdi",      position: "LB", isCaptain: false },
    { shirtNumber: 8,  playerName: "Fred",       position: "CM", isCaptain: false },
    { shirtNumber: 17, playerName: "İrfan",      position: "CM", isCaptain: false },
    { shirtNumber: 10, playerName: "Szymanski",  position: "AM", isCaptain: true  },
    { shirtNumber: 7,  playerName: "Tadic",      position: "LW", isCaptain: false },
    { shirtNumber: 11, playerName: "Rodrigo",    position: "RW", isCaptain: false },
    { shirtNumber: 9,  playerName: "Dzeko",      position: "ST", isCaptain: false },
  ],
  awayBench: [
    { shirtNumber: 40, playerName: "Harun",      position: "GK", isCaptain: false },
    { shirtNumber: 20, playerName: "Alioski",    position: "LB", isCaptain: false },
    { shirtNumber: 14, playerName: "Miha",       position: "ST", isCaptain: false },
  ],
};

const LINEUP_NOT_ANNOUNCED: LineupSectionDto = {
  lineupsAnnounced: false,
  homeStartingXI: [],
  homeBench: [],
  awayStartingXI: [],
  awayBench: [],
};

const STANDINGS_SUPER_LIG: StandingSectionDto = {
  leagueId: 203,
  seasonYear: 2025,
  homeTeamPeek: {
    position: 1, teamName: "Galatasaray", played: 34, won: 24, drawn: 6, lost: 4,
    goalsFor: 78, goalsAgainst: 32, goalDifference: 46, points: 78,
    form: "WWWDW", isHighlighted: true,
  },
  awayTeamPeek: {
    position: 2, teamName: "Fenerbahçe", played: 34, won: 22, drawn: 5, lost: 7,
    goalsFor: 70, goalsAgainst: 38, goalDifference: 32, points: 71,
    form: "WWLDW", isHighlighted: true,
  },
  tableSlice: [
    { position: 1, teamName: "Galatasaray",  played: 34, won: 24, drawn: 6, lost: 4,  goalsFor: 78, goalsAgainst: 32, goalDifference: 46, points: 78, form: "WWWDW", isHighlighted: true  },
    { position: 2, teamName: "Fenerbahçe",   played: 34, won: 22, drawn: 5, lost: 7,  goalsFor: 70, goalsAgainst: 38, goalDifference: 32, points: 71, form: "WWLDW", isHighlighted: true  },
    { position: 3, teamName: "Beşiktaş",     played: 34, won: 19, drawn: 7, lost: 8,  goalsFor: 60, goalsAgainst: 40, goalDifference: 20, points: 64, form: "WDWWL", isHighlighted: false },
    { position: 4, teamName: "Trabzonspor",  played: 34, won: 16, drawn: 8, lost: 10, goalsFor: 54, goalsAgainst: 44, goalDifference: 10, points: 56, form: "WDLWW", isHighlighted: false },
    { position: 5, teamName: "Başakşehir",   played: 34, won: 13, drawn: 9, lost: 12, goalsFor: 45, goalsAgainst: 48, goalDifference: -3, points: 48, form: "LLDWW", isHighlighted: false },
  ],
};

// ── Home Feed: 4 RecommendationCardDto ───────────────────────────────────────

export const MOCK_FEED: RecommendationCardDto[] = [
  {
    matchId: 101,
    homeTeam: { id: 1, name: "Galatasaray", leagueRank: 1, avgGoalsFor: 2.29, avgGoalsAgainst: 0.94, isStableTeam: true },
    awayTeam: { id: 2, name: "Fenerbahçe",  leagueRank: 2, avgGoalsFor: 2.06, avgGoalsAgainst: 1.12, isStableTeam: true },
    teamA: "Galatasaray",
    teamB: "Fenerbahçe",
    homeRank: 1,
    awayRank: 2,
    matchDate: "2026-05-30T19:00:00Z",   // Yarın 22:00 (TR UTC+3)
    score: 91,
    recommendationScore: 91,
    confidenceScore: 87,
    confidenceLabel: "HIGH",
    cardType: "Title Race",
    personalReason: "Dev derby için yüksek oynanma sinyali tespit edildi.",
    recommendationReason: "TRENDING",
    trend: { playRate: 94, trendDelta: 12, globalTrend: 88, isTrending: true, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    external: { score: 82 },
    externalTrend: { oddsMovement: -0.18, marketConfidence: 79, isHot: true, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    insightLabel: "Lider Derbisi",
    insightReason: "İki takım arasında 7 puan fark var. Galatasaray galibiyetle şampiyonluğu ilan edebilir.",
    priority: 1,
    trendWeight: 0.88,
    trendImpact: 0.91,
    marketTrendScore: 82,
    userTrendScore: 94,
    globalTrendScore: 88,
    externalMomentum: 79,
    highlight: "Sezonun en büyük derbisi",
    aiComment: "Galatasaray ev sahibi avantajını iyi kullanıyor, son 5 derbide 4 galibiyet.",
    aiSummary: "Galatasaray ev sahibi avantajıyla güçlü konumda. Form farkı belirgin, piyasa sinyali de ev sahibini işaret ediyor.",
    tags: ["Derby", "Kritik Maç", "Ev Sahibi Formu"],
    storyHeadline: "⚔️ Dev Derbi",
    storyBody: "Galatasaray ile Fenerbahçe arasındaki derbi, taraftarların yakından takip ettiği karşılaşmalar arasında.",
    crossUserScore: 89,
    momentumScore: 78,
    spikeScore: 65,
    directionScore: 83,
    radarScore: 82,
    teamInterestScore: 70,
    leagueInterestScore: 65,
  },
  {
    matchId: 102,
    homeTeam: { id: 3, name: "Trabzonspor",    leagueRank: 4, avgGoalsFor: 1.59, avgGoalsAgainst: 1.29, isStableTeam: true },
    awayTeam: { id: 4, name: "Başakşehir FK",  leagueRank: 8, avgGoalsFor: 1.32, avgGoalsAgainst: 1.41, isStableTeam: false },
    teamA: "Trabzonspor",
    teamB: "Başakşehir FK",
    homeRank: 4,
    awayRank: 8,
    matchDate: "2026-05-30T16:00:00Z",   // Yarın 19:00 (TR UTC+3)
    score: 72,
    recommendationScore: 72,
    confidenceScore: 61,
    confidenceLabel: "MEDIUM",
    cardType: "Value",
    personalReason: "Küçük piyasa hareketi gözlemlendi.",
    recommendationReason: "FORM_ADVANTAGE",
    trend: { playRate: 61, trendDelta: 4, globalTrend: 58, isTrending: false, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    external: { score: 54 },
    externalTrend: { oddsMovement: -0.05, marketConfidence: 56, isHot: false, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    insightLabel: "Ev Sahibi Baskısı",
    insightReason: "Trabzonspor son 5 ev maçında 4 galibiyet. Başakşehir deplasmanda zayıf.",
    priority: 2,
    trendWeight: 0.58,
    trendImpact: 0.61,
    marketTrendScore: 54,
    userTrendScore: 61,
    globalTrendScore: 58,
    externalMomentum: 56,
    highlight: "Trabzonspor'da kritik randevu",
    aiComment: "Orta güven seviyesi. Trabzonspor ev sahibi avantajıyla hafif önde.",
    aiSummary: "Trabzonspor son dönem ev sahibi performansıyla öne çıkıyor. Başakşehir deplasman zafiyeti devam ediyor.",
    tags: ["Ev Avantajı", "Orta Güven"],
    storyHeadline: "📈 Ev Sahibi Formda",
    storyBody: "Trabzonspor evinde Başakşehir FK ile karşılaşıyor.",
    crossUserScore: 58,
    momentumScore: 62,
    spikeScore: 22,
    directionScore: 64,
    radarScore: 58,
    teamInterestScore: 50,
    leagueInterestScore: 55,
  },
  {
    matchId: 103,
    homeTeam: { id: 5, name: "Beşiktaş",   leagueRank: 3, avgGoalsFor: 1.76, avgGoalsAgainst: 1.18, isStableTeam: true },
    awayTeam: { id: 6, name: "Konyaspor",  leagueRank: 14, avgGoalsFor: 1.06, avgGoalsAgainst: 1.88, isStableTeam: false },
    teamA: "Beşiktaş",
    teamB: "Konyaspor",
    homeRank: 3,
    awayRank: 14,
    matchDate: "2026-06-01T13:00:00Z",   // Pazar 16:00 (TR UTC+3)
    score: 48,
    recommendationScore: 48,
    confidenceScore: 38,
    confidenceLabel: "LOW",
    cardType: "Standard",
    personalReason: "Düşük sinyal — bilgi için gösteriliyor.",
    recommendationReason: "GLOBAL_SIGNAL",
    trend: { playRate: 41, trendDelta: -2, globalTrend: 44, isTrending: false, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    external: { score: 36 },
    externalTrend: undefined,
    insightLabel: "Düşük Sinyal",
    insightReason: "Yeterli piyasa verisi yok. Sonuç belirsiz.",
    priority: 3,
    trendWeight: 0.38,
    trendImpact: 0.41,
    marketTrendScore: 36,
    userTrendScore: 41,
    globalTrendScore: 44,
    externalMomentum: 32,
    highlight: "Konyaspor son haftada sürpriz yaptı",
    aiComment: "Düşük güven. Sınırlı veri nedeniyle analiz kısıtlı.",
    aiSummary: "Yeterli piyasa sinyali tespit edilemedi. Beşiktaş puanını kollamak isteyecek.",
    tags: ["Düşük Güven", "İhtiyatlı"],
    storyHeadline: "🏟️ Türkiye - Süper Lig Maçı",
    storyBody: "Beşiktaş evinde Konyaspor ile karşılaşıyor.",
    crossUserScore: 39,
    momentumScore: 35,
    spikeScore: 8,
    directionScore: 42,
    radarScore: 40,
    teamInterestScore: 50,
    leagueInterestScore: 45,
  },
  {
    matchId: 201,
    homeTeam: { id: 101, name: "Real Madrid", leagueRank: 1, avgGoalsFor: 2.71, avgGoalsAgainst: 0.88, isStableTeam: true },
    awayTeam: { id: 102, name: "Barcelona",   leagueRank: 2, avgGoalsFor: 2.65, avgGoalsAgainst: 0.94, isStableTeam: true },
    teamA: "Real Madrid",
    teamB: "Barcelona",
    homeRank: 1,
    awayRank: 2,
    matchDate: "2026-06-03T18:45:00Z",   // Çarşamba 21:45 (TR UTC+3)
    score: 88,
    recommendationScore: 88,
    confidenceScore: 81,
    confidenceLabel: "HIGH",
    cardType: "ElClasico",
    personalReason: "El Clásico finali — en yüksek oynanma sinyali.",
    recommendationReason: "MARKET_SIGNAL",
    trend: { playRate: 97, trendDelta: 21, globalTrend: 96, isTrending: true, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    external: { score: 88 },
    externalTrend: { oddsMovement: -0.22, marketConfidence: 85, isHot: true, lastUpdatedAt: "2026-05-29T08:00:00Z" },
    insightLabel: "El Clásico",
    insightReason: "La Liga şampiyonluğu bu maçta belli olacak. Piyasa Real Madrid'i hafif favori gösteriyor.",
    priority: 1,
    trendWeight: 0.95,
    trendImpact: 0.97,
    marketTrendScore: 88,
    userTrendScore: 97,
    globalTrendScore: 96,
    externalMomentum: 85,
    highlight: "Şampiyonluk bu maçta belli olacak",
    aiComment: "Her iki takım da zirve formunda. Küçük Real Madrid avantajı ev sahipliğinden kaynaklanıyor.",
    aiSummary: "Tarihi El Clásico. Ev sahibi avantajı ve piyasa hareketi Real Madrid'i işaret ediyor, ancak Barcelona farkı kapatabilir.",
    tags: ["El Clásico", "Şampiyonluk", "Yüksek Risk"],
    storyHeadline: "🏆 Liderlik Yarışı",
    storyBody: "Real Madrid ile Barcelona arasındaki liderlik mücadelesi bu hafta sahaya taşınıyor.",
    crossUserScore: 92,
    momentumScore: 88,
    spikeScore: 74,
    directionScore: 81,
    radarScore: 76,
    teamInterestScore: 60,
    leagueInterestScore: 60,
  },
];

// ── Match Detail — Scheduled (Preview) ───────────────────────────────────────

export const MOCK_MATCH_SCHEDULED: MatchDetailDto = {
  matchId: 101,
  homeTeam: GS,
  awayTeam: FB,
  matchDate: "2026-05-30T19:00:00Z",
  status: "Scheduled",
  league: "Süper Lig",
  round: "Hafta 35",
  referee: "Ali Palabıyık",
  venue: "Rams Park, İstanbul",
  weather: "Açık, 22°C",
  watchersCount: 1847,
  homeTeamLastMatches: GS_LAST_MATCHES,
  awayTeamLastMatches: FB_LAST_MATCHES,
  comparison: {
    home: { avgGoalsFor: 2.29, avgGoalsAgainst: 0.94, goalScoringRate: 0.91, cleanSheetRate: 0.44, homeAwayAvgGoals: 2.6, formScore: 88, leagueRank: 1 },
    away: { avgGoalsFor: 2.06, avgGoalsAgainst: 1.12, goalScoringRate: 0.85, cleanSheetRate: 0.35, homeAwayAvgGoals: 1.8, formScore: 72, leagueRank: 2 },
  },
  h2h: H2H_GS_FB,
  insight: {
    headline: "Galatasaray son 3 derbide yenilmedi",
    summary: "Son 6 karşılaşmada Galatasaray 4 galibiyet, 2 beraberlik aldı. Ev sahibi avantajı ve form farkı kritik.",
  },
  sapma: {
    oynanmaSkoru: 87,
    gucSkoru: 78,
    sapma: 32,
    oynanmaYonu: "Home",
    gercekGucYonu: "Home",
    sapmaBolgesi: "Normal Bant",
    sessizMi: false,
    sapmaMetni: "Piyasa ve güç analizi uyumlu. Galatasaray yönünde belirgin oynanma sinyali var.",
  },
  ai: {
    state: "Extended",
    summary: "Galatasaray, Fenerbahçe karşısında hem form hem de ev sahipliği avantajına sahip. Son 5 haftada süregelen üstün performans, rakibin deplasman zafiyetiyle birleşince ciddi bir fark ortaya çıkıyor. Piyasa da bu yönü destekliyor.",
  },
  userProtection: COMMON_USER_PROTECTION,
  probabilities: [
    { market: "Galatasaray Galibiyet",  probability: 54, confidence: "MEDIUM" },
    { market: "Beraberlik",             probability: 24, confidence: "LOW"    },
    { market: "Fenerbahçe Galibiyet",   probability: 22, confidence: "LOW"    },
    { market: "İlk Yarı GS Gol",        probability: 71, confidence: "HIGH"   },
    { market: "Toplam 2+ Gol",          probability: 78, confidence: "HIGH"   },
  ],
  keyMatchups: [
    { homePlayer: "Icardi", awayPlayer: "Djiku", homePosition: "ST", awayPosition: "CB", matchupContext: "Icardi son 3 derbide gol attı." },
    { homePlayer: "Torreira", awayPlayer: "Fred", homePosition: "CM", awayPosition: "CM", matchupContext: "Orta saha hakimiyeti belirleyici." },
  ],
  marketIntelligence: {
    headline: "Galatasaray'da anormal oynanma artışı",
    detail: "Son 24 saatte ev sahibi yönünde %18 odds düşüşü gözlemlendi. Piyasa ciddi hareket sinyal veriyor.",
    tone: "positive",
  },
  riskIntelligence: {
    homeRiskLabel: "Düşük Risk",
    homeRiskDetail: "Galatasaray son 6 ev maçında sadece 1 mağlubiyet.",
    awayRiskLabel: "Orta Risk",
    awayRiskDetail: "Fenerbahçe deplasmanda son 3 maçta 1 galibiyet.",
  },
  tacticalMatchup: TACTICAL_GS_FB,
  lineup: LINEUP_ANNOUNCED,
  playerStatus: {
    injuries: [
      { playerName: "Seferovic", teamId: 1, status: "Injured", reason: "Uyluk ağrısı — maç kadrosu dışı" },
    ],
    suspensions: [],
    doubtful: [
      { playerName: "Demirbay", teamId: 1, status: "Doubtful", reason: "Antrenman limitasyonu" },
    ],
  },
  standing: STANDINGS_SUPER_LIG,
  competitionContext: undefined,
  live: { stats: undefined, timeline: [], momentum: [] },
  nabizFeed: COMMON_NABIZ,
};

// ── Match Detail — Live (63') ─────────────────────────────────────────────────

export const MOCK_MATCH_LIVE: MatchDetailDto = {
  matchId: 102,
  homeTeam: TS,
  awayTeam: BSK,
  matchDate: "2026-05-29T17:00:00Z",
  status: "Live",
  league: "Süper Lig",
  round: "Hafta 35",
  referee: "Mete Kalkavan",
  venue: "Papara Park, Trabzon",
  watchersCount: 632,
  homeTeamLastMatches: TS_LAST_MATCHES,
  awayTeamLastMatches: BSK_LAST_MATCHES,
  comparison: {
    home: { avgGoalsFor: 1.59, avgGoalsAgainst: 1.29, goalScoringRate: 0.74, cleanSheetRate: 0.26, homeAwayAvgGoals: 1.9, formScore: 66, leagueRank: 4 },
    away: { avgGoalsFor: 1.32, avgGoalsAgainst: 1.41, goalScoringRate: 0.62, cleanSheetRate: 0.21, homeAwayAvgGoals: 1.1, formScore: 54, leagueRank: 8 },
  },
  h2h: H2H_TS_BSK,
  insight: {
    headline: "Maç Devam Ediyor",
    summary: "63. dakikada 1-1 eşitlik. Trabzonspor ikinci yarıda baskı kurmaya çalışıyor.",
  },
  sapma: {
    oynanmaSkoru: 61,
    gucSkoru: 68,
    sapma: 22,
    oynanmaYonu: "Home",
    gercekGucYonu: "Home",
    sapmaBolgesi: "Normal Bant",
    sessizMi: false,
    sapmaMetni: "Canlı veriye göre Trabzonspor lehine hafif momentum sinyali.",
  },
  ai: {
    state: "Short",
    summary: "1-1 eşitlikte Trabzonspor baskı kuruyor. Son 10 dakikada ev sahibi yönünde momentum. Başakşehir savunmada zorluk yaşıyor.",
  },
  userProtection: COMMON_USER_PROTECTION,
  probabilities: [],
  keyMatchups: [],
  marketIntelligence: {
    headline: "Canlı piyasa Trabzonspor'u işaret ediyor",
    detail: "63. dakika itibarıyla Trabzonspor galibiyeti oranı düştü, yoğun oynanma devam ediyor.",
    tone: "positive",
  },
  riskIntelligence: {
    homeRiskLabel: "Orta Risk",
    homeRiskDetail: "1-1 eşitlik devam ederse Trabzonspor Avrupa şansını kaybedebilir.",
    awayRiskLabel: "Yüksek Risk",
    awayRiskDetail: "Başakşehir savunma baskısı altında, ikinci sarı riski var.",
  },
  tacticalMatchup: TACTICAL_TS_BSK,
  lineup: LINEUP_ANNOUNCED,
  playerStatus: { injuries: [], suspensions: [], doubtful: [] },
  standing: {
    leagueId: 203,
    seasonYear: 2025,
    homeTeamPeek: { position: 4, teamName: "Trabzonspor", played: 34, won: 16, drawn: 8, lost: 10, goalsFor: 54, goalsAgainst: 44, goalDifference: 10, points: 56, form: "WDLWW", isHighlighted: true },
    awayTeamPeek: { position: 8, teamName: "Başakşehir FK", played: 34, won: 13, drawn: 9, lost: 12, goalsFor: 45, goalsAgainst: 48, goalDifference: -3, points: 48, form: "LLDWW", isHighlighted: true },
    tableSlice: [],
  },
  live: {
    stats: {
      homeScore: 1,
      awayScore: 1,
      minute: 63,
      phase: "SecondHalf",
      possessionHome: 56,
      possessionAway: 44,
      shotsHome: 9,
      shotsAway: 5,
      shotsOnTargetHome: 4,
      shotsOnTargetAway: 2,
      cornersHome: 6,
      cornersAway: 2,
      foulsHome: 8,
      foulsAway: 11,
      offsidesHome: 2,
      offsidesAway: 1,
      yellowHome: 1,
      yellowAway: 2,
      redHome: 0,
      redAway: 0,
      dangerousAttacksHome: 42,
      dangerousAttacksAway: 19,
      xgHome: 1.4,
      xgAway: 0.8,
      updatedAt: "2026-05-29T18:03:00Z",
    },
    timeline: [
      { minute: 12, eventType: "Goal",        teamName: "Trabzonspor",   playerName: "Berat Özdemir",   detail: "Sol köşeden sert vuruş",      impactScore: 9 },
      { minute: 28, eventType: "YellowCard",   teamName: "Başakşehir FK", playerName: "Emre Belözoğlu",  detail: "Sert müdahale",               impactScore: 3 },
      { minute: 39, eventType: "Goal",         teamName: "Başakşehir FK", playerName: "Stefano Napoleoni", detail: "Penaltıdan gol",             impactScore: 9 },
      { minute: 45, eventType: "YellowCard",   teamName: "Trabzonspor",   playerName: "Vitor Hugo",      detail: "İtiraz",                      impactScore: 2 },
      { minute: 54, eventType: "Substitution", teamName: "Trabzonspor",   playerName: "Paulo Vitor",     detail: "Paulo Vitor ⬆ Hamsik ⬇",    impactScore: 4 },
      { minute: 61, eventType: "YellowCard",   teamName: "Başakşehir FK", playerName: "Junior Caiçara",  detail: "Geç müdahale",                impactScore: 4 },
    ],
    momentum: [
      { minute: 5,  homePressure: 55, awayPressure: 45 },
      { minute: 10, homePressure: 62, awayPressure: 38 },
      { minute: 15, homePressure: 48, awayPressure: 52 },
      { minute: 20, homePressure: 50, awayPressure: 50 },
      { minute: 25, homePressure: 44, awayPressure: 56 },
      { minute: 30, homePressure: 47, awayPressure: 53 },
      { minute: 35, homePressure: 52, awayPressure: 48 },
      { minute: 40, homePressure: 49, awayPressure: 51 },
      { minute: 45, homePressure: 53, awayPressure: 47 },
      { minute: 50, homePressure: 60, awayPressure: 40 },
      { minute: 55, homePressure: 65, awayPressure: 35 },
      { minute: 60, homePressure: 68, awayPressure: 32 },
    ],
  },
  nabizFeed: { items: [] },
};

// ── Match Detail — Finished ───────────────────────────────────────────────────

export const MOCK_MATCH_FINISHED: MatchDetailDto = {
  matchId: 103,
  homeTeam: BJK,
  awayTeam: KON,
  matchDate: "2026-05-29T14:00:00Z",
  status: "Finished",
  league: "Süper Lig",
  round: "Hafta 35",
  referee: "Cüneyt Çakır",
  venue: "Tüpraş Stadyumu, İstanbul",
  watchersCount: 289,
  homeTeamLastMatches: BJK_LAST_MATCHES,
  awayTeamLastMatches: KON_LAST_MATCHES,
  comparison: {
    home: { avgGoalsFor: 1.76, avgGoalsAgainst: 1.18, goalScoringRate: 0.79, cleanSheetRate: 0.31, homeAwayAvgGoals: 2.1, formScore: 80, leagueRank: 3 },
    away: { avgGoalsFor: 1.06, avgGoalsAgainst: 1.88, goalScoringRate: 0.47, cleanSheetRate: 0.15, homeAwayAvgGoals: 0.9, formScore: 48, leagueRank: 14 },
  },
  h2h: H2H_BJK_KON,
  insight: {
    headline: "Beşiktaş 2-1 galip",
    summary: "Beşiktaş zorlu bir maçtan 3 puanla ayrıldı. Konyaspor son dakikada gol attı ama yetmedi.",
  },
  sapma: {
    oynanmaSkoru: 72,
    gucSkoru: 79,
    sapma: 18,
    oynanmaYonu: "Home",
    gercekGucYonu: "Home",
    sapmaBolgesi: "Normal Bant",
    sessizMi: false,
    sapmaMetni: "Maç öncesi sinyal doğrulandı. Beşiktaş güç üstünlüğünü sonuca yansıttı.",
  },
  ai: {
    state: "Extended",
    summary: "Beşiktaş beklentileri karşıladı. Güç ve piyasa sinyalleri ev sahibini gösteriyordu ve sonuç buna uydu. Konyaspor 89'daki golü maçı heyecanlı bitirdi.",
  },
  userProtection: COMMON_USER_PROTECTION,
  probabilities: [],
  keyMatchups: [],
  marketIntelligence: {
    headline: "Sinyal doğrulama: Beşiktaş favori konumunu korudu",
    detail: "Maç öncesi piyasa analizi doğrulandı. Konyaspor geç golü sürpriz olmasa da skoru değiştirmedi.",
    tone: "positive",
  },
  riskIntelligence: {
    homeRiskLabel: "Gerçekleşti",
    homeRiskDetail: "Beşiktaş 90+3'te gol yedi ama 3 puanı aldı.",
    awayRiskLabel: "Gerçekleşmedi",
    awayRiskDetail: "Konyaspor puan alamadı.",
  },
  tacticalMatchup: TACTICAL_BJK_KON,
  lineup: LINEUP_ANNOUNCED,
  playerStatus: { injuries: [], suspensions: [], doubtful: [] },
  standing: {
    leagueId: 203,
    seasonYear: 2025,
    homeTeamPeek: { position: 3, teamName: "Beşiktaş", played: 35, won: 20, drawn: 7, lost: 8, goalsFor: 62, goalsAgainst: 41, goalDifference: 21, points: 67, form: "WWDWW", isHighlighted: true },
    awayTeamPeek: { position: 14, teamName: "Konyaspor", played: 35, won: 9, drawn: 7, lost: 19, goalsFor: 36, goalsAgainst: 58, goalDifference: -22, points: 34, form: "LLWLL", isHighlighted: true },
    tableSlice: [],
  },
  live: {
    stats: {
      homeScore: 2,
      awayScore: 1,
      minute: 90,
      phase: "Finished",
      possessionHome: 61,
      possessionAway: 39,
      shotsHome: 14,
      shotsAway: 7,
      shotsOnTargetHome: 6,
      shotsOnTargetAway: 3,
      cornersHome: 8,
      cornersAway: 3,
      foulsHome: 10,
      foulsAway: 14,
      offsidesHome: 3,
      offsidesAway: 2,
      yellowHome: 1,
      yellowAway: 3,
      redHome: 0,
      redAway: 0,
      dangerousAttacksHome: 58,
      dangerousAttacksAway: 27,
      xgHome: 1.9,
      xgAway: 0.7,
      updatedAt: "2026-05-29T15:48:00Z",
    },
    timeline: [
      { minute: 22, eventType: "Goal",        teamName: "Beşiktaş",   playerName: "Semih Kılıçsoy",  detail: "Süper vuruş — sağ köşe",    impactScore: 9 },
      { minute: 35, eventType: "YellowCard",  teamName: "Konyaspor",  playerName: "Ali Turan",       detail: "Sert müdahale",              impactScore: 3 },
      { minute: 51, eventType: "Goal",        teamName: "Beşiktaş",   playerName: "Rafa Silva",      detail: "Kafa vuruşu — kornerden",   impactScore: 9 },
      { minute: 67, eventType: "YellowCard",  teamName: "Beşiktaş",   playerName: "Al-Musrati",      detail: "Geri gitme ihlali",          impactScore: 2 },
      { minute: 75, eventType: "Substitution", teamName: "Konyaspor", playerName: "Salih Dursun",    detail: "Salih ⬆ Endri ⬇",          impactScore: 3 },
      { minute: 78, eventType: "YellowCard",  teamName: "Konyaspor",  playerName: "Endri Çekiçi",    detail: "İtiraz",                     impactScore: 2 },
      { minute: 89, eventType: "Goal",        teamName: "Konyaspor",  playerName: "Bilal Başaçıkoğlu", detail: "Uzaktan sert vuruş",     impactScore: 7 },
      { minute: 90, eventType: "YellowCard",  teamName: "Konyaspor",  playerName: "Bekir Aksoy",     detail: "Maç sonu protesto",          impactScore: 1 },
    ],
    momentum: [],
  },
  nabizFeed: { items: [] },
};
