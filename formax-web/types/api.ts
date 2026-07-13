// ─────────────────────────────────────────────────────────────────────────────
// FORMAX API Types — mirrors Formax.Application DTOs exactly
// Shapes come from: RecommendationCardDto, MatchDetailDto and live DTOs
// ─────────────────────────────────────────────────────────────────────────────

// ── Auth ─────────────────────────────────────────────────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  // Backend RegisterRequestDto has only Email + Password — no name field
}

export interface AuthResponse {
  // Mirrors AuthResponseDto exactly
  token: string;
  userId: number;
  isPremium: boolean;
  accessLevel: string; // "Free" | "Premium" | ...
}

// ── Shared ────────────────────────────────────────────────────────────────────

// Mirrors Formax.Application.DTOs.Teams.TeamDto exactly
export interface TeamDto {
  id: number;
  name: string;
  leagueRank: number;
  avgGoalsFor: number;
  avgGoalsAgainst: number;
  isStableTeam: boolean;
  logoUrl?: string;
  colorPrimary?: string;
  colorSecondary?: string;
}

// ── Home Feed: RecommendationCardDto ─────────────────────────────────────────

// Mirrors Formax.Application.DTOs.Recommendations.TrendDto
export interface TrendDto {
  playRate: number;
  trendDelta: number;
  globalTrend: number;
  isTrending: boolean;
  lastUpdatedAt?: string; // DateTime? serialized as ISO string or null
}

// Mirrors Formax.Application.DTOs.Recommendations.ExternalDto
export interface ExternalDto {
  score: number; // single field only
}

// Mirrors Formax.Engine.Core.ExternalTrends.ExternalTrendDto
export interface ExternalTrendDto {
  oddsMovement: number;
  marketConfidence: number;
  isHot: boolean;
  lastUpdatedAt?: string; // DateTime? → ISO string or null
}

export type ConfidenceLabel = "HIGH" | "MEDIUM" | "LOW";

// LOCKED design — FORMAX AI key signal (3 sütun). Backend ileride doldurur.
export interface KeySignal {
  icon?: "fire" | "ball" | "home" | string;
  title: string;
  value?: string;
  caption?: string;
  tone?: "purple" | "orange" | "green" | "default" | string;
}

export interface RecommendationCardDto {
  matchId: number;
  homeTeam: TeamDto;
  awayTeam: TeamDto;
  teamA: string;
  teamB: string;
  /** League position (1-based). Null when unknown. Importance signal. */
  homeRank?: number | null;
  awayRank?: number | null;
  /** ISO date string — needs backend support; countdown shown when present */
  matchDate?: string;
  score: number;
  recommendationScore: number;
  confidenceScore: number;
  confidenceLabel: ConfidenceLabel;
  cardType: string;
  personalReason: string;
  /** Deterministic reason code: FOLLOWED_TEAM | HIGH_INTEREST | TRENDING | MARKET_SIGNAL | GLOBAL_SIGNAL */
  recommendationReason: string;
  trend: TrendDto;
  external: ExternalDto;
  externalTrend?: ExternalTrendDto;
  insightLabel: string;
  insightReason: string;
  priority: number;
  trendWeight: number;
  trendImpact: number;
  marketTrendScore: number;
  userTrendScore: number;
  globalTrendScore: number;
  externalMomentum: number;
  highlight: string;
  aiComment: string;
  aiSummary: string;
  tags: string[];
  storyHeadline: string;
  storyBody: string;
  crossUserScore: number;
  momentumScore: number;
  spikeScore: number;
  directionScore: number;
  // R.14.7 — Radar carry-through
  radarScore: number;
  teamInterestScore: number;
  leagueInterestScore: number;
  // LOCKED design — backend ileride doldurur (opsiyonel; gelmezse component gracefully gizlenir)
  aiHeadline?: string;
  radarLevel?: "YÜKSEK" | "ORTA" | "DÜŞÜK" | string;
  radarReason?: string;
  keySignals?: KeySignal[];
  matchImportance?: string;
  leagueName?: string;
  kickoffTime?: string;
}

