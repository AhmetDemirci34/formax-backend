import { describe, expect, it } from "vitest";
import { readFileSync } from "fs";
import path from "path";
import { renderToStaticMarkup } from "react-dom/server";
import type { LineupPlayerDto, LineupSectionDto, MatchDetailDto } from "@/types/api";
import {
  LINEUP_POLL_INTERVAL_MS,
  createLineupPoller,
  msUntilPollWindow,
  shouldPollLineup,
  type LineupPollInput,
} from "@/lib/lineup/lineupPolling";
import { LINEUP_TEXT_SOURCE_DELAYED, lineupWaitingText } from "@/lib/lineup/lineupStatus";
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";

// Venezia–Fiorentina, 11.09.2026 21:45 (TR) = 18:45 UTC.
const KICKOFF = Date.parse("2026-09-11T18:45:00Z");
const at = (minutesFromKickoff: number) => KICKOFF + minutesFromKickoff * 60_000;

function player(n: number, name: string): LineupPlayerDto {
  return { shirtNumber: n, playerName: name, position: "M", grid: `2:${n}`, isCaptain: false };
}

function lineup(verified: boolean, extra: Partial<LineupSectionDto> = {}): LineupSectionDto {
  return {
    lineupsAnnounced: verified,
    homeFormation: verified ? "3-5-2" : null,
    awayFormation: verified ? "4-3-3" : null,
    homeStartingXI: verified ? [player(1, "Venezia GK")] : [],
    homeBench: verified ? [player(12, "Venezia Yedek")] : [],
    awayStartingXI: verified ? [player(1, "Fiorentina GK")] : [],
    awayBench: [],
    ...extra,
  };
}

function detail(verified: boolean, status = "NotStarted"): LineupPollInput & { matchId: number } {
  return { matchId: 15383, matchDate: "2026-09-11T18:45:00Z", status, lineup: lineup(verified) };
}

/** Elle ilerletilen zamanlayıcı — gerçek saat/ağ yok. */
function fakeTimers() {
  let seq = 0;
  const pending = new Map<number, { fn: () => void; ms: number }>();
  return {
    pending,
    setTimer: (fn: () => void, ms: number) => {
      const id = ++seq;
      pending.set(id, { fn, ms });
      return id;
    },
    clearTimer: (h: unknown) => {
      pending.delete(h as number);
    },
    /** Bekleyen tek zamanlayıcıyı çalıştırır. */
    fire: () => {
      const [id, t] = [...pending.entries()][0];
      pending.delete(id);
      t.fn();
    },
  };
}

const flush = () => new Promise((r) => setTimeout(r, 0));

describe("kadro yoklama penceresi", () => {
  it("kickoff'a 90 dk'dan az kaldığında ve kadro yokken çalışır", () => {
    expect(shouldPollLineup(detail(false), at(-91))).toBe(false);
    expect(shouldPollLineup(detail(false), at(-89))).toBe(true);
    expect(shouldPollLineup(detail(false), at(-13))).toBe(true);
  });

  it("kickoff'tan 10 dk sonra durur", () => {
    expect(shouldPollLineup(detail(false), at(9))).toBe(true);
    expect(shouldPollLineup(detail(false), at(11))).toBe(false);
  });

  it("doğrulanmış kadro varsa ya da maç bittiyse çalışmaz", () => {
    expect(shouldPollLineup(detail(true), at(-13))).toBe(false);
    expect(shouldPollLineup(detail(false, "Finished"), at(-13))).toBe(false);
  });

  it("T−90'a kadar beklenecek süreyi verir", () => {
    expect(msUntilPollWindow(detail(false), at(-100))).toBe(10 * 60_000);
    expect(msUntilPollWindow(detail(false), at(-60))).toBeNull();
  });
});

