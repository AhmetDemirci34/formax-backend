// FORMAX · Kadro bekleme durumu — saf fonksiyonlar (test edilir).
//
// VAAT DEĞİL, DURUM: ekran saat sözü vermez ("1 saat önce açıklanacak" metni
// 06.09.2026'da kaldırıldı ve hiçbir durumda geri gelmez). Üç alanın kaynağı backend'dir:
// lineup.pollingWindowOpen (kickoff'a ≤ 90 dk), lineup.kickoffPassed, lineup.lastCheckedUtc.

import type { LineupSectionDto } from "@/types/api";

export const LINEUP_TEXT_NOT_FOUND = "Bu maç için doğrulanmış kadro verisi bulunamadı.";
export const LINEUP_TEXT_PROVIDER_PENDING =
  "Resmî kadrolar henüz veri sağlayıcısında yayımlanmadı. Yayımlandığında burada gösterilecek.";
export const LINEUP_TEXT_WAITING = "Resmî kadrolar maç saatine yaklaşıldığında burada gösterilecek.";

/** Doğrulanmış kadro gerçekten var mı? (açıklandı + en az bir ilk 11 oyuncusu) */
export function hasVerifiedLineup(l?: Partial<LineupSectionDto> | null): boolean {
  if (!l || l.lineupsAnnounced !== true) return false;
  return (l.homeStartingXI?.length ?? 0) > 0 || (l.awayStartingXI?.length ?? 0) > 0;
}

/** Kadro yokken gösterilecek tek cümle. */
export function lineupWaitingText(l?: Partial<LineupSectionDto> | null): string {
  if (l?.kickoffPassed === true) return LINEUP_TEXT_NOT_FOUND;
  if (l?.pollingWindowOpen === true) return LINEUP_TEXT_PROVIDER_PENDING;
  return LINEUP_TEXT_WAITING;
}

/**
 * "Son kontrol: SS:DD" — kullanıcının yerel saatiyle. Damga yoksa ya da bozuksa null:
 * satır hiç gösterilmez, uydurma bir saat yazılmaz.
 */
export function formatLastCheck(iso?: string | null, timeZone?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  const hhmm = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit", timeZone });
  return `Son kontrol: ${hhmm}`;
}