// ── Match Detail: MatchDetailDto ──────────────────────────────────────────────

// Mirrors Formax.Application.DTOs.Matches.TeamSummaryDto
export interface TeamSummaryDto {
  id: number;
  name: string;
  shortName?: string; // ShortName? in backend
  logoUrl?: string;
  rank?: number;
}

// Mirrors Formax.Application.DTOs.Matches.LastMatchDto
export interface LastMatchDto {
  matchId: number | null; // int? in backend
  opponent: string;
  result: "W" | "D" | "L";
  score: string;
  date: string;
  competition: string;
  isHome: boolean;
}

export interface TeamComparisonDto {
  avgGoalsFor: number;
  avgGoalsAgainst: number;
  goalScoringRate: number;
  cleanSheetRate: number;
  homeAwayAvgGoals: number;
  formScore: number;
  leagueRank: number;
}

export interface ComparisonDto {
  home: TeamComparisonDto;
  away: TeamComparisonDto;
}

export interface H2HMatchDto {
  matchDate: string;
  homeTeamName: string;
  awayTeamName: string;
  homeScore: number;
  awayScore: number;
  competition: string;
}

export interface H2HDto {
  homeWins: number;
  awayWins: number;
  draws: number;
  matches: H2HMatchDto[];
}

export interface InsightDto {
  headline: string;
  summary: string;
}

export interface SapmaDto {
  oynanmaSkoru: number;
  gucSkoru: number;
  sapma: number;
  oynanmaYonu: string;
  gercekGucYonu: string;
  sapmaBolgesi: string;
  sessizMi: boolean;
  sapmaMetni: string;
}

export interface AiDto {
  state: "Extended" | "Short" | "SelfRetracted" | "Silent";
  summary: string;
}

export interface UserProtectionDto {
  responsibilityNote: string;
  decisionIsYours: boolean;
}

export interface ProbabilityItemDto {
  market: string;
  probability: number;
  confidence: string;
}

export interface KeyMatchupDto {
  homePlayer: string;
  awayPlayer: string;
  homePosition: string;
  awayPosition: string;
  matchupContext: string;
}

export interface MarketIntelligenceDto {
  headline: string;
  detail: string;
  tone: "positive" | "negative" | "neutral";
}

export interface RiskIntelligenceDto {
  homeRiskLabel: string;
  homeRiskDetail: string;
  awayRiskLabel: string;
  awayRiskDetail: string;
}

export interface TacticalDimensionDto {
  label: string;
  homeScore: number;
  awayScore: number;
}

export interface TacticalMatchupDto {
  attack: TacticalDimensionDto;
  defense: TacticalDimensionDto;
  transition: TacticalDimensionDto;
  setPiece: TacticalDimensionDto;
  form: TacticalDimensionDto;
  discipline: TacticalDimensionDto;
}

export interface LineupPlayerDto {
  shirtNumber: number;
  playerName: string;
  position: string;
  isCaptain: boolean;
}

export interface LineupSectionDto {
  lineupsAnnounced: boolean;
  homeStartingXI: LineupPlayerDto[];
  homeBench: LineupPlayerDto[];
  awayStartingXI: LineupPlayerDto[];
  awayBench: LineupPlayerDto[];
}

export interface PlayerStatusDto {
  playerName: string;
  teamId: number;
  status: string;
  reason: string;
}

export interface PlayerStatusSectionDto {
  injuries: PlayerStatusDto[];
  suspensions: PlayerStatusDto[];
  doubtful: PlayerStatusDto[];
}

export interface TeamStandingDto {
  position: number;
  teamName: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
  form: string;
  isHighlighted: boolean;
}

export interface StandingSectionDto {
  leagueId: number;
  seasonYear: number;
  homeTeamPeek?: TeamStandingDto;
  awayTeamPeek?: TeamStandingDto;
  tableSlice: TeamStandingDto[];
}

