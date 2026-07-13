import { useInfiniteQuery } from "@tanstack/react-query";
import { getRecommendations } from "@/lib/api/home";
import type { RecommendationCardDto } from "@/types/api";

const PAGE_SIZE = 10;

export const RECOMMENDATIONS_KEY = ["recommendations"] as const;

export function useRecommendations() {
  return useInfiniteQuery<RecommendationCardDto[]>({
    queryKey: RECOMMENDATIONS_KEY,
    queryFn: ({ pageParam }) =>
      getRecommendations(pageParam as number, PAGE_SIZE),
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) =>
      lastPage.length === PAGE_SIZE ? allPages.length + 1 : undefined,
    staleTime: 5 * 60 * 1000,  // 5 min
  });
}
