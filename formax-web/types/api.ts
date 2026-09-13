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

// ── AI Discovery Engine DTO'ları (backend = Single Source Of Truth) ───────────
export type MatchStatus =
  | "Upcoming"
  | "Live"
  | "Finished"
  | "FullTime"
  | "AfterExtraTime"
  | "AfterPenalties"
  | "Cancelled"
  | "Postponed"
  | "Suspended"
  | "Abandoned"
  | "Walkover"
  | "Awarded"
  | string;

export type OddsMovement = "Up" | "Down" | "None" | string;

/** Backend'in maç başına hesapladığı tek en güçlü sonuç (FAZ 2). */
export interface TopPredictionDto {
  market: string;
  probability: number;
  /** GERÇEK market oranı (backend MatchMarketOdds). Karşılığı yoksa null. */
  odd?: number | null;
}

/** Backend'in maç başına hesapladığı ilk 3 AI tahmini (FAZ 3). Oran/hareket backend'den. */
export interface AiPredictionDto {
  market: string;
  probability: number;
  confidence: string;
  /** GERÇEK market oranı. Sağlayıcıda karşılığı olmayan markette null → gösterilmez. */
  currentOdd?: number | null;
  previousOdd?: number | null;
  movement: OddsMovement;
  updatedAt?: string;
}

export interface StadiumDto {
  name?: string;
  city?: string;
  imageUrl?: string | null;
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

  /**
   * Keşfet anlatısı (Match Intelligence / Gemma) — feed yanıtıyla gelir.
   * Backend'de daha önce üretilmiş snapshot varsa dolu, yoksa boştur; Keşfet
   * bu alanlar için maç detayı ucunu ÇAĞIRMAZ ve metin ÜRETMEZ.
   */
  radarSummary?: string;
  radarHighlights?: string[];

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

  // ── AI Discovery Engine (FAZ 1) — backend ekledikçe dolar; yoksa component gizler ──
  status?: MatchStatus;
  isLive?: boolean;
  liveMinute?: number | null;
  homeScore?: number | null;
  awayScore?: number | null;
  aiTrustScore?: number;
  userInterestScore?: number;
  topPrediction?: TopPredictionDto | null;
  predictions?: AiPredictionDto[];
  stadium?: StadiumDto | null;
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
  /** İlk yarı skoru — MAÇIN yönünde (ev - deplasman). Yoksa null → UI "—" gösterir. */
  halfTimeHomeScore?: number | null;
  halfTimeAwayScore?: number | null;
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
  /** İlk yarı skoru (ev - deplasman). Sağlayıcı vermediyse null → UI "—" gösterir. */
  halfTimeHomeScore?: number | null;
  halfTimeAwayScore?: number | null;
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
  /**
   * Sağlayıcının açıkladığı saha koordinatı "hat:sıra" (ör. "1:1", "2:4").
   * Diziliş BUNDAN çizilir. Yoksa null — frontend konum ÜRETMEZ.
   */
  grid?: string | null;
  isCaptain: boolean;
}