export interface CompetitionContextSectionDto {
  competitionType: string;
  stageName: string;
  contextHeadline: string;
  contextSummary: string;
  bracketJson?: string;
}

// Mirrors Formax.Application.DTOs.Live.LiveStatsDto
export interface LiveStatsDto {
  homeScore: number;
  awayScore: number;
  minute: number | null;   // int? in backend
  phase: string;
  possessionHome: number;
  possessionAway: number;
  shotsHome: number;
  shotsAway: number;
  shotsOnTargetHome: number;
  shotsOnTargetAway: number;
  cornersHome: number;
  cornersAway: number;
  foulsHome: number;
  foulsAway: number;
  offsidesHome: number;
  offsidesAway: number;
  yellowHome: number;
  yellowAway: number;
  redHome: number;
  redAway: number;
  dangerousAttacksHome: number;
  dangerousAttacksAway: number;
  xgHome: number | null;   // double? in backend
  xgAway: number | null;   // double? in backend
  updatedAt: string;
}

export interface LiveEventDto {
  minute: number;
  eventType: string;
  teamName: string;
  playerName: string;
  detail: string;
  impactScore: number;
}

export interface MomentumSnapshotDto {
  minute: number;
  homePressure: number;
  awayPressure: number;
}

export interface LiveSectionDto {
  stats?: LiveStatsDto;
  timeline: LiveEventDto[];
  momentum: MomentumSnapshotDto[];
}

export interface NabizFeedItemDto {
  type: string;
  source: string;
  author: string;
  authorVerified: boolean;
  headline: string;
  summary?: string;
  imageUrl?: string;
  sourceUrl?: string;
  publishedAt: string;
}

export interface NabizSectionDto {
  items: NabizFeedItemDto[];
}

export interface MatchDetailDto {
  matchId: number;
  homeTeam: TeamSummaryDto;
  awayTeam: TeamSummaryDto;
  matchDate: string;
  status: string;
  league: string;
  round?: string;
  referee?: string;
  venue?: string;
  weather?: string;
  watchersCount: number;
  homeTeamLastMatches: LastMatchDto[];
  awayTeamLastMatches: LastMatchDto[];
  comparison: ComparisonDto;
  h2h: H2HDto;
  insight: InsightDto;
  sapma: SapmaDto;
  ai: AiDto;
  userProtection: UserProtectionDto;
  probabilities: ProbabilityItemDto[];
  keyMatchups: KeyMatchupDto[];
  marketIntelligence: MarketIntelligenceDto;
  riskIntelligence: RiskIntelligenceDto;
  tacticalMatchup: TacticalMatchupDto;
  lineup: LineupSectionDto;
  playerStatus: PlayerStatusSectionDto;
  standing?: StandingSectionDto;
  competitionContext?: CompetitionContextSectionDto;
  live: LiveSectionDto;
  nabizFeed: NabizSectionDto;
}

// ── Follow: MatchListItemDto ──────────────────────────────────────────────────
// Mirrors Formax.Application.DTOs.Matches.MatchListItemDto

export interface FollowedMatchDto {
  matchId: number;
  homeTeam: string;
  awayTeam: string;
  league: string;
  startTime: string; // ISO date string
  score?: { home: number; away: number };
  minute?: number | null;
  status: string;
  // Sapma fields (optional — UI shows text only)
  sessizMi?: boolean;
  sapmaMetni?: string;
  sapmaBolgesi?: string;
}

// ── Swipe action ──────────────────────────────────────────────────────────────

export type SwipeAction = "like" | "skip" | "view" | "detail" | "detail_return" | "follow";
export type SwipeDirection = "left" | "right";

export interface SwipeRequest {
  matchId: number;
  action: SwipeAction;
  confidenceLabel?: string;
  isTrending?: boolean;
  oddsDrop?: number;
  odds?: number;
  viewDurationMs?: number;
  detailDurationMs?: number;
  swipeDirection?: SwipeDirection;
  team?: string;
}
