// FORMAX · Tahmin domain modeli — KAYNAK-AGNOSTİK.
// Bugün localStorage, yarın backend API — model değişmez. Backend PredictionDto
// alanlarıyla (id, match_status, home_team, away_team, market_name, odds ...) uyumludur.

// Geleceğe hazır durum kümesi. Bugün yalnız active/pending kullanılsa da model
// tümünü destekler; backend geldiğinde enum değişmez.
export type PredictionStatus = "active" | "pending" | "won" | "lost" | "void" | "cancelled";

export interface PredictionTeam {
  name: string;
  logoUrl?: string | null;
}

/**
 * Genel tahmin modeli. Herhangi bir kaynak (AI Kombini, Keşfet, Maç Detay,
 * gelecekteki özellikler) bu modeli üretir; UI yalnızca bunu tüketir.
 */
export interface Prediction {
  predictionId: string;
  matchId: number;
  market: string; // "2.5 Üst", "KG Var"
  selection: string; // "Üst", "Var" — market'in seçilen tarafı
  odds: number | null; // gerçek oran; backend verince dolar (uydurulmaz)
  status: PredictionStatus;
  createdAt: string; // ISO
  // Opsiyonel snapshot — kaynak sağlarsa UI fallback'i (backend DTO'da da bulunur)
  homeTeam?: PredictionTeam;
  awayTeam?: PredictionTeam;
  league?: string;
}

/** Yeni tahmin ekleme girdisi — kaynaktan bağımsız. */
export interface PredictionInput {
  matchId: number;
  market: string;
  selection?: string;
  odds?: number | null;
  homeTeam?: PredictionTeam;
  awayTeam?: PredictionTeam;
  league?: string;
  createdAt?: string;
}
