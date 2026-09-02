"use client";

import { useQuery } from "@tanstack/react-query";
import {
  getMatchResultDays,
  getMatchResults,
  type MatchResultDayDto,
  type MatchResultItemDto,
} from "@/lib/api/matchResults";
import { RESULT_DAY_SPAN } from "@/lib/matches/resultDays";

/**
 * SONUÇLAR sekmesinin veri kancaları.
 *
 * İkisi de salt DB okur; sağlayıcıya çıkmaz. Sonuçlar geçmişe aittir ve DEĞİŞMEZ:
 * bu yüzden otomatik tazeleme yoktur ve önbellek uzun tutulur — sekme değiştirmek
 * ya da güne geri dönmek yeni istek üretmez.
 */

const FINISHED_DATA_IS_IMMUTABLE = 10 * 60_000;

export function useMatchResultDays(enabled: boolean) {
  return useQuery<MatchResultDayDto[]>({
    queryKey: ["match-result-days", RESULT_DAY_SPAN],
    queryFn: () => getMatchResultDays(RESULT_DAY_SPAN),
    staleTime: FINISHED_DATA_IS_IMMUTABLE,
    refetchInterval: false,
    enabled,
  });
}

export function useMatchResults(day: string | null) {
  return useQuery<MatchResultItemDto[]>({
    queryKey: ["match-results", day],
    queryFn: () => getMatchResults(day!),
    staleTime: FINISHED_DATA_IS_IMMUTABLE,
    refetchInterval: false,
    enabled: !!day,
  });
}
