// FORMAX · Anonim maç takibi için KALICI depo (localStorage).
//
// NEDEN VAR: gerçek takip uçları (/api/follows/*) MEVCUT ve kullanılıyor, ama JWT istiyor —
// giriş yapılmamışken 401 döner (ölçüldü). Auth kapısı şu an KAPALI (AUTH_GATE_ENABLED=false),
// yani kullanıcıların çoğu anonim geziyor. Bu depo yalnız O DURUM için kalıcılık sağlar.
//
// İKİNCİ BİR TAKİP SİSTEMİ DEĞİLDİR: tek giriş noktası hâlâ `useFollow`. Giriş yapılmışsa
// backend, yapılmamışsa bu depo kullanılır. Sahte uç uydurulmadı.
//
// Kimlik: YALNIZ int MatchId. Küme semantiği → aynı maç iki kez eklenemez.

const KEY = "formax_followed_matches";
const EMPTY: number[] = [];

let cache: number[] | null = null;
const listeners = new Set<() => void>();

function normalize(input: unknown): number[] {
  if (!Array.isArray(input)) return [];
  const ids = input.filter((v): v is number => Number.isInteger(v) && (v as number) > 0);
  return [...new Set(ids)].sort((a, b) => a - b);
}

function read(): number[] {
  if (cache) return cache;
  if (typeof window === "undefined") return EMPTY;
  try {
    const raw = window.localStorage.getItem(KEY);
    cache = raw ? normalize(JSON.parse(raw)) : [];
  } catch {
    cache = [];
  }
  return cache;
}

function write(next: number[]): void {
  cache = normalize(next);
  try {
    window.localStorage.setItem(KEY, JSON.stringify(cache));
  } catch {
    /* kota/gizli mod — bellekte tutulur, ekran yine doğru çalışır */
  }
  for (const l of listeners) l();
}

export const localFollowStore = {
  /** useSyncExternalStore aboneliği — tüm ekranlar aynı anda güncellenir. */
  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
  /** Kararlı referans döndürür (aksi halde React sonsuz döngüye girer). */
  getSnapshot(): number[] {
    return read();
  },
  getServerSnapshot(): number[] {
    return EMPTY;
  },
  isFollowing(matchId: number): boolean {
    return read().includes(matchId);
  },
  /** Ekler. Zaten varsa HİÇBİR ŞEY yapmaz (tekilleştirme garantisi). */
  add(matchId: number): void {
    const cur = read();
    if (cur.includes(matchId)) return;
    write([...cur, matchId]);
  },
  remove(matchId: number): void {
    const cur = read();
    if (!cur.includes(matchId)) return;
    write(cur.filter((id) => id !== matchId));
  },
  toggle(matchId: number): boolean {
    const next = !localFollowStore.isFollowing(matchId);
    if (next) localFollowStore.add(matchId);
    else localFollowStore.remove(matchId);
    return next;
  },
};
