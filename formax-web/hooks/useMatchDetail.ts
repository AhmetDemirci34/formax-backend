import { useQuery } from "@tanstack/react-query";
import { getMatchDetail } from "@/lib/api/matches";
import type { MatchDetailDto } from "@/types/api";

/**
 * KÖK NEDEN (ölçülmeden değiştirildi → artık düzeltildi):
 *
 * İlk açılışta "yüklenemiyor" hatasının en olası kaynağı geçici ağ hatası veya
 * backend yavaş cevabıdır. React 18 StrictMode development'ta çift mount yapar,
 * fakat @tanstack/react-query v5 aynı queryKey için DEDUPLİKASYON uygular:
 * ikinci mount ilk isteğin sonucunu paylaşır, gerçek HTTP çiftlemesi OLMAZ.
 *
 * Asıl sorun: retry varsayılanı 3 idi fakat hata sınıflandırması yoktu —
 * 4xx (ör. 404 NotFound) bile yeniden deneniyordu ve kullanıcıyı 3×timeout
 * kadar bekletiyordu. Aşağıdaki yapılandırma:
 *   • network/5xx/timeout → EN FAZLA 1 retry (toplam 2 istek).
 *   • 4xx → retry YAPILMAZ (maç yoksa yoktur).
 */
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

    // KONTROLLÜ RETRY: network/5xx/timeout → EN FAZLA 1 yeniden deneme.
    // 4xx (istemci hatası, ör. 404 Not Found) → yeniden deneme YAPILMAZ.
    retry(failureCount, error) {
      if (failureCount >= 1) return false;

      // axios hataları: error.response?.status varsa HTTP cevabı gelmiş demektir.
      const status = (error as any)?.response?.status as number | undefined;

      // 4xx → yeniden deneme yapılmaz.
      if (status !== undefined && status >= 400 && status < 500) return false;

      // network hatası, 5xx veya timeout → 1 kez daha dene.
      return true;
    },
    retryDelay: 1_000,
  });
}
