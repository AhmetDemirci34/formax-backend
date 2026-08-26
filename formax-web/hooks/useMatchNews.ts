import { useQuery } from "@tanstack/react-query";
import { getMatchNews } from "@/lib/api/matches";
import type { NabizSectionDto } from "@/types/api";

/**
 * SON DAKİKA — maçın haberleri, seçili FORMAX dilinde.
 *
 * Anahtar (matchId, language): dil değişince yeniden istenir, aynı dilde tekrar
 * istenmez. Çeviri backend'de kalıcı önbellekli olduğu için ilk istek yavaş
 * (LLM), sonrakiler anlıktır.
 *
 * `retry: false` — çeviri/haber ucu hata verirse sessizce yeniden denemek yerine
 * hata durumu yüzeye çıkar; UI bunu "haber yok" diye MASKELEMEZ.
 */
export function useMatchNews(matchId: number, language: string, enabled = true) {
  return useQuery<NabizSectionDto>({
    queryKey: ["match-news", matchId, language],
    queryFn: () => getMatchNews(matchId, language),
    staleTime: 5 * 60_000,
    retry: false,
    refetchOnWindowFocus: false,
    enabled: enabled && matchId > 0,
  });
}
