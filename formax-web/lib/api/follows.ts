import apiClient from "./client";
import type { FollowedMatchDto } from "@/types/api";

export async function followMatch(matchId: number): Promise<void> {
  await apiClient.post(`/api/follows/${matchId}`);
}

export async function unfollowMatch(matchId: number): Promise<void> {
  await apiClient.delete(`/api/follows/${matchId}`);
}

export async function getFollowedMatches(): Promise<FollowedMatchDto[]> {
  const res = await apiClient.get<FollowedMatchDto[]>("/api/follows/me");
  return res.data;
}

export async function getFollowedMatchIds(): Promise<number[]> {
  const res = await apiClient.get<number[]>("/api/follows/me/ids");
  return res.data;
}

/** Takip kartı — backend'in "Takip Ettiğim Maçlar" sözleşmesi. */
export interface FollowedMatchCardDto {
  matchId: number;
  leagueId: number;
  league: string;
  /** Kickoff (UTC). Geri sayım bundan hesaplanır. */
  matchDateUtc: string;
  homeTeam: string;
  awayTeam: string;
  homeTeamLogoUrl?: string | null;
  awayTeamLogoUrl?: string | null;
  /** DEPODAKİ gerçek durum — saatten türetilmez. */
  status: string;
  /** Yalnız bitmiş maçta dolu; aksi hâlde null (0-0 uydurulmaz). */
  homeScore?: number | null;
  awayScore?: number | null;
  halfTimeHomeScore?: number | null;
  halfTimeAwayScore?: number | null;
}

export interface FollowedMatchesScreen {
  upcoming: FollowedMatchCardDto[];
  finished: FollowedMatchCardDto[];
  totalCount: number;
}

/**
 * "TAKİP ETTİĞİM MAÇLAR" — ekranın TEK veri kaynağı.
 *
 * Yalnız kullanıcının kendi MAÇ takiplerini döndürür: takım kartı, lig kartı,
 * istatistik kutusu ve gelişme akışı bu yanıtta YOKTUR. Sıra backend'de
 * belirlenir (yaklaşan artan, tamamlanan azalan); arayüz onu korur.
 */
export async function getFollowedMatchesScreen(): Promise<FollowedMatchesScreen> {
  const res = await apiClient.get<FollowedMatchesScreen>("/api/follows/me/matches");
  return res.data;
}