export interface LineupSectionDto {
  lineupsAnnounced: boolean;
  /** Kickoff'a 90 dk veya daha az kaldı mı? false iken ekran SAAT SÖZÜ VERMEZ. */
  pollingWindowOpen?: boolean;
  /** Kickoff geçti mi? Geçtiyse ve veri yoksa ekran bunu açıkça söyler. */
  kickoffPassed?: boolean;
  /** Sağlayıcıya en son ne zaman soruldu (UTC ISO). Hiç sorulmadıysa null. */
  lastCheckedUtc?: string | null;
  /**
   * Backend kadro durumu: "Released" | "SourceDelayed" | "Waiting" | "NotFound".
   * SourceDelayed = kontroller sürüyor, lisanslı veri kaynağı kadroyu henüz iletmedi.
   */
  status?: string | null;
  /** Açıklanan diziliş ("4-4-2"). Takım başına AYRI; yoksa null (tahmin edilmez). */
  homeFormation?: string | null;
  awayFormation?: string | null;
  homeStartingXI: LineupPlayerDto[];
  homeBench: LineupPlayerDto[];
  awayStartingXI: LineupPlayerDto[];
  awayBench: LineupPlayerDto[];
  /** Kadronun alındığı resmî kaynak (ör. "Lega Serie A"); eski kayıtta null. */
  source?: string | null;
  /** Taraf bazında yayım — kulüp yalnız kendi ilk 11'ini açıkladıysa diğeri false. */
  homeReleased?: boolean;
  awayReleased?: boolean;
  /** Kaynak verdiyse teknik direktör; yoksa null (uydurulmaz). */
  homeCoach?: string | null;
  awayCoach?: string | null;
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

/** Tek bir ligin TAM puan durumu tablosu (sıralama backend'den gelir). */
export interface StandingTableDto {
  leagueId: number;
  leagueName: string;
  seasonYear: number;
  rows: TeamStandingDto[];
}

export interface TeamSeasonSplitDto {
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
}

/** Mirrors Formax.Application.DTOs.Matches.TeamSeasonFormDto */
export interface TeamSeasonFormDto {
  teamId: number;
  teamName: string;
  leagueId: number;
  leagueName: string;
  seasonYear: number;
  /** "2026/27" */
  seasonLabel: string;
  seasonStartUtc: string;
  windowEndUtc: string;
  firstMatchUtc?: string | null;
  lastMatchUtc?: string | null;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  goalsFor: number;
  goalsAgainst: number;
  goalDifference: number;
  points: number;
  home: TeamSeasonSplitDto;
  away: TeamSeasonSplitDto;
  /** Hesaba giren GERÇEK maç id leri (en yeniden eskiye). */
  matchIds: number[];
  usedMatchCount: number;
  /** 3ten az tamamlanmış maç → ekran bunu açıkça söyler. */
  isLimitedSample: boolean;
  hasNoData: boolean;
  /** "Son 5 maç" ifadesi yalnız true iken kullanılabilir. */
  allowsLastFivePhrase: boolean;
  /** TEŞHİS — ligin bu sezondaki beklenen tamamlanmış maç sayısı. UI OKUMAZ. */
  seasonExpectedFixtures: number;
  /** TEŞHİS — LİG genelinde sonucu gelmemiş maç sayısı. UI OKUMAZ (06.09.2026). */
  seasonMissingFixtures: number;
  /** TEŞHİS — ligin verisi eksiksiz mi? Form kapısı DEĞİLDİR; UI OKUMAZ. */
  isSeasonDataComplete: boolean;
  /** BU TAKIMIN sonucu kesinleşmemiş maç sayısı (ligin geri kalanı sayılmaz). */
  teamMissingResultCount: number;
  /** Sınırlamanın sebebi olan maç id leri (teşhis). */
  teamMissingResultMatchIds: number[];
  /** "None" | "Minimal" | "Limited" | "Sufficient" — anlatı dilinin kapısı. */
  sampleQuality: string;
  /** Genel form yorumu izni — YALNIZ takımın kendi örneklemine bakar. */
  allowsGeneralization: boolean;
  /** Backend in yazdığı deterministik form cümlesi. */
  sentence: string;
  /** G/B/M dizisi (en yeni önce). */
  resultSequence: string;
  /**
   * Kullanıcıya gösterilebilir sınırlama notu — teknik terim İÇERMEZ ve yalnız
   * BU TAKIMIN kendi maç sonucu kesinleşmediyse dolar. Boşsa uyarı gösterilmez.
   */
  limitationNote: string;
}

export interface StandingSectionDto {
  leagueId: number;
  leagueName?: string;
  seasonYear: number;

