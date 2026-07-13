import apiClient from "./client";
import type { MatchDetailDto } from "@/types/api";

export async function getMatchDetail(matchId: number): Promise<MatchDetailDto> {
  const res = await apiClient.get<MatchDetailDto>(
    `/api/matches/${matchId}/detail`
  );
  return res.data;
}
