/**
 * FORMAX — Decision paketi (TEK AI kaynağı).
 *
 * Kaynak: GET /api/matches/{id}/decision  →  AiDecisionPackage.
 *
 * KİLİTLİ KARAR: UI yalnızca `reading` düğümü üzerinden render eder.
 * `editorial` ve `story` düğümleri `reading`'in projeksiyonlarıdır (aynı diziler,
 * aynı cümleler) — ikinci kez gösterilmezler, bu yüzden burada TİPLENMEZLER.
 *
 * Frontend bu veriden metin ÜRETMEZ, cümle KURMAZ, skor HESAPLAMAZ.
 * Boş alan = blok tamamen gizlenir (placeholder yok).
 */

/** Takım okuması — backend takım adını ve madde madde tespitleri verir. */
export interface ReadingTeamDto {
  teamName: string;
  hasData: boolean;
  points: string[];
}

/** FORMAX'ın kapanış hükmü. */
export interface ReadingVerdictDto {
  hasData: boolean;
  criticalTopic: string;
  whyWatch: string;
  biggestAdvantage: string;
  biggestRisk: string;
  whatCouldChange: string;
}

/**
 * MatchReadingEngine çıktısı — 9 analiz bloğunun tamamı buradadır.
 * Tüm dizi alanları backend'de boş dizi olarak gelebilir (veri yoksa).
 */
export interface MatchReadingDto {
  hasData: boolean;
  coverageDepth: number;

  // ── Blok kaynakları ──
  context: string[];
  homeTeam: ReadingTeamDto | null;
  awayTeam: ReadingTeamDto | null;
  squad: string[];
  fixture: string[];
  coach: string[];
  keyPlayers: string[];
  news: string[];
  transfers: string[];
  tactical: string[];
  psychology: string[];
  hidden: string[];
  synthesis: string[];

  // ── Hikâye alanları (tekil cümleler) ──
  mainStory: string;
  subStory: string;
  turningPoint: string;
  biggestAdvantage: string;
  biggestRisk: string;
  surprisePotential: string;
  formaxView: string;
  verdict: ReadingVerdictDto | null;

  // ── Anlatı dizileri (uzun okuma sunumu için) ──
  live: string[];
  matchStoryLines: string[];
  teamStory: string[];
  playerStory: string[];
  squadStory: string[];
  tacticalStory: string[];
  timelineStory: string[];
  competitionStory: string[];
  newsStory: string[];
  psychologyStory: string[];
  hiddenStory: string[];
  formaxOpinion: string[];
}

/** Karar paketi endeksi (kullanıcıya gösterilmez; kullanıcı göstergesi snapshot kaynaklı AI Beklentisi) — backend üretir, frontend hesaplamaz. */
export interface DecisionConfidenceDto {
  score: number;
  level: string;
  basis: string;
}

/** Olasılık kalemi — senaryo motoru çıktısı. */
export interface DecisionProbabilityDto {
  market: string;
  probability: number;
  confidence: string;
  reason: string;
  family: string;
  /**
   * GERÇEK market oranı (backend MatchMarketOdds → sağlayıcı verisi).
   * Sağlayıcıda karşılığı olmayan markette null — UI oran göstermez, ÜRETMEZ.
   */
  odd?: number | null;
  previousOdd?: number | null;
}

/** Decision paketinin UI'ın okuduğu yüzeyi. */
export interface MatchDecisionDto {
  matchId: number;
  homeName: string;
  awayName: string;
  version: string;

  reading: MatchReadingDto | null;
  confidence: DecisionConfidenceDto | null;
  probabilities: DecisionProbabilityDto[];

  criticalFactors: string[];
  decisionDrivers: string[];
  unknownFactors: string[];

  decisionQualityScore: number;
}
