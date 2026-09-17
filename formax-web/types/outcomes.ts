/**
 * AI OLASI SONUÇLAR — backend snapshot sözleşmesi (GET /api/matches/{id}/outcomes).
 * Keşfet ve Maç Detayı AYNI yükü okur; frontend yüzde HESAPLAMAZ, market SIRALAMAZ.
 */
export type OutcomeFamily = "MatchResult" | "TotalGoals" | "BothTeamsScore" | "Other" | string;

export interface OutcomeCandidateDto {
  family: OutcomeFamily;
  familyTitle: string;
  market: string;
  /** Seçim anahtarı; seçilemeyen market için null. */
  marketKey: string | null;
  /** Gösterilecek kalibre yüzde (aile içinde tutarlı yuvarlanmış). */
  probability: number;
  rawProbability: number;
  calibratedProbability: number;
  baselineProbability: number;
  informationLift: number;
  evidenceCoverage: number;
  sampleQuality: string;
  uncertainty: number;
  selectionScore: number;
  reasonCodes: string[];
  reason?: string | null;
  limitation?: string | null;
}

export interface OutcomeFamilyDto {
  family: OutcomeFamily;
  title: string;
  items: OutcomeCandidateDto[];
}

export interface OutcomeSnapshotDto {
  snapshotId: string | null;
  matchId: number;
  modelVersion: string;
  calibrationRunId?: string | null;
  computedAtUtc?: string | null;
  inputsCutoffUtc?: string | null;
  /** "Available" | "NotEligible" | "InsufficientData" | "Pending" */
  status: string;
  /** "Enabled" | "Limited" | "Disabled" — yüzdeler yalnız Enabled'da gelir. */
  predictionEligibility?: string | null;
  eligibilityReasons?: string[];
  triggerType?: string | null;
  previousSnapshotId?: string | null;
  expectedHomeGoals?: number | null;
  expectedAwayGoals?: number | null;
  evidenceCoverage: number;
  sampleQuality: string;
  homeSampleSize: number;
  awaySampleSize: number;
  limitation?: string | null;
  mainCards: OutcomeCandidateDto[];
  families: OutcomeFamilyDto[];
  topScores: { home: number; away: number; probability: number }[];
  reasonCodes: string[];
  notice?: string | null;
}