  // ── İç kaynaklı projeksiyon üst verisi (backend LeagueStandingsSnapshot) ──
  // Tablo artık FORMAX in KENDİ tamamlanmış maçlarından saatlik üretilir. Sağlayıcı
  // tablosuna düşüldüğünde bu alanlar GELMEZ (undefined) — sahte tazelik gösterilmez.
  /** Sezonun gerçek başlangıcı (ligin bu sezondaki ilk maçı). */
  seasonStartDate?: string;
  /** Projeksiyonun üretildiği an — "Son güncelleme" bunu gösterir. */
  calculatedAtUtc?: string;
  /** Tabloya giren en son tamamlanmış maçın tarihi. */
  lastIncludedMatchUtc?: string;
  /** "InternalResultsProjection". */
  source?: string;
  /** 2 saatten yeni mi. false → "Puan durumu güncelleniyor". */
  isFresh?: boolean;
  /** Sıra resmî değil (ligin eşitlik kuralı uygulanamadı). */
  isProvisional?: boolean;
  /** Uygulanan sıralama kuralının kimliği. */
  rankingRuleId?: string;
  /** Tabloya giren tamamlanmış maç sayısı. */
  matchesIncluded?: number;

  // ── Veri tamlığı ────────────────────────────────────────────────────────
  /** Bu ana kadar oynanmış OLMASI GEREKEN lig maçı sayısı. */
  expectedCompletedFixtures?: number;
  /** Sonucu kesinleşmiş ve tabloya giren maç sayısı. */
  includedCompletedFixtures?: number;
  /** Sonucu hâlâ gelmemiş maç sayısı. */
  missingCompletedFixtures?: number;
  /**
   * false → tablo EKSİK. isFresh true olsa bile "güncel/resmî" diye GÖSTERİLMEZ;
   * ekran "Puan durumu verileri tamamlanıyor" der.
   */
  isComplete?: boolean;
  /**
   * Ertelenmiş maç sayısı. Tabloyu EKSİK YAPMAZ: ertelenen maç oynanmamıştır,
   * sonucu beklenmez ve takımların oynadığı maç sayısının farklı olması normaldir.
   * Yalnız bilgi olarak gösterilir; uyarı DEĞİLDİR.
   */
  postponedFixtures?: number;
  cancelledFixtures?: number;
  abandonedFixtures?: number;
  /** Oynanması beklenip sonucu hâlâ gelmemiş maç sayısı. */
  staleResultFixtures?: number;

