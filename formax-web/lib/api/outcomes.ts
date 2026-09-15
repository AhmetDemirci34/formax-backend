import apiClient from "./client";
import type { OutcomeSnapshotDto } from "@/types/outcomes";

/**
 * AI OLASI SONUÇLAR — GET /api/matches/{id}/outcomes. Arka planda üretilmiş snapshot'ı okur;
 * sayfa açılışı olasılık hesaplatmaz. Keşfet ve Maç Detayı bu tek çağrıyı paylaşır.
 */
export async function getMatchOutcomes(matchId: number): Promise<OutcomeSnapshotDto> {
  const res = await apiClient.get<OutcomeSnapshotDto>(`/api/matches/${matchId}/outcomes`);
  return res.data;
}
