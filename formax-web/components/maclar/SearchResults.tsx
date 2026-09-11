"use client";

import { useMemo } from "react";
import type { MatchResultItemDto } from "@/lib/api/matchResults";

interface Props {
  results: MatchResultItemDto[];
  isSearching: boolean;
  onOpen: (matchId: number) => void;
}

interface DateGroup {
  date: string;
  leagues: LeagueGroup[];
}

interface LeagueGroup {
  leagueId: number;
  leagueName: string;
  matches: MatchResultItemDto[];
}

/**
 * Arama sonuçlarını tarih ve lige göre gruplayarak gösterir.
 * Mevcut ResultsView / LeagueGroup tasarımıyla uyumlu stilde.
 */
export function SearchResults({ results, isSearching, onOpen }: Props) {
  const groups = useMemo(() => groupByDateAndLeague(results), [results]);

  if (isSearching && results.length === 0) {
    return (
      <div className="flex items-center justify-center py-16">
        <span className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-[#A855F7] border-t-transparent" />
      </div>
    );
  }

  if (!isSearching && results.length === 0) {
    return (
      <p className="pt-16 text-center text-[13px] text-text-muted">
        Eşleşen maç bulunamadı.
      </p>
    );
  }

  return (
    <main className="flex flex-col gap-2.5 px-3.5 pb-[calc(var(--bottom-nav-height)+16px)] pt-2">
      {groups.map((dg) => (
        <div key={dg.date} className="flex flex-col gap-2">
          <h3 className="px-1 pt-2 text-[11px] font-bold uppercase tracking-wider text-text-muted">
            {formatDateLabel(dg.date)}
          </h3>
          {dg.leagues.map((lg) => (
            <div key={`${dg.date}-${lg.leagueId}`} className="rounded-2xl bg-white/[0.04] p-3">
              <h4 className="mb-2 text-[11px] font-semibold text-text-muted">
                {lg.leagueName}
              </h4>
              {lg.matches.map((m) => (
                <button
                  key={m.matchId}
                  type="button"
                  onClick={() => onOpen(m.matchId)}
                  className="flex w-full items-center gap-3 rounded-xl px-2 py-2.5 text-left transition-colors hover:bg-white/[0.06] active:bg-white/[0.08]"
                >
                  <div className="flex flex-1 flex-col gap-0.5">
                    <div className="flex items-center justify-between">
                      <span className="text-[13px] font-medium text-white">
                        {m.homeTeam.name}
                      </span>
                      {m.status === "Finished" && (
                        <span className="min-w-[20px] text-right text-[13px] font-bold tabular-nums text-white">
                          {m.homeScore}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-[13px] font-medium text-white">
                        {m.awayTeam.name}
                      </span>
                      {m.status === "Finished" && (
                        <span className="min-w-[20px] text-right text-[13px] font-bold tabular-nums text-white">
                          {m.awayScore}
                        </span>
                      )}
                    </div>
                  </div>
                  {m.hasPlayableOfficialVideo && (
                    <span className="shrink-0 rounded-md bg-[#A855F7]/20 px-1.5 py-0.5 text-[9px] font-bold text-[#A855F7]">
                      VİDEO
                    </span>
                  )}
                </button>
              ))}
            </div>
          ))}
        </div>
      ))}
    </main>
  );
}

/**
 * Arama sonuçlarını tarih → lig olarak gruplar.
 *
 * SIRA BURADA ÜRETİLMEZ, KORUNUR.
 *
 * KÖK NEDEN (06.09.2026): burada tarih grupları koşulsuz ARTAN sıralanıyordu
 * (`a.localeCompare(b)`). Backend "sonuçlar" aramasını doğru sırada — en yeni maç
 * en üstte — döndürdüğü hâlde bu satır sırayı ters çeviriyordu: "Fenerbahçe"
 * aramasında dün oynanan Fenerbahçe–Beşiktaş en alta düşüyor, Temmuz maçları
 * listenin başında görünüyordu.
 *
 * ÇÖZÜM: Map ekleme sırası korunur. JavaScript'te `Map` anahtarları ilk ekleme
 * sırasında tutar; backend listesi zaten deterministik sıradadır (finished →
 * tarih azalan + MatchId azalan, upcoming → tarih artan + MatchId artan).
 * Böylece iki sekme için ikinci bir sıralama kuralı yazılmasına gerek kalmaz ve
 * arayüz backend sözleşmesiyle ayrışamaz.
 */
function groupByDateAndLeague(results: MatchResultItemDto[]): DateGroup[] {
  const dateMap = new Map<string, Map<number, MatchResultItemDto[]>>();

  for (const m of results) {
    const dateKey = m.matchDateUtc.substring(0, 10); // yyyy-MM-dd
    if (!dateMap.has(dateKey)) dateMap.set(dateKey, new Map());
    const leagueMap = dateMap.get(dateKey)!;
    if (!leagueMap.has(m.leagueId)) leagueMap.set(m.leagueId, []);
    leagueMap.get(m.leagueId)!.push(m);
  }

  return Array.from(dateMap.entries()).map(([date, leagueMap]) => ({
    date,
    leagues: Array.from(leagueMap.entries()).map(([leagueId, matches]) => ({
      leagueId,
      leagueName: matches[0]?.leagueName ?? "",
      matches,
    })),
  }));
}

/** yyyy-MM-dd → "18 Ağustos 2026" gibi Türkçe tarih.
 *  Intl API kullanılır; desteklenmezse ham tarih döner. */
function formatDateLabel(iso: string): string {
  try {
    const d = new Date(iso + "T00:00:00");
    return d.toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" });
  } catch {
    return iso;
  }
}