  // ── Aşama sunumu ────────────────────────────────────────────────────────
  /** Maçın turnuva aşaması: "DomesticLeague" | "Qualifying" | "LeaguePhase" | ... */
  matchPhase?: string;
  /**
   * Tablo gösterilebilir mi ve nasıl. Bu kararı BACKEND verir; ekran yeniden
   * yorumlamaz — aksi hâlde ikisi farklı şey söyleyebilir.
   */
  standingsAvailability?: "Table" | "LeaguePhaseTable" | "NotApplicable" | "NotAvailable" | "Unresolved";
  /** Başlık (ör. "Lig Aşaması Puan Durumu"). */
  standingsTitle?: string;
  /** Tablo yoksa kullanıcıya gösterilecek nötr açıklama. */
  standingsNotice?: string;
  /** Teşhis kodu — KULLANICIYA GÖSTERİLMEZ. */
  diagnostic?: string;
  completenessCheckedAtUtc?: string;
  homeTeamPeek?: TeamStandingDto;
  awayTeamPeek?: TeamStandingDto;
  /** Birincil tablonun TAM satır listesi (eski ad korundu; artık kırpılmaz). */
  tableSlice: TeamStandingDto[];
  /**
   * Gösterilecek tablolar. Ulusal lig maçında tek tablo; Avrupa kupası maçında
   * takımların kendi ulusal lig tabloları (ör. Süper Lig + Ligue 1).
   */
  tables?: StandingTableDto[];
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

// ── CANLI TAKİP (GET /api/matches/{id}/livefeed) ──────────────────────────────
// Global kaynaklı canlı akış. AI Maç Analizi'nden BAĞIMSIZ ayrı uçtur.

/** "Unknown" = maç saati geçti ama canlı olduğu global kaynaklardan doğrulanamadı. */
export type MatchLiveState = "NotStarted" | "Live" | "Finished" | "Unknown";

export interface MatchLiveScoreDto {
  homeScore: number;
  awayScore: number;
  phase: string | null;
  /** "record" = kayıtlı kesin sonuç, "global" = global kaynağın yazdığı canlı skor. */
  origin: string;
  updatedAt: string;
}

/** Kanonik canlı maç olayı. Canlı Takip VİDEO İÇERMEZ (o ayrı özellik). */
export interface MatchLiveEventItemDto {
  id: string;
  /** GOAL, RED_CARD, HALF_TIME … */
  eventType: string;
  /** Türkçe ekran etiketi ("GOL", "KIRMIZI KART"). */
  label: string;
  /** YALNIZ kaynak metninde açıkça yazıyorsa dolu; hesaplanmaz. */
  minute: number | null;
  minuteLabel: string | null;
  team: string | null;
  player: string | null;
  /** Kaynağın kendi metni — FORMAX cümle üretmez. */
  description: string;
  source: string;
  sourceUrl: string;
  publishedAt: string;
  /** "news" | "social" */
  origin: string;
}

export interface MatchLiveFeedDto {
  matchId: number;
  state: MatchLiveState;
  stateMessage: string | null;
  /** "CANLI" etiketi YALNIZ bu true iken gösterilir. */
  liveConfirmed: boolean;
  kickoffUtc: string;
  homeTeam: string;
  awayTeam: string;
  score: MatchLiveScoreDto | null;
  scoreIsFinal: boolean;
  scoreUnavailableReason: string | null;
  /** Kayıtlı skor maçın tamamını kapsamıyorsa (uzatma/penaltı) dürüst açıklama. */
  scoreNote: string | null;
  /** Penaltı seri sonucu — yalnız seri oynandıysa dolu. */
  shootoutHome: number | null;
  shootoutAway: number | null;
  /** Ters kronolojik: en yeni en üstte. */
  events: MatchLiveEventItemDto[];
}

// ── ÖNEMLİ ANLAR (GET /api/matches/{id}/highlights) ───────────────────────────

export type MatchHighlightsStatus = "NotStartedYet" | "Ready" | "NoContent";

export interface MatchHighlightMomentDto {
  minute: number;
  /** Gösterim etiketi; penaltı atışları için "PEN". */
  minuteLabel: string;
  type: string;
  label: string;
  /** Sağlayıcı alanlarından kurulu kısa açıklama; çıkarım içermez. */
  description: string | null;
  team: string | null;
  player: string | null;
  /** Bu ana bağlanmış doğrulanmış videonun id'si; yoksa null. */
  videoId: string | null;
}

export interface MatchHighlightVideoDto {
  id: string;
  title: string;
  minute: number | null;
  platform: string;
  source: string;
  url: string;
  /** Platformun izin verdiği embed adresi; embed edilemiyorsa null. */
  embedUrl: string | null;
  thumbnailUrl: string | null;
  embeddable: boolean;
  publishedUtc: string;
}

export interface MatchHighlightsDto {
  matchId: number;
  state: MatchLiveState;
  status: MatchHighlightsStatus;
  homeTeam: string;
  awayTeam: string;
  moments: MatchHighlightMomentDto[];
  videos: MatchHighlightVideoDto[];
}

export interface NabizFeedItemDto {
  /** Haberin kimliği (backend ContentHash). Detay seçimi bunu kullanır. */
  id?: string;
  type: string;
  source: string;
  author: string;
  authorVerified: boolean;
  /** Dil seçiliyse çevrilmiş başlık, değilse sağlayıcının orijinal başlığı. */
  headline: string;
  summary?: string;
  imageUrl?: string;
  sourceUrl?: string;
  publishedAt: string;
  /** Haberin kendi dili (ISO-639-1). */
  language?: string;
  /** true ise headline/summary çevrilmiştir ve original* alanları doludur. */
  isTranslated?: boolean;
  originalHeadline?: string;
  originalSummary?: string;
}

export interface NabizSectionDto {
  items: NabizFeedItemDto[];
}

// ── Match Intelligence anlatısı (Gemma) ───────────────────────────────────────
// Mirrors Formax.Application.DTOs.Matches.RadarNarrativeDto
//
// TEK KAYNAK: GET /api/matches/{id}/detail → aiNarrative.
// Backend'in Match Intelligence + IntelligencePack + Gemma zinciri bu metinleri
// ÜRETİR; frontend yalnız gösterir. Burada hiçbir alan türetilmez, birleştirilmez,
// yeniden yazılmaz. Boş alan = o blok hiç render edilmez.

/** Senaryo gerekçesi — market adı backend'in canonical değeridir, DEĞİŞTİRİLMEZ. */
export interface RadarScenarioReasonDto {
  market: string;
  reason: string;
}

export interface RadarNarrativeDto {
  // Keşfet yüzeyi
  radarSummary: string;
  highlights: string[];

