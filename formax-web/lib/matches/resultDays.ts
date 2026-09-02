// FORMAX · SONUÇLAR sekmesinin takvim kuralı — TEK MERKEZ.
//
// Bütün gün hesapları Europe/Istanbul takvimine göredir. Tarayıcının yerel saat dilimi
// kullanılamaz: kullanıcı yurt dışındayken "bugün" kayar ve 21:00'den sonra başlayan
// Avrupa maçları bir gün öteye düşer.

/**
 * Ürün kararı (03.09.2026): bugün + önceki 30 takvim günü gezilebilir.
 *
 * 7 gün, bitmiş maç ARŞİVİ için yetmiyordu: iki hafta önceki bir maça normal UI
 * akışıyla ulaşmanın yolu kalmıyordu. Sınır YALNIZ gezinme içindir — uç daha eski
 * tarihleri de sunar.
 */
export const RESULT_DAY_SPAN = 30;

const TR_TZ = "Europe/Istanbul";

/** Bir anın Türkiye takvim günü, "yyyy-MM-dd". */
export function istanbulDay(at: Date = new Date()): string {
  // en-CA biçimi zaten "yyyy-mm-dd" verir; elle string kurmaktan güvenlidir.
  return at.toLocaleDateString("en-CA", { timeZone: TR_TZ });
}

/** "yyyy-MM-dd" gününe delta gün ekler (takvim aritmetiği, saat dilimsiz). */
export function shiftDay(day: string, delta: number): string {
  const [y, m, d] = day.split("-").map(Number);
  const base = new Date(Date.UTC(y, m - 1, d));
  base.setUTCDate(base.getUTCDate() + delta);
  return base.toISOString().slice(0, 10);
}

/** Gezilebilir en eski gün (bugün dâhil 8 günün ilki). */
export function oldestSelectableDay(today: string = istanbulDay()): string {
  return shiftDay(today, -(RESULT_DAY_SPAN - 1));
}

/** Bu gün seçilebilir mi? Gelecek YOK, 7 günden eski YOK. */
export function isSelectableDay(day: string, today: string = istanbulDay()): boolean {
  return day <= today && day >= oldestSelectableDay(today);
}

/**
 * Sekme ilk açıldığında hangi gün seçili gelmeli?
 *
 * Bugün sonuç varsa bugün. Yoksa pencere içindeki EN YAKIN sonuçlu gün — kullanıcıyı
 * boş bir "bugün" ekranına düşürüp "sonuç yok" sanmasına izin verilmez. Pencerenin
 * tamamı boşsa bugün seçilir ve ekran dürüst boş durumu gösterir.
 */
export function pickInitialDay(
  daysWithResults: readonly { date: string; matchCount: number }[],
  today: string = istanbulDay()
): string {
  const oldest = oldestSelectableDay(today);
  const inWindow = daysWithResults
    .filter((d) => d.matchCount > 0 && d.date <= today && d.date >= oldest)
    .map((d) => d.date)
    .sort()
    .reverse();
  return inWindow[0] ?? today;
}

/** Tarih seçicideki insan okunur etiket. */
export function dayLabel(day: string, today: string = istanbulDay()): string {
  if (day === today) return "Bugün";
  if (day === shiftDay(today, -1)) return "Dün";

  const [y, m, d] = day.split("-").map(Number);
  const at = new Date(Date.UTC(y, m - 1, d, 12));
  const dm = at.toLocaleDateString("tr-TR", { day: "numeric", month: "long", timeZone: "UTC" });
  const wd = at.toLocaleDateString("tr-TR", { weekday: "short", timeZone: "UTC" });
  return `${wd} · ${dm}`;
}

/** Kickoff'un Türkiye saati ("22:00"). Saat üretilmez, yalnız çevrilir. */
export function istanbulTime(iso: string): string {
  const at = new Date(iso);
  if (Number.isNaN(at.getTime())) return "--:--";
  return at.toLocaleTimeString("tr-TR", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: TR_TZ,
  });
}
