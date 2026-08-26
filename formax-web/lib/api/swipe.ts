import apiClient from "./client";
import type { SwipeRequest } from "@/types/api";

/**
 * Swipe aksiyonu → /api/swipe (UserActions).
 *
 * Anonim guard: token yoksa istek ATILMAZ. Login artık zorunlu (HomeGate); token'sız
 * swipe backend'de userId=1'e yazılıyordu (öğrenme kirlenmesi). trackInterest ile aynı
 * guard → gereksiz anonim UserActions yazımı kesilir.
 */
export async function postSwipe(req: SwipeRequest): Promise<void> {
  if (typeof window !== "undefined" && !localStorage.getItem("formax_token")) return;
  await apiClient.post("/api/swipe", req);
}
