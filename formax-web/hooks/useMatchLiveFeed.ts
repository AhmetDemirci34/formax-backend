import { useQuery } from "@tanstack/react-query";
import { getMatchLiveFeed } from "@/lib/api/matches";
import type { MatchLiveFeedDto } from "@/types/api";

/**
 * CANLI TAKİP akışı.
 *
 * Yenileme YALNIZ maç oynanırken yapılır: backend "Live" derse 30 sn'de bir yenilenir,
 * "NotStarted"/"Finished" durumunda hiç yoklanmaz (bitmiş maçın akışı değişmez).
 * Bu hook AI Maç Analizi sorgusuna DOKUNMAZ — ayrı queryKey, ayrı uç.
 */
export function useMatchLiveFeed(matchId: number, enabled = true) {
  return useQuery<MatchLiveFeedDto>({
    queryKey: ["match-livefeed", matchId],
    queryFn: () => getMatchLiveFeed(matchId),
    staleTime: 15_000,
    refetchInterval: (query) =>
      query.state.data?.state === "Live" ? 30_000 : false,
    enabled: enabled && matchId > 0,
  });
}