describe("DB-only poller", () => {
  it("30 sn aralıkla yalnız backend'i (refresh) okur; kadro gelince durur", async () => {
    const timers = fakeTimers();
    let calls = 0;
    const responses = [detail(false), detail(true)];
    const poller = createLineupPoller({
      refresh: async () => responses[Math.min(calls++, responses.length - 1)],
      now: () => at(-13),
      setTimer: timers.setTimer,
      clearTimer: timers.clearTimer,
    });

    poller.start();
    expect([...timers.pending.values()][0].ms).toBe(LINEUP_POLL_INTERVAL_MS);

    timers.fire();
    await flush();
    expect(calls).toBe(1);
    expect(poller.isRunning()).toBe(true);     // hâlâ kadro yok → yeniden kuruldu
    expect(timers.pending.size).toBe(1);

    timers.fire();
    await flush();
    expect(calls).toBe(2);
    expect(poller.isRunning()).toBe(false);    // kadro geldi → durdu
    expect(timers.pending.size).toBe(0);
  });

  it("sayfa kapanınca (stop) zamanlayıcı temizlenir ve istek atılmaz", async () => {
    const timers = fakeTimers();
    let calls = 0;
    const poller = createLineupPoller({
      refresh: async () => {
        calls++;
        return detail(false);
      },
      now: () => at(-13),
      setTimer: timers.setTimer,
      clearTimer: timers.clearTimer,
    });
    poller.start();
    expect(timers.pending.size).toBe(1);

    poller.stop();
    expect(timers.pending.size).toBe(0);
    await poller.tick();
    expect(calls).toBe(0);
  });

  it("önceki okuma bitmeden duplicate detail isteği oluşmaz", async () => {
    const timers = fakeTimers();
    let calls = 0;
    let release: (v: LineupPollInput) => void = () => {};
    const poller = createLineupPoller({
      refresh: () => {
        calls++;
        return new Promise<LineupPollInput>((r) => (release = r));
      },
      now: () => at(-13),
      setTimer: timers.setTimer,
      clearTimer: timers.clearTimer,
    });
    poller.start();

    timers.fire();                         // 30 sn doldu → okuma başladı (sürüyor)
    expect(calls).toBe(1);
    await poller.tick();                   // okuma sürerken ikinci tur denemesi
    expect(calls).toBe(1);                 // duplicate istek YOK
    expect(timers.pending.size).toBe(0);   // okuma bitmeden yeni zamanlayıcı da yok

    release(detail(false));
    await flush();
    expect(calls).toBe(1);
    expect(timers.pending.size).toBe(1);   // okuma bitti → tek zamanlayıcı
    poller.stop();
  });

  it("kickoff+10 geçince kendiliğinden durur", async () => {
    const timers = fakeTimers();
    const poller = createLineupPoller({
      refresh: async () => detail(false),
      now: () => at(11),
      setTimer: timers.setTimer,
      clearTimer: timers.clearTimer,
    });
    poller.start();
    timers.fire();
    await flush();
    expect(poller.isRunning()).toBe(false);
    expect(timers.pending.size).toBe(0);
  });

  it("frontend doğrudan API-Football'a çıkmaz", () => {
    for (const f of ["lib/lineup/lineupPolling.ts", "hooks/useLineupAutoRefresh.ts"]) {
      const src = readFileSync(path.resolve(__dirname, "..", f), "utf8");
      expect(src).not.toMatch(/api-sports|football\.api|fixtures\/lineups|x-apisports-key/i);
      expect(src).not.toMatch(/\bfetch\(|axios/);
    }
  });
});

describe("kadro metni ve başlamış maç", () => {
  it("SourceDelayed: 'yayımlanmadı' denmez, kontrollerin sürdüğü söylenir", () => {
    expect(lineupWaitingText({ status: "SourceDelayed" })).toBe(LINEUP_TEXT_SOURCE_DELAYED);
    expect(lineupWaitingText({ pollingWindowOpen: true })).toBe(LINEUP_TEXT_SOURCE_DELAYED);
    expect(LINEUP_TEXT_SOURCE_DELAYED).toBe("FORMAX veri kaynağı resmî kadroyu henüz iletmedi. Kontroller sürüyor.");
    expect(lineupWaitingText({ status: "SourceDelayed" })).not.toMatch(/yayımlanmadı/);
  });

  it("başlamış maçta DB'deki doğrulanmış kadro gösterilir", () => {
    const m = {
      matchId: 15383,
      status: "NotStarted",
      matchDate: "2026-09-11T18:45:00Z",
      homeTeam: { name: "Venezia", logoUrl: null },
      awayTeam: { name: "Fiorentina", logoUrl: null },
      lineup: lineup(true, { kickoffPassed: true, status: "Released", lastCheckedUtc: "2026-09-11T18:40:00Z" }),
    } as unknown as MatchDetailDto;

    const html = renderToStaticMarkup(<LineupPanel match={m} />);
    expect(html).toContain('data-lineup-state="verified"');
    expect(html).toContain("3-5-2");
    expect(html).toContain("4-3-3");
    expect(html).toContain("Venezia Yedek");
    expect(html).toMatch(/Son kontrol: \d{2}:\d{2}/);
  });
});
