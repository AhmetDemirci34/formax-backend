import apiClient from "./client";
import type { RecommendationCardDto } from "@/types/api";

export async function getRecommendations(
  page = 1,
  pageSize = 10
): Promise<RecommendationCardDto[]> {
  const res = await apiClient.get<RecommendationCardDto[]>(
    `/api/home/recommendations?page=${page}&pageSize=${pageSize}`
  );
  return res.data;
}
