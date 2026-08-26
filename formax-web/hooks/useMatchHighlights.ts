import { useQuery } from "@tanstack/react-query";
import { getMatchHighlights } from "@/lib/api/matches";
import type { MatchHighlightsDto } from "@/types/api";

/**
 * ÖNEMLİ ANLAR — panel açıldığında çekilir (enabled ile tetiklenir).
 * AI Maç Analizi sorgusundan tamamen ayrıdır.
 */
export function useMatchHighlights(matchId: number, enabled: boolean) {
  return useQuery<MatchHighlightsDto>({
    queryKey: ["match-highlights", matchId],
    queryFn: () => getMatchHighlights(matchId),
    staleTime: 60_000,
    enabled: enabled && matchId > 0,
  });
}
