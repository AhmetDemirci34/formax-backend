import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { pickInitialDay, istanbulDay, shiftDay } from "@/lib/matches/resultDays";
import { buildResultLeagueGroups } from "@/lib/matches/resultGrouping";
import { resultsRefreshInterval, TODAY_RESULTS_REFRESH_MS } from "@/hooks/useMatchResults";
import type { MatchResultItemDto } from "@/lib/api/matchResults";

function item(matchId: number, leagueName: string, matchDateUtc: string): MatchResultItemDto {
  return {
    matchId,
    leagueName,
    matchDateUtc,
    homeTeam: { teamId: 1, name: "Ev", logoUrl: null },
    awayTeam: { teamId: 2, name: "Dep", logoUrl: null },
    homeScore: 1,
    awayScore: 0,
    status: "Finished",
  } as unknown as MatchResultItemDto;
}

describe("SONUÇLAR · bugün varsayılanı", () => {
  it("ilk gün her zaman bugündür (Europe/Istanbul)", () => {
    expect(pickInitialDay("2026-09-13")).toBe("2026-09-13");
    expect(pickInitialDay()).toBe(istanbulDay());
  });

  it("Istanbul günü UTC gününden ayrılır (21:30Z ertesi gündür)", () => {
    expect(istanbulDay(new Date("2026-09-13T21:30:00Z"))).toBe("2026-09-14");
  });

  it("düne otomatik geçiş kodu yoktur", () => {
    const hook = readFileSync(join(__dirname, "..", "hooks", "useResultDaySelection.ts"), "utf8");
    expect(hook).toContain("setDay(pickInitialDay(istanbulDay()))");
    expect(hook).not.toContain("pickInitialDay(daysWithResults");
  });
});

describe("SONUÇLAR · sıralama", () => {
  it("en son biten maç önce, eşitlikte MatchId azalan", () => {
    const groups = buildResultLeagueGroups([
      item(10, "Bundesliga", "2026-09-13T13:30:00Z"),
      item(12, "Bundesliga", "2026-09-13T13:30:00Z"),
      item(11, "Bundesliga", "2026-09-13T16:30:00Z"),
      item(20, "Premier League", "2026-09-13T15:00:00Z"),
    ]);
    expect(groups.map((g) => g.league)).toEqual(["Bundesliga", "Premier League"]);
    expect(groups[0].results.map((r) => r.matchId)).toEqual([11, 12, 10]);
  });
});

describe("SONUÇLAR · kontrollü tazeleme", () => {
  it("yalnız bugün tazelenir, geçmiş gün tazelenmez", () => {
    const today = "2026-09-13";
    expect(resultsRefreshInterval(today, today)).toBe(TODAY_RESULTS_REFRESH_MS);
    expect(resultsRefreshInterval(shiftDay(today, -1), today)).toBe(false);
    expect(resultsRefreshInterval(null, today)).toBe(false);
  });
});
