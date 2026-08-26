import apiClient from "./client";

/** Mirrors Formax.Application.DTOs.Leagues.LeagueDto exactly. */
export interface LeagueDto {
  id: number;
  name: string;
  /** Projede lig logosu kaynağı yok → backend null döner. */
  logoUrl: string | null;
}

/** MEVCUT uç: GET /api/users/me/leagues (UsersLeaguesController). */
export async function getMyLeagues(): Promise<LeagueDto[]> {
  const res = await apiClient.get<LeagueDto[]>("/api/users/me/leagues");
  return res.data;
}
