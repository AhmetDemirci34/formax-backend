import apiClient from "./client";
import type { LiveSectionDto, LiveEventDto } from "@/types/api";

export async function getLiveScreen(matchId: number): Promise<unknown> {
  const res = await apiClient.get(`/api/live/${matchId}/screen`);
  return res.data;
}

export async function getLiveEvents(matchId: number): Promise<LiveEventDto[]> {
  const res = await apiClient.get<LiveEventDto[]>(
    `/api/live/${matchId}/events`
  );
  return res.data;
}

export async function getLiveAiAnalysis(matchId: number): Promise<unknown> {
  const res = await apiClient.get(`/api/live/${matchId}/ai-analysis`);
  return res.data;
}
