import apiClient from "./client";

/**
 * "SENİN SEÇİMİN" — kullanıcının olası sonuç seçimleri.
 *
 * FRONTEND OLASILIK ÜRETMEZ: yüzde ve oran, backend'in döndürdüğü "Olası Sonuçlar"
 * listesinden olduğu gibi taşınır. Backend market etiketini bilinen anahtarlara
 * çözemezse isteği reddeder — arayüz uydurma bir market kaydedemez.
 *
 * KALICILIK BACKEND'DEDİR: localStorage kullanılmaz. Seçim hesaba bağlıdır ve
 * sayfa yenilendiğinde, hatta başka bir cihazda bile aynı görünür.
 */

export interface UserPickDto {
  id: string;
  matchId: number;
  /** Normalize market anahtarı ("ALT_2_5") — satır eşlemesi bununla yapılır. */
  marketKey: string;
  /** Çakışma grubu ("TOTAL_2_5"); grupsuz markette null. */
  marketGroup?: string | null;
  label: string;
  /** SEÇİM ANINDAKİ olasılık — model sonradan değişse de bu sayı sabittir. */
  probabilityPercent: number;
  odd?: number | null;
  /** "Active" | "Pending" | "Settled" | "Unsettleable" */
  selectionStatus: string;
  createdAtUtc: string;
  matchKickoffUtc?: string | null;
  /** true = doğru, false = yanlış, null = hesaplanamadı (uydurma settlement YOK). */
  isCorrect?: boolean | null;
  settlementNote?: string | null;
}

export interface UserPredictionCardDto {
  matchId: number;
  homeTeam: string;
  awayTeam: string;
  homeTeamLogoUrl?: string | null;
  awayTeamLogoUrl?: string | null;
  league: string;
  matchDateUtc: string;
  status: string;
  homeScore?: number | null;
  awayScore?: number | null;
  halfTimeHomeScore?: number | null;
  halfTimeAwayScore?: number | null;
  /** 2. yarı = MS − İY; backend hesaplar, yoksa null. */
  secondHalfHomeScore?: number | null;
  secondHalfAwayScore?: number | null;
  cardStatus: string;
  selections: UserPickDto[];
}

export interface PickToggleRequest {
  matchId: number;
  /** Kullanıcıya gösterilen market etiketi ("2.5 Alt"). */
  marketLabel: string;
  probabilityPercent: number;
  odd?: number | null;
  modelVersions?: string | null;
  modelFingerprint?: string | null;
}

export interface PickToggleResponse {
  accepted: boolean;
  /** MATCH_NOT_FOUND | UNKNOWN_MARKET | MATCH_ALREADY_STARTED */
  reason?: string | null;
  selections: UserPickDto[];
}

/** Seçimi ekler veya (zaten seçiliyse) kaldırır. */
export async function togglePick(request: PickToggleRequest): Promise<PickToggleResponse> {
  const res = await apiClient.post<PickToggleResponse>("/api/picks/toggle", request);
  return res.data;
}

/** Bir maçtaki mevcut seçimler — sayfa yenilendiğinde durum buradan geri gelir. */
export async function getPicksForMatch(matchId: number): Promise<UserPickDto[]> {
  const res = await apiClient.get<{ matchId: number; selections: UserPickDto[] }>(
    `/api/picks/match/${matchId}`
  );
  return res.data.selections ?? [];
}

/** TAHMİNLERİM — kullanıcının bütün seçimleri, maç kartları hâlinde. */
export async function getMyPredictions(): Promise<UserPredictionCardDto[]> {
  const res = await apiClient.get<{ count: number; cards: UserPredictionCardDto[] }>(
    "/api/picks/me"
  );
  return res.data.cards ?? [];
}
