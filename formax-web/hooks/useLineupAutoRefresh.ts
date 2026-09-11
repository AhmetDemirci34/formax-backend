import { useEffect, useState } from "react";
import type { MatchDetailDto } from "@/types/api";
import {
  createLineupPoller,
  msUntilPollWindow,
  shouldPollLineup,
} from "@/lib/lineup/lineupPolling";

/** T−90'ı beklemek için kurulan tek zamanlayıcının üst sınırı (çok uzak maçlarda kurulmaz). */
const MAX_WINDOW_WAIT_MS = 6 * 60 * 60_000;

/**
 * AÇIK EKRANDA KADRO — kadro DB'ye gelince manuel yenileme gerekmez.
 *
 * Yalnız mevcut maç-detay sorgusunu (`refetch`) yeniden çalıştırır: istek FORMAX
 * backend'ine gider, backend DB'den okur. Sağlayıcıya istek YOK.
 */
export function useLineupAutoRefresh(
  match: MatchDetailDto | undefined,
  refetch: () => Promise<{ data?: MatchDetailDto }>
) {
  // T−90 geçişini yakalamak için yeniden değerlendirme sayacı.
  const [windowTick, setWindowTick] = useState(0);
  const active = shouldPollLineup(match, Date.now());

  // Pencere henüz açılmadıysa T−90'da tek bir yeniden değerlendirme kurulur.
  useEffect(() => {
    if (active) return;
    const wait = msUntilPollWindow(match, Date.now());
    if (wait === null || wait > MAX_WINDOW_WAIT_MS) return;
    const h = window.setTimeout(() => setWindowTick((t) => t + 1), wait + 1_000);
    return () => window.clearTimeout(h);
  }, [active, match?.matchId, match?.matchDate, windowTick]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!active) return;
    const poller = createLineupPoller<MatchDetailDto>({
      refresh: async () => (await refetch()).data,
      now: () => Date.now(),
      setTimer: (fn, ms) => window.setTimeout(fn, ms),
      clearTimer: (h) => window.clearTimeout(h as number),
    });
    poller.start();
    // Sayfa kapanınca / kadro gelince (active=false) zamanlayıcı temizlenir.
    return () => poller.stop();
  }, [active, match?.matchId]); // eslint-disable-line react-hooks/exhaustive-deps
}
