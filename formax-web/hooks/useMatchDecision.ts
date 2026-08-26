import { useQuery } from "@tanstack/react-query";
import { getMatchDecision } from "@/lib/api/decision";
import type { MatchDecisionDto } from "@/types/decision";

/** Üç ekranın paylaştığı tek cache anahtarı. */
export const matchDecisionKey = (matchId: number) => ["decision", matchId] as const;

/**
 * TEK AI analizi — Match Detail, Discover teaser'ı ve AI İncele aynı
 * queryKey üzerinden aynı cache'i okur. AI İncele sayfası açıldığında
 * veri zaten cache'te olduğu için İKİNCİ İSTEK ATILMAZ.
 *
 * Analiz maç öncesi üretilir ve sık değişmez → uzun staleTime, otomatik
 * polling yok (canlı skor bu paketin işi değildir).
 */
export function useMatchDecision(matchId: number, enabled = true) {
  return useQuery<MatchDecisionDto>({
    queryKey: matchDecisionKey(matchId),
    queryFn: () => getMatchDecision(matchId),
    staleTime: 5 * 60 * 1000,
    gcTime: 30 * 60 * 1000,
    enabled: enabled && matchId > 0,
  });
}
