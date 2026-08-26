import apiClient from "./client";
import type { MatchDecisionDto } from "@/types/decision";

/**
 * FORMAX'ın TEK AI kaynağı — GET /api/matches/{id}/decision.
 *
 * Match Detail, Discover teaser'ı ve AI İncele bu tek çağrıyı paylaşır
 * (bkz. useMatchDecision — ortak react-query cache'i). Ekranlar ikinci
 * istek atmaz, ikinci analiz üretmez.
 */
export async function getMatchDecision(matchId: number): Promise<MatchDecisionDto> {
  const res = await apiClient.get<MatchDecisionDto>(
    `/api/matches/${matchId}/decision`
  );
  return res.data;
}
