import apiClient from "./client";
import type { RecommendationCardDto } from "@/types/api";

export async function getRecommendations(
  page = 1,
  pageSize = 10,
  /**
   * Hero/swipe kuyruğunun zaman ufku (bugün + N takvim günü). Backend'e HANGİ YÜZEY
   * olduğunu bildirir; süzme BACKEND'de aday üretiminde yapılır — burada tarih
   * karşılaştırması, filtreleme veya sıralama YOKTUR. Gönderilmezse backend geniş
   * evreni döner (Sana Özel / Günün AI Kombini / Trending böyle çağırır).
   */
  maxHorizonDays?: number
): Promise<RecommendationCardDto[]> {
  const horizon = maxHorizonDays != null ? `&maxHorizonDays=${maxHorizonDays}` : "";
  const res = await apiClient.get<RecommendationCardDto[]>(
    `/api/home/recommendations?page=${page}&pageSize=${pageSize}${horizon}`
  );
  return res.data;
}
