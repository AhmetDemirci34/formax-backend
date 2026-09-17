"use client";

import { useQuery } from "@tanstack/react-query";
import {
  getMatchResultDays,
  getMatchResults,
  type MatchResultDayDto,
  type MatchResultItemDto,
} from "@/lib/api/matchResults";
import { RESULT_DAY_SPAN, istanbulDay } from "@/lib/matches/resultDays";

/**
 * SONUÇLAR sekmesinin veri kancaları.
 *
 * İkisi de salt DB okur; sağlayıcıya/resmî kaynağa ÇIKMAZ (resmî sonuçları arka plan işi
 * DB'ye yazar). Geçmiş günler DEĞİŞMEZ: otomatik tazeleme yoktur ve önbellek uzun
 * tutulur. YALNIZ BUGÜN kontrollü tazelenir — gün içinde biten maçın resmî sonucu DB'ye
 * yazıldığında ekran kendiliğinden görsün diye; sekme gizliyken istek atılmaz.
 */

const FINISHED_DATA_IS_IMMUTABLE = 10 * 60_000;
// Bugünün listesi: resmî sonuç DB'ye yazıldıktan sonra açık ekranda ≤ 30 sn içinde görünsün diye 20 sn (salt DB).
export const TODAY_RESULTS_REFRESH_MS = 20_000;
/** Gün şeridi ("hangi günde kaç sonuç") daha seyrek tazelenir. */
export const RESULT_DAYS_REFRESH_MS = 5 * 60_000;

/** Tazeleme aralığı: yalnız bugün; geçmiş gün için false. */
export function resultsRefreshInterval(day: string | null, today: string = istanbulDay()): number | false {
  return day !== null && day === today ? TODAY_RESULTS_REFRESH_MS : false;
}

export function useMatchResultDays(enabled: boolean) {
  return useQuery<MatchResultDayDto[]>({
    queryKey: ["match-result-days", RESULT_DAY_SPAN],
    queryFn: () => getMatchResultDays(RESULT_DAY_SPAN),
    staleTime: FINISHED_DATA_IS_IMMUTABLE,
    // Bugünün sayısı gün içinde değişir; "Son sonuçlar" butonu bu listeye bakar.
    refetchInterval: enabled ? RESULT_DAYS_REFRESH_MS : false,
    refetchIntervalInBackground: false,
    enabled,
  });
}

export function useMatchResults(day: string | null) {
  return useQuery<MatchResultItemDto[]>({
    queryKey: ["match-results", day],
    queryFn: () => getMatchResults(day!),
    staleTime: day !== null && day === istanbulDay() ? TODAY_RESULTS_REFRESH_MS : FINISHED_DATA_IS_IMMUTABLE,
    refetchInterval: resultsRefreshInterval(day),
    refetchIntervalInBackground: false,
    enabled: !!day,
  });
}
