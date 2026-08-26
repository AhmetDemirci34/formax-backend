import apiClient from "./client";

/** Backend MatchListItemDto — GET /api/matches (gerçek veri, mock yok). */
export interface MatchListItemDto {
  matchId: number;
  homeTeam: string;
  awayTeam: string;
  league: string;
  startTime: string;
  /** Scheduled | Live | Finished (backend türetir). */
  status: string;
  /** Canlı dakika — yalnız canlı maçta doludur (MatchLiveStats). */
  minute: number | null;
  score: { home: number; away: number } | null;
}

/** Maç listesi — Maçlar ekranının tek kaynağı. */
export async function getMatchList(): Promise<MatchListItemDto[]> {
  const res = await apiClient.get<MatchListItemDto[]>("/api/matches");
  return res.data;
}
