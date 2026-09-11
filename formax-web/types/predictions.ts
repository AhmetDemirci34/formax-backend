import type { MatchScoreBreakdownDto } from '@/types/api';

// FORMAX · Tahminlerim ekranı UI tipleri.
// Kaynak: localStorage `formax_predictions` (SavedPrediction) + gerçek maç
// durumu `GET /api/matches/{id}/detail` ile zenginleştirilir. Mock/fake yok.

export type PredictionTab = "active" | "pending" | "all";

export type UiPredictionStatus = "live" | "upcoming" | "finished" | "unknown";

export interface UiTeam {
  name: string;
  logoUrl?: string | null;
}

export interface UiPrediction {
  id: string; // predictionId
  matchId: number;
  home: UiTeam;
  away: UiTeam;
  league: string;
  market: string; // "KG Var", "2.5 Üst" ...
  selection: string; // seçilen taraf
  odds: number | null; // gerçek oran; backend verince dolar (uydurulmaz)
  status: UiPredictionStatus; // maç durumu (canlı/bekleyen/bitti) — /detail'den
  minute: number | null; // canlı dakika
  score: string | null; // "2-1" (canlı/bitmiş)
  kickoff: string | null; // ISO (başlamamış)
  createdAt: string; // tahminin oluşturulma zamanı
  /**
   * İY / 2Y / MS kırılımı — BACKEND'den gelir (secondHalf backend'de hesaplanır).
   * Eksik alan null kalır; ekran '—' gösterir, 0-0 UYDURMAZ.
   */
  scoreBreakdown?: MatchScoreBreakdownDto | null;
}

export interface PredictionCounts {
  active: number;
  pending: number;
  total: number;
}
