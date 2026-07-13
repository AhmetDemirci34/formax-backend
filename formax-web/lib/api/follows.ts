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
