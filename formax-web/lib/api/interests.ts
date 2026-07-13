import apiClient from "./client";

export type InterestEventType = "click" | "view" | "open";

/**
 * R.14.8 — Mevcut /api/interests/track endpoint'ine ince istemci sarmalı.
 * Learning pipeline'ı besler (UserInterestScore → UserInterestProfile → affinity → RadarScore).
 *
 * Fire-and-forget: UX'i bloklamaz, hataları yutar.
 * Anonim guard: token yoksa hiç istek atmaz → 401 → login redirect tetiklenmez.
 */
export function trackInterest(eventType: InterestEventType, matchId: number): void {
  if (typeof window === "undefined") return;
  if (!localStorage.getItem("formax_token")) return; // anonim: track yazma
  if (!matchId || matchId <= 0) return;

  apiClient
    .post("/api/interests/track", { eventType, matchId })
    .catch(() => {/* fire & forget */});
}
