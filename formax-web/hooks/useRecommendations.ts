import { useInfiniteQuery } from "@tanstack/react-query";
import { getRecommendations } from "@/lib/api/home";
import type { RecommendationCardDto } from "@/types/api";

// Backend her karta Decision paketinden gerçek AI yüzeyini yazar ve güven endeksi 0 olan
// maçta tahmin GÖNDERMEZ (bkz. GetRecommendationFeedUseCase.ApplyDecisionSurfaceAsync).
// Gerçek GDP kapsamı seyrek: ölçümde 20 kartta 1, 50 kartta 4 maçın AI verisi vardı.
// 10'luk sayfada Kombin (min. 2 ayak) hiç dolmuyordu. 50 → ilk açılışta gerçek ayaklar
// gelir; ölçülen maliyet ≈ 0,84 sn (feed decision paketlerini toplu kurar).
const PAGE_SIZE = 50;

export const RECOMMENDATIONS_KEY = ["recommendations"] as const;

/**
 * @param maxHorizonDays Hero/swipe kuyruğunun zaman ufku (bugün + N gün). Yalnız backend'e
 * iletilir; süzme BACKEND'de aday üretiminde yapılır (frontend tarih hesabı YAPMAZ).
 * Verilmezse geniş evren döner — Sana Özel, Günün AI Kombini, /tumu ve Trending böyle çağırır
 * ve bu değişiklikten ETKİLENMEZ. Ufuklu ve ufuksuz feed'ler AYRI cache anahtarında tutulur.
 */
export function useRecommendations(maxHorizonDays?: number) {
  return useInfiniteQuery<RecommendationCardDto[]>({
    queryKey: [...RECOMMENDATIONS_KEY, maxHorizonDays ?? "all"],
    queryFn: ({ pageParam }) =>
      getRecommendations(pageParam as number, PAGE_SIZE, maxHorizonDays),
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) =>
      lastPage.length === PAGE_SIZE ? allPages.length + 1 : undefined,
    staleTime: 5 * 60 * 1000,  // 5 min
    // MERGE DEDUP (gerçek neden fix'i): backend Skip/Take sıralamayı her sayfa çağrısında
    // yeniden hesapladığı için (bandit UCB + kullanıcı aksiyonu değişimi) sayfalar aynı
    // maçı içerebiliyor → flatten sonrası duplicate matchId. Sayfaları matchId'ye göre
    // TEKİLLEŞTİRİYORUZ (ilk görülen kalır). Key hack değil: gerçek duplicate item elenir.
    // Tek nokta → feed/combo/featured hepsi tekil. hasNextPage/fetchNextPage etkilenmez.
    select: (data) => {
      const seen = new Set<number>();
      const unique: RecommendationCardDto[] = [];
      for (const page of data.pages) {
        for (const card of page) {
          if (card && !seen.has(card.matchId)) {
            seen.add(card.matchId);
            unique.push(card);
          }
        }
      }
      return { pages: [unique], pageParams: data.pageParams };
    },
  });
}
