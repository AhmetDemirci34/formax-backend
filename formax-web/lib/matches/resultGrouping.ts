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
 *  • Lig sırası: kilitli kapsamın ürün sırası (leagueMeta.rank), eşitlikte Türkçe
 *    alfabetik. Bilinmeyen lig en sona düşer ama ELENMEZ.
 *  • Lig içi: kickoff saati, eşitlikte MatchId. Aynı dakikada başlayan maçlar her
 *    açılışta AYNI sırada görünür — backend zaten böyle sıralar, burada o sıra
 *    korunur, yeniden yorumlanmaz.
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
  for (const g of list) {
    g.results.sort(
      (a, b) =>
        new Date(a.matchDateUtc).getTime() - new Date(b.matchDateUtc).getTime() ||
        a.matchId - b.matchId
    );
  }
  list.sort((a, b) => a.rank - b.rank || a.league.localeCompare(b.league, "tr"));
  return list;
}
