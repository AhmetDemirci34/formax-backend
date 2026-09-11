// FORMAX · Kadro bekleme durumu — saf fonksiyonlar (test edilir).
//
// VAAT DEĞİL, DURUM: ekran saat sözü vermez ("1 saat önce açıklanacak" metni
// 06.09.2026'da kaldırıldı ve hiçbir durumda geri gelmez). Kaynak backend'dir:
// lineup.status (Released | SourceDelayed | Waiting | NotFound), lineup.pollingWindowOpen,
// lineup.kickoffPassed, lineup.lastCheckedUtc.
//
// "KADRO YAYIMLANMADI" DENMEZ (11.09.2026): Venezia–Fiorentina'da kadro dünyada T−13'te
// yayımlıyken ekran "henüz veri sağlayıcısında yayımlanmadı" diyordu. FORMAX yalnız kendi
// lisanslı kaynağının ne ilettiğini bilir; cümle bunu söyler.

import type { LineupSectionDto } from "@/types/api";

export const LINEUP_TEXT_NOT_FOUND = "Bu maç için doğrulanmış kadro verisi bulunamadı.";
export const LINEUP_TEXT_SOURCE_DELAYED =
  "FORMAX veri kaynağı resmî kadroyu henüz iletmedi. Kontroller sürüyor.";
export const LINEUP_TEXT_WAITING = "Resmî kadrolar maç saatine yaklaşıldığında burada gösterilecek.";

/** Doğrulanmış kadro gerçekten var mı? (açıklandı + en az bir ilk 11 oyuncusu) */
export function hasVerifiedLineup(l?: Partial<LineupSectionDto> | null): boolean {
  if (!l || l.lineupsAnnounced !== true) return false;
  return (l.homeStartingXI?.length ?? 0) > 0 || (l.awayStartingXI?.length ?? 0) > 0;
}

/** Kadro yokken gösterilecek tek cümle. Backend durumu önceliklidir. */
export function lineupWaitingText(l?: Partial<LineupSectionDto> | null): string {
  switch (l?.status) {
    case "SourceDelayed":
      return LINEUP_TEXT_SOURCE_DELAYED;
    case "NotFound":
      return LINEUP_TEXT_NOT_FOUND;
    case "Waiting":
      return LINEUP_TEXT_WAITING;
  }
  // Eski yanıt (status alanı yok): bayraklardan türet.
  if (l?.kickoffPassed === true) return LINEUP_TEXT_NOT_FOUND;
  if (l?.pollingWindowOpen === true) return LINEUP_TEXT_SOURCE_DELAYED;
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
