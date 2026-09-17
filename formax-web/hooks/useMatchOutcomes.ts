import { useQuery } from "@tanstack/react-query";
import { getMatchOutcomes } from "@/lib/api/outcomes";
import type { OutcomeSnapshotDto } from "@/types/outcomes";

/** Keşfet ve Maç Detayı'nın paylaştığı TEK cache anahtarı — aynı SnapshotId iki ekranda. */
export const matchOutcomesKey = (matchId: number) => ["outcomes", matchId] as const;

/**
 * Yeni snapshot (resmî kadro / kritik gelişme sonrası arka planda yazılan) açık ekrana MANUEL YENİLEMESİZ gelsin diye
 * dakikada bir salt-DB okuması. Aynı maç için Keşfet halkası, Keşfet kartları, Detay göstergesi ve Detay kartları AYNI
 * anahtarı paylaşır → tek istek; sekme arka plandayken sorgu yok; bileşen kapanınca zamanlayıcı biter (sonsuz yoklama yok).
 */
export const OUTCOMES_REFRESH_MS = 60_000;

export function useMatchOutcomes(matchId: number, enabled = true) {
  return useQuery<OutcomeSnapshotDto>({
    queryKey: matchOutcomesKey(matchId),
    queryFn: () => getMatchOutcomes(matchId),
    staleTime: 30 * 1000,
    gcTime: 30 * 60 * 1000,
    refetchInterval: enabled && matchId > 0 ? OUTCOMES_REFRESH_MS : false,
    refetchIntervalInBackground: false,
    enabled: enabled && matchId > 0,
  });
}
