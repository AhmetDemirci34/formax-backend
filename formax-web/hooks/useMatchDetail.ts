import { useQuery } from "@tanstack/react-query";
import { getMatchDetail } from "@/lib/api/matches";
import { isFinishedMatch } from "@/lib/matches/upcomingOnly";
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
export const matchDetailKey = (matchId: number) => ["match", matchId] as const;

/** Sonucu beklenen açık ekranın DB'yi yeniden okuma aralığı (resmî sonuç arka planda yazılır). */
export const AWAITING_RESULT_REFRESH_MS = 20_000;
/** Başlama saatinden bu kadar sonra hâlâ sonuç yoksa açık ekran tazelemeyi bırakır (sonsuz yoklama yok). */
export const AWAITING_RESULT_WINDOW_MS = 6 * 60 * 60_000;

/** KONTROLLÜ RETRY: network/5xx/timeout → EN FAZLA 1 yeniden deneme; 4xx → yeniden deneme YOK. */
export function matchDetailRetry(failureCount: number, error: unknown): boolean {
  if (failureCount >= 1) return false;
  // axios hataları: error.response?.status varsa HTTP cevabı gelmiş demektir.
  const status = (error as { response?: { status?: number } } | null)?.response?.status;
  if (status !== undefined && status >= 400 && status < 500) return false;
  return true;
}

/**
 * SONUÇ BEKLENİYOR MU — başlama saati geçmiş, backend henüz "bitti" demiyor ve pencere
 * içinde. Bu durumda açık ekran backend'i (DB) aralıklı okur; resmî sonuç arka plan
 * botuyla yazılınca Maç Özeti manuel yenilemesiz gelir. Canlı skor/dakika GÖSTERİLMEZ;
 * istek sağlayıcıya ya da resmî kaynağa gitmez. Ertelenen/iptal edilen maç beklenmez.
 */
export function awaitingResultRefreshInterval(
  match: Pick<MatchDetailDto, "status" | "matchDate"> | undefined,
  nowMs: number = Date.now()
): number | false {
  if (!match || isFinishedMatch(match.status)) return false;
  const status = (match.status ?? "").trim().toLowerCase();
  if (status === "postponed" || status === "cancelled" || status === "abandoned") return false;
  const kickoff = Date.parse(match.matchDate ?? "");
  if (Number.isNaN(kickoff) || kickoff > nowMs) return false;
  return nowMs - kickoff <= AWAITING_RESULT_WINDOW_MS ? AWAITING_RESULT_REFRESH_MS : false;
}

/** Tek sorgu tanımı — sayfa ve testler AYNI anahtarı ve seçenekleri kullanır. */
export function matchDetailQueryOptions(
  matchId: number,
  enabled = true,
  fetcher: (id: number) => Promise<MatchDetailDto> = getMatchDetail
) {
  return {
    queryKey: matchDetailKey(matchId),
    queryFn: () => fetcher(matchId),
    staleTime: 30_000,
    // CANLI VERİ KAPALI: maç oynanırken yoklama yok. Yalnız başlama saati geçmiş ve sonucu
    // henüz yazılmamış maçta DB tekrar okunur; sonuç gelince ya da pencere bitince durur.
    refetchInterval: (query: { state: { data?: MatchDetailDto } }) =>
      awaitingResultRefreshInterval(query.state.data),
    refetchIntervalInBackground: false,
    enabled: enabled && matchId > 0,
    retry: matchDetailRetry,
    retryDelay: 1_000,
  };
}

export function useMatchDetail(matchId: number, enabled = true) {
  return useQuery<MatchDetailDto>(matchDetailQueryOptions(matchId, enabled));
}
