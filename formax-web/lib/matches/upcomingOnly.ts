// FORMAX · "YALNIZ MAÇ ÖNCESİ" ürün kuralı — TEK MERKEZ.
//
// KİLİTLİ ÜRÜN KARARI: FORMAX canlı maç GÖSTERMEZ. Kullanıcıya maç önerilen/ listelenen
// her yüzey bu tek fonksiyondan geçer; ikinci bir kural kopyası yazılmaz.
//
// Canlı skor/dakika için hiçbir sağlayıcıya (api-football dahil) istek YAPILMAZ —
// karar tamamen elde olan durum + kickoff zamanıyla verilir.

/** Ekranda gösterilebilir tek durum ailesi. Diğer her şey gizlenir. */
const UPCOMING_STATUSES = new Set(["scheduled", "notstarted", "not started", "ns", "tba"]);

/**
 * Bu maç kullanıcıya gösterilebilir mi?
 *
 * GÖSTER : status Scheduled/NotStarted VE kickoff gelecekte
 * GİZLE  : Live / FirstHalf / HalfTime / SecondHalf / ExtraTime / Penalties /
 *          Finished / Postponed / Cancelled / Abandoned — ve durumu ne olursa olsun
 *          kickoff'u geçmiş her maç (bayat "başlamamış" anlık görüntüsü ekranda kalmasın).
 */
export function isUpcomingMatch(
  match: { status?: string | null; startTime?: string | null },
  now: Date = new Date()
): boolean {
  const status = (match.status ?? "").trim().toLowerCase();
  if (status.length > 0 && !UPCOMING_STATUSES.has(status)) return false;

  const iso = match.startTime;
  if (!iso) return false;
  const kickoff = new Date(iso);
  if (Number.isNaN(kickoff.getTime())) return false;

  // GÜVENLİ EK KONTROL: backend durumu güncellenmemiş olsa bile başlamış maç düşer.
  return kickoff.getTime() > now.getTime();
}

/** Liste süzgeci — aynı kuralın toplu hâli. */
export function onlyUpcoming<T extends { status?: string | null; startTime?: string | null }>(
  matches: readonly T[],
  now: Date = new Date()
): T[] {
  return matches.filter((m) => isUpcomingMatch(m, now));
}

/**
 * MAÇ BİTTİ Mİ — maç sonrası özet ekranının tek kapısı.
 *
 * Backend durumu tek yetkilidir: saatten "bitti" ÜRETİLMEZ (bir maç uzayabilir,
 * ertelenebilir). Uzatma/penaltı ile biten maçlar da bitmiş sayılır.
 */
const FINISHED_STATUSES = new Set([
  "finished",
  "fulltime",
  "full time",
  "ft",
  "afterextratime",
  "aet",
  "afterpenalties",
  "pen",
]);

export function isFinishedMatch(status?: string | null): boolean {
  return FINISHED_STATUSES.has((status ?? "").trim().toLowerCase());
}
