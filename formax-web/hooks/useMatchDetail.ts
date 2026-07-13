import { useQuery } from "@tanstack/react-query";
import { getMatchDetail } from "@/lib/api/matches";
import type { MatchDetailDto } from "@/types/api";

export function useMatchDetail(matchId: number) {
  return useQuery<MatchDetailDto>({
    queryKey: ["match", matchId],
    queryFn: () => getMatchDetail(matchId),
    staleTime: 30_000,
    // Drive polling off the fetched status: only a "Live" match auto-refreshes.
    // Starts when the match goes live and stops once it finishes.
    refetchInterval: (query) =>
      query.state.data?.status === "Live" ? 30_000 : false,
    enabled: matchId > 0,
  });
}