  // Maç Detayı yüzeyi
  matchReport: string;
  whyThisMatch: string;
  reasoningSummary: string;
  newsSummary: string;
  socialSummary: string;
  statisticalSummary: string;
  keyInsights: string[];
  scenarios: RadarScenarioReasonDto[];
  evidenceSummary: string;

  // AI İncele yüzeyi
  aiIncele: string;

  /** Reasoning Layer'ın güven skoru (0–100) — backend üretir. */
  reasoningConfidence: number;
  /** true = Gemma üretti · false = deterministik fallback. */
  isAiGenerated: boolean;
}

/** Maç skor kırılımı — İY / 2Y / MS. 2Y BACKEND'de hesaplanır; ekran çıkarma yapmaz. */
export interface MatchScoreBreakdownDto {
  halfTime?: { home: number; away: number } | null;
  secondHalf?: { home: number; away: number } | null;
  fullTime?: { home: number; away: number } | null;
  extraTime?: { home: number; away: number } | null;
  penalties?: { home: number; away: number } | null;
}

/** Önemli anlar satırı — kaynakta olmayan olay üretilmez. */
export interface MatchEventDto {
  minute: number;
  extraMinute?: number | null;
  team?: string | null;
  player?: string | null;
  assist?: string | null;
  /** Sağlayıcının HAM türü — yalnız teşhis; kullanıcıya `label` gösterilir. */
  eventType: string;
  /** Sağlayıcının HAM açıklaması — yalnız teşhis. */
  detail?: string | null;
  /** Kullanıcıya gösterilecek Türkçe etiket — backend'in deterministik eşlemesi. */
  label?: string;
  /** "Goal" | "OwnGoal" | "PenaltyGoal" | "MissedPenalty" | "Substitution" | "YellowCard" | "SecondYellow" | "RedCard" | "Var" | "Other" */
  kind?: string;
  /** Oyuncu değişikliğinde oyuna giren / çıkan. */
  playerIn?: string | null;
  playerOut?: string | null;
}

/**
 * RESMÎ ÖZET ARAMASI — kalıcı defterden. "Found" | "Checking" | "NotFound".
 * Ekran "bulunamadı" kararını YALNIZ bundan verir; saatten türetmez.
 */
export interface VideoSearchDto {
  status: "Found" | "Checking" | "NotFound" | string;
  attemptsMade: number;
  maxAttempts: number;
  lastAttemptUtc?: string | null;
}

/**
 * Maç videosu. canPlayInApp false ise uygulama içi oynatıcı AÇILMAZ ve backend
 * embedUrl'i null gönderir — ekranın deneyebileceği bir adres bırakılmaz.
 *
 * NOT: bitmiş maç ekranında MAÇ SONRASI HABER YOKTUR (02.09.2026 ürün kararı).
 * Bu yüzden haber kartı için bir tip de tanımlı değildir; geri gelmesi isteniyorsa
 * önce ürün kararının değişmesi gerekir.
 */
export interface MatchVideoDto {
  title: string;
  /** Resmî yayıncı adı ("UEFA", "TRT SPOR"…). */
  publisher: string;
  /** Kaynağın kendi sayfası. */
  sourcePageUrl: string;
  embedUrl?: string | null;
  thumbnailUrl?: string | null;
  videoType:
    | 'MatchHighlights'
    | 'ExtendedHighlights'
    | 'Goal'
    | 'Penalty'
    | 'RedCard'
    | 'VAR'
    | 'ImportantMoment'
    | string;
  publishedAtUtc?: string | null;
  durationSeconds?: number | null;
  canPlayInApp: boolean;
  /** Videonun açık olduğu ülkeler (ISO alpha-2). Boş = kaynak söylemedi. */
  availableCountries?: string[];
  /** true ise video yalnız belirli ülkelerde oynar; ekran bunu açıkça söyler. */
  isRegionRestricted?: boolean;
  /** Olay klibi meta verisi — yalnız AYRI kliplerde dolu. */
  eventMinute?: number | null;
  eventExtraMinute?: number | null;
  eventPlayer?: string | null;
  eventTeam?: string | null;
}

/** Tek istatistik satırı — ev/deplasman karşılaştırması. */
export interface MatchStatisticRowDto {
  key: string;
  label: string;
  home: number;
  away: number;
  isPercentage: boolean;
}

/**
 * Maç istatistikleri. Backend YALNIZ gerçek veri varsa gönderir; bütün alanları
 * sıfır olan bir satır "0 şut" değil VERİ YOK demektir ve null gelir.
 */
export interface MatchStatisticsDto {
  rows: MatchStatisticRowDto[];
}

export interface MatchDetailDto {
  videos?: MatchVideoDto[];
  /** Resmî özet aramasının kalıcı defterdeki durumu (yalnız bitmiş maçta). */
  videoSearch?: VideoSearchDto | null;
  scoreBreakdown?: MatchScoreBreakdownDto | null;
  events?: MatchEventDto[];
  /** Yalnız gerçek veri varsa dolu; aksi hâlde null ve bölüm hiç render edilmez. */
  statistics?: MatchStatisticsDto | null;
  matchId: number;
  homeTeam: TeamSummaryDto;
  awayTeam: TeamSummaryDto;
  matchDate: string;
  status: string;
  league: string;
  /** Sağlayıcının HAM tur adı ("3rd Qualifying Round", "Regular Season - 1"). */
  round?: string;
  /**
   * MAÇ TÜRÜ — backend'in ham tur adından türettiği Türkçe etiket ("Eleme Turu",
   * "Son 16 Turu", "Lig Maçı · 1. Hafta", "Final"). Backend üretemediyse null gelir ve
   * UI tür satırını HİÇ göstermez — frontend maç türü TAHMİN ETMEZ.
   */
  matchTypeLabel?: string | null;
  referee?: string;
  venue?: string;
  weather?: string;
  watchersCount: number;
  homeTeamLastMatches: LastMatchDto[];
  awayTeamLastMatches: LastMatchDto[];
  /**
   * Form listesinin süzüldüğü ligin gerçek adı. null = takımın ligi çözülemedi →
   * liste süzülmedi, başlıkta "ligde" DENMEZ.
   */
  homeTeamFormLeague?: string | null;

