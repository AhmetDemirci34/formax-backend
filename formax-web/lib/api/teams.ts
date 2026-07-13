import apiClient from "./client";
import type { TeamDto } from "@/types/api";

export async function getAllTeams(): Promise<TeamDto[]> {
  const res = await apiClient.get<TeamDto[]>("/api/teams");
  return res.data;
}

export async function getMyTeams(): Promise<TeamDto[]> {
  const res = await apiClient.get<TeamDto[]>("/api/users/me/teams");
  return res.data;
}

export async function setMyTeams(teamIds: number[]): Promise<void> {
  await apiClient.post("/api/users/me/teams", { teamIds });
}
