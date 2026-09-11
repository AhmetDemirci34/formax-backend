// FORMAX · Maç listesi bilgi mimarisi — lig bazında gruplama.
//
// Buradaki tek "üretilen" şey SUNUM ETİKETİDİR (ülke adı + bayrak). Maç verisi, skor,
// olasılık ve AI çıktısı ASLA burada üretilmez — hepsi backend'den gelir.
// Bilinmeyen lig için ülke etiketi UYDURULMAZ; yalnız ligin kendi adı gösterilir.
//
// KİLİTLİ ÜRÜN KARARI: yalnız BAŞLAMAMIŞ maçlar gruplanır (bkz. lib/matches/upcomingOnly.ts).
// Canlı/devre arası/uzatma/penaltı/bitmiş/ertelenmiş maç hiçbir filtrede görünmez;
// durum filtresi (Tümü|Canlı|Başlamamış) kaldırılmıştır.

import type { MatchListItemDto } from "@/lib/api/matchList";
import { isUpcomingMatch } from "./upcomingOnly";

interface LeagueMeta {
  country: string;
  flag: string;
  /** Küçük sayı = listede üstte. Kilitli müsabaka kapsamının okuma önceliği. */
  rank: number;
}

/** Kilitli kapsamdaki ligler için ülke etiketi. Eşleşme ligin adı üzerinden yapılır. */
const LEAGUE_META: Array<{ match: RegExp; meta: LeagueMeta }> = [
  { match: /süper lig|super lig/i, meta: { country: "Türkiye", flag: "🇹🇷", rank: 1 } },
  { match: /champions league/i, meta: { country: "Avrupa", flag: "🏆", rank: 2 } },
  { match: /europa league/i, meta: { country: "Avrupa", flag: "🏆", rank: 3 } },
  { match: /conference league/i, meta: { country: "Avrupa", flag: "🏆", rank: 4 } },
  { match: /premier league/i, meta: { country: "İngiltere", flag: "🏴", rank: 5 } },
  { match: /championship/i, meta: { country: "İngiltere", flag: "🏴", rank: 6 } },
  { match: /la ?liga/i, meta: { country: "İspanya", flag: "🇪🇸", rank: 7 } },
  { match: /serie a/i, meta: { country: "İtalya", flag: "🇮🇹", rank: 8 } },
  { match: /bundesliga/i, meta: { country: "Almanya", flag: "🇩🇪", rank: 9 } },
  { match: /ligue 1/i, meta: { country: "Fransa", flag: "🇫🇷", rank: 10 } },
  { match: /eredivisie/i, meta: { country: "Hollanda", flag: "🇳🇱", rank: 11 } },
];

export function leagueMeta(league: string): LeagueMeta | null {
  if (!league) return null;
  for (const entry of LEAGUE_META) if (entry.match.test(league)) return entry.meta;
  return null;
}

export interface LeagueGroup {
  key: string;
  league: string;
  country: string | null;
  flag: string | null;
  rank: number;
  matches: MatchListItemDto[];
}

/** Aynı takvim gününde mi? (yerel gün, kullanıcının gördüğü gün) */
export function isSameLocalDay(iso: string, day: Date): boolean {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return false;
  return d.toDateString() === day.toDateString();
}

/**
 * Seçili günün BAŞLAMAMIŞ maçlarını lig gruplarına ayırır.
 * Durum filtresi yoktur: başlamış/bitmiş/ertelenmiş hiçbir maç bu listeye giremez.
 */
export function buildLeagueGroups(
  matches: MatchListItemDto[],
  day: Date,
  now: Date = new Date()
): LeagueGroup[] {
  const groups = new Map<string, LeagueGroup>();

  for (const m of matches) {
    if (!isUpcomingMatch(m, now)) continue;
    if (!isSameLocalDay(m.startTime, day)) continue;

    const league = m.league || "Diğer";
    const meta = leagueMeta(league);
    const existing = groups.get(league);
    if (existing) {
      existing.matches.push(m);
    } else {
      groups.set(league, {
        key: league,
        league,
        country: meta?.country ?? null,
        flag: meta?.flag ?? null,
        rank: meta?.rank ?? 99,
        matches: [m],
      });
    }
  }

  const list = [...groups.values()];
  for (const g of list) {
    g.matches.sort(
      (a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
    );
  }
  // Kapsam önceliği, sonra alfabetik (Türkçe).
  list.sort((a, b) => a.rank - b.rank || a.league.localeCompare(b.league, "tr"));
  return list;
}