  /**
   * MEVCUT SEZON LİG FORMU (backend TeamSeasonFormDto). Kapsam: aynı lig + bu sezon +
   * maç saatinden önce + tamamlanmış maçlar. Önceki sezon, hazırlık, kupa ve Avrupa
   * maçları BU ÖZETE GİRMEZ. `sentence` backend in yazdığı deterministik cümledir;
   * frontend cümle KURMAZ, sayı HESAPLAMAZ.
   */
  homeSeasonForm?: TeamSeasonFormDto | null;
  awaySeasonForm?: TeamSeasonFormDto | null;
  awayTeamFormLeague?: string | null;
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
  /** Backend her zaman gönderir; alan hiç gelmezse UI bunu "haber yok" diye MASKELEMEZ. */
  nabizFeed?: NabizSectionDto;

  /**
   * Match Intelligence anlatısı — Keşfet, Maç Detayı ve AI İncele yüzeylerinin
   * ORTAK kaynağı. Backend üretmediyse null gelir; UI o zaman anlatı bölümünü
   * hiç göstermez (uydurma metin yok).
   */
  aiNarrative?: RadarNarrativeDto | null;
}

// ── Follow: MatchListItemDto ──────────────────────────────────────────────────
// Mirrors Formax.Application.DTOs.Matches.MatchListItemDto

export interface FollowedMatchDto {
  matchId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamLogoUrl?: string | null;
  awayTeamLogoUrl?: string | null;
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
