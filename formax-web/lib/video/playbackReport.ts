// FORMAX · Oynatıcı hata bildirimi — gerçek YouTube oynatıcısı video kaldırılmış (100), gömme/bölge engeli
// (101/150/152) ya da oynatıcı hatası (2/5) bildirdiğinde backend'e YAZILIR.
//
// NEDEN: ekranın yalnız bir sonraki videoya geçmesi yetmez. Backend kaydı SourceBlocked yapar, gerekçeyi
// deftere yazar ve maçı kalıcı arama kuyruğuna döndürür; böylece bir sonraki kullanıcı aynı engelli videoyu
// görmez ve arka plan işi alternatif resmî kaynağı aramaya devam eder. Bu çağrı keşif BAŞLATMAZ.

import apiClient from "@/lib/api/client";
import type { MatchVideoDto } from "@/types/api";

/** Kaydı kapatan oynatıcı kodları (YouTube IFrame API). */
export const BLOCKING_PLAYER_CODES = new Set([2, 5, 100, 101, 150, 152]);

/** Aynı oturumda aynı video için tek bildirim. */
const reported = new Set<string>();

/** Gömme adresinden YouTube kimliği ("…/embed/ID"). */
export function youtubeIdFromEmbed(embedUrl?: string | null): string | null {
  if (!embedUrl) return null;
  const m = embedUrl.match(/\/embed\/([A-Za-z0-9_-]{11})/);
  return m ? m[1] : null;
}

export function shouldReport(code: number): boolean {
  return BLOCKING_PLAYER_CODES.has(code);
}

export async function reportPlaybackError(matchId: number, video: MatchVideoDto, code: number): Promise<void> {
  if (!matchId || !shouldReport(code)) return;
  const videoId = youtubeIdFromEmbed(video.embedUrl);
  if (!videoId) return;
  const key = `${matchId}|${videoId}`;
  if (reported.has(key)) return;
  reported.add(key);
  try {
    await apiClient.post(`/api/matches/${matchId}/videos/playback-error`, { videoId, code });
  } catch {
    // Bildirim başarısızsa ekran yine alternatif videoya geçer; arka plan yeniden doğrulaması oEmbed ile ayrıca yakalar.
    reported.delete(key);
  }
}
