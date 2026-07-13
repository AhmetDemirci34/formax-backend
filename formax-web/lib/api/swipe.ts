import apiClient from "./client";
import type { SwipeRequest } from "@/types/api";

export async function postSwipe(req: SwipeRequest): Promise<void> {
  await apiClient.post("/api/swipe", req);
}
