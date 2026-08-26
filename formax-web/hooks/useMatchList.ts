"use client";

import { useQuery } from "@tanstack/react-query";
import { getMatchList, type MatchListItemDto } from "@/lib/api/matchList";

/**
 * Maçlar ekranının TEK veri kaynağı — GET /api/matches (gerçek backend).
 * Mock/placeholder yok. Canlı maçlar için düzenli tazelenir; canlı dakika ve
 * skor backend'den (MatchLiveStats) gelir, frontend üretmez.
 */
export function useMatchList() {
  return useQuery<MatchListItemDto[]>({
    queryKey: ["match-list"],
    queryFn: getMatchList,
    staleTime: 30_000,
    // CANLI VERİ KAPALI (backend: LiveMatchData:Enabled=false).
    // Buradaki 30 sn'lik otomatik tazeleme, canlı dakika/skor ilerlesin diye vardı. Backend
    // artık canlı veriyi hiçbir kaynaktan tazelemediği için bu istekler aynı veriyi tekrar
    // tekrar çekmekten başka bir şey yapmıyordu. Liste açılışta bir kez yüklenir; maçlar,
    // skorlar ve durumlar son bilinen gerçek değerleriyle gösterilir (uydurma yok).
    // Canlı özellik geri açıldığında bu satır eski haline döner.
    refetchInterval: false,
  });
}
