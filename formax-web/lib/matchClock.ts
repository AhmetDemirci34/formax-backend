// ─────────────────────────────────────────────────────────────────────────────
// FORMAX · Maç saati / geri sayım — TEK ORTAK HESAP
//
// NEDEN VAR: aynı maç iki ekranda iki farklı durum gösteriyordu. Keşfet
// (components/discover/hero/MatchClock.tsx) doğru davranıyordu; Maç Merkezi Hero'su
// ise KENDİ hesabını yapıyor ve kickoff geçmişse saatten "CANLI" ÜRETİYORDU.
// Backend'in kickoff'u UTC eki olmadan gönderdiği dönemde bu, gelecekteki maçı da
// canlı gösteriyordu (ölçüldü: Fenerbahçe–Lyon, 19:06'da CANLI).
//
// KİLİTLİ KURALLAR
//  • Bu modül saatten CANLI/BİTTİ ÜRETMEZ. Canlı durum YALNIZ backend'in açıkça
//    gönderdiği alandan gelir ve MVP'de zaten kapalıdır (LiveMatchData:Enabled=false).
//  • Kickoff geçmiş ama backend canlı demediyse → yine planlanan saat gösterilir.
//  • Kickoff ISO değeri backend'den geldiği gibi ayrıştırılır; manuel saat dilimi
//    kaydırması (ör. "+3") YAPILMAZ. Yerel saate çevirmeyi tarayıcı yapar.
// ─────────────────────────────────────────────────────────────────────────────

const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;

/** ≤ 6 saat kala saniyeli geri sayıma geçilir (Keşfet'te doğrulanmış eşik). */
export const COUNTDOWN_WINDOW_MS = 6 * HOUR_MS;

export type MatchClockMode = "time" | "countdown";

export interface MatchClockState {
  mode: MatchClockMode;
  label: string;
}

function two(n: number): string {
  return n < 10 ? `0${n}` : `${n}`;
}

function sameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

/** "Bugün 22:00" / "Yarın 22:00" / "26 Ağu 22:00" — kullanıcının yerel saatinde. */
export function absoluteTime(d: Date): string {
  const hm = d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
  const today = new Date();
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  if (sameDay(d, today)) return `Bugün ${hm}`;
  if (sameDay(d, tomorrow)) return `Yarın ${hm}`;
  return `${d.toLocaleDateString("tr-TR", { day: "2-digit", month: "short" })} ${hm}`;
}

/**
 * Kickoff ms + şimdi → gösterilecek durum. CANLI/BİTTİ ÜRETMEZ (bkz. dosya başlığı).
 */
export function computeMatchClock(kickoffMs: number, now: number): MatchClockState {
  const diff = kickoffMs - now;

  if (diff > COUNTDOWN_WINDOW_MS) {
    return { mode: "time", label: absoluteTime(new Date(kickoffMs)) };
  }
  if (diff > 0) {
    const totalSec = Math.floor(diff / 1000);
    const h = Math.floor(totalSec / 3600);
    const m = Math.floor((totalSec % 3600) / 60);
    const s = totalSec % 60;
    return { mode: "countdown", label: `${two(h)}:${two(m)}:${two(s)}` };
  }
  // Kickoff geçmiş, backend canlı demedi → durum ÜRETME; planlanan saati göster.
  return { mode: "time", label: absoluteTime(new Date(kickoffMs)) };
}

/** ISO string → ms. Geçersizse NaN. */
export function kickoffMsOf(iso?: string | null): number {
  return iso ? new Date(iso).getTime() : NaN;
}

/**
 * Backend'in maç durumu "bitti" mi? Zamandan DEĞİL, yalnız status alanından okunur.
 * (Backend canonical: MatchStatuses.Finished; eski/alternatif etiketler de tanınır.)
 */
const FINISHED_STATUSES = new Set([
  "finished",
  "fulltime",
  "afterextratime",
  "afterpenalties",
  "ended",
]);

export function isFinishedStatus(status?: string | null): boolean {
  if (!status) return false;
  return FINISHED_STATUSES.has(status.toLowerCase().replace(/\s+|_/g, ""));
}
