import { useQuery } from "@tanstack/react-query";
import { getMatchDetail } from "@/lib/api/matches";
import type { MatchDetailDto } from "@/types/api";

export function useMatchDetail(matchId: number, enabled = true) {
  return useQuery<MatchDetailDto>({
    queryKey: ["match", matchId],
    queryFn: () => getMatchDetail(matchId),
    staleTime: 30_000,
    // CANLI VERİ KAPALI (backend: LiveMatchData:Enabled=false).
    // Eskiden maç "Live" iken 30 sn'de bir kendini yeniliyordu. Backend canlı veriyi artık
    // tazelemediği için bu yoklama aynı yanıtı tekrar getiriyordu; kaldırıldı. Maç detayı
    // açılışta bir kez yüklenir. Canlı özellik geri açıldığında bu satır eski haline döner.
    refetchInterval: false,
    enabled: enabled && matchId > 0,
  });
}
