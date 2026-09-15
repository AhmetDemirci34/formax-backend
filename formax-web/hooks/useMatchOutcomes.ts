import { useQuery } from "@tanstack/react-query";
import { getMatchOutcomes } from "@/lib/api/outcomes";
import type { OutcomeSnapshotDto } from "@/types/outcomes";

/** Keşfet ve Maç Detayı'nın paylaştığı TEK cache anahtarı — aynı SnapshotId iki ekranda. */
export const matchOutcomesKey = (matchId: number) => ["outcomes", matchId] as const;

export function useMatchOutcomes(matchId: number, enabled = true) {
  return useQuery<OutcomeSnapshotDto>({
    queryKey: matchOutcomesKey(matchId),
    queryFn: () => getMatchOutcomes(matchId),
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000,
    enabled: enabled && matchId > 0,
  });
}
