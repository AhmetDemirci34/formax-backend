// FORMAX · SONUÇLAR listesinin lig gruplaması.
//
// YAKLAŞAN sekmesiyle AYNI bilgi mimarisini kullanır: ülke etiketi ve lig sırası
// buildLeagueGroups ile ortak kaynaktan (leagueMeta) gelir. İki sekme aynı tasarım
// sistemine ait görünmelidir; ikinci bir ülke/bayrak tablosu YAZILMAZ.
//
// Burada hiçbir maç verisi üretilmez — yalnız backend'den gelen liste gruplanır.

import type { MatchResultItemDto } from "@/lib/api/matchResults";
import { leagueMeta } from "./leagueGrouping";

export interface ResultLeagueGroup {
  key: string;
  league: string;
  country: string | null;
  flag: string | null;
  rank: number;
  results: MatchResultItemDto[];
}

/**
 * Sonuçları lige göre gruplar.
 *
 * SIRA DETERMİNİSTİKTİR ve iki kademelidir:
 *  • Lig içi: EN SON BİTEN ÖNCE — kickoff azalan, eşitlikte MatchId azalan (backend
 *    zaten böyle sıralar, burada o sıra korunur).
 *  • Lig sırası: grubun en yeni maçı önce; eşitlikte kilitli kapsamın ürün sırası
 *    (leagueMeta.rank), sonra Türkçe alfabetik. Bilinmeyen lig ELENMEZ.
 */
export function buildResultLeagueGroups(results: readonly MatchResultItemDto[]): ResultLeagueGroup[] {
  const groups = new Map<string, ResultLeagueGroup>();

  for (const r of results) {
    const league = r.leagueName || "Diğer";
    const existing = groups.get(league);
    if (existing) {
      existing.results.push(r);
      continue;
    }
    const meta = leagueMeta(league);
    groups.set(league, {
      key: league,
      league,
      country: meta?.country ?? null,
      flag: meta?.flag ?? null,
      rank: meta?.rank ?? 99,
      results: [r],
    });
  }

  const list = [...groups.values()];
  const newest = (r: MatchResultItemDto) => new Date(r.matchDateUtc).getTime();
  for (const g of list) {
    g.results.sort((a, b) => newest(b) - newest(a) || b.matchId - a.matchId);
  }
  list.sort(
    (a, b) =>
      newest(b.results[0]) - newest(a.results[0]) ||
      b.results[0].matchId - a.results[0].matchId ||
      a.rank - b.rank ||
      a.league.localeCompare(b.league, "tr")
  );
  return list;
}
