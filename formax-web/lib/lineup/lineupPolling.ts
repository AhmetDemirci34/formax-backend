// FORMAX · Açık maç ekranında kadro yoklaması — saf mantık (test edilir).
//
// NEDEN (11.09.2026, Venezia–Fiorentina): maç detayı açılışta bir kez yükleniyordu
// (refetchInterval: false). Kadro sonradan DB'ye gelse bile açık ekran eski "kadro yok"
// yanıtında ve eski "Son kontrol" saatinde kalıyordu.
//
// KURALLAR
//  • Yalnız FORMAX backend'inin mevcut maç-detay ucu okunur (DB). Frontend sağlayıcıya
//    (API-Football) HİÇ çıkmaz.
//  • Kickoff'a 90 dakikadan az kaldığında ve doğrulanmış kadro yokken çalışır.
//  • 30 saniyede bir; önceki okuma bitmeden yenisi başlamaz (duplicate istek YOK).
//  • Kadro gelince, maç bitince ya da kickoff'tan 10 dakika sonra durur.
//  • Sayfa kapanınca zamanlayıcı temizlenir (stop).

import type { LineupSectionDto } from "@/types/api";
import { hasVerifiedLineup } from "./lineupStatus";

export const LINEUP_POLL_INTERVAL_MS = 30_000;
export const LINEUP_POLL_WINDOW_MS = 90 * 60_000;
export const LINEUP_POLL_GRACE_MS = 10 * 60_000;

export interface LineupPollInput {
  lineup?: Partial<LineupSectionDto> | null;
  matchDate?: string | null;
  status?: string | null;
}

/** Açık ekran şu an kadro için backend'i yeniden okumalı mı? */
export function shouldPollLineup(input: LineupPollInput | null | undefined, nowMs: number): boolean {
  if (!input) return false;
  if (hasVerifiedLineup(input.lineup)) return false;
  if ((input.status ?? "").toLowerCase() === "finished") return false;

  const kickoff = Date.parse(input.matchDate ?? "");
  if (Number.isNaN(kickoff)) return false;

  const remaining = kickoff - nowMs;
  if (remaining > LINEUP_POLL_WINDOW_MS) return false;   // T−90'dan önce: yok
  if (remaining < -LINEUP_POLL_GRACE_MS) return false;   // kickoff+10'dan sonra: dur
  return true;
}

/** T−90'a kadar beklenecek süre (ms); zaten penceredeyse ya da geçtiyse null. */
export function msUntilPollWindow(input: LineupPollInput | null | undefined, nowMs: number): number | null {
  const kickoff = Date.parse(input?.matchDate ?? "");
  if (Number.isNaN(kickoff) || hasVerifiedLineup(input?.lineup)) return null;
  const wait = kickoff - LINEUP_POLL_WINDOW_MS - nowMs;
  return wait > 0 ? wait : null;
}

type TimerHandle = unknown;

export interface LineupPollerDeps<T extends LineupPollInput> {
  /** Backend'den (DB) güncel maç detayını okur. */
  refresh: () => Promise<T | undefined>;
  now: () => number;
  setTimer: (fn: () => void, ms: number) => TimerHandle;
  clearTimer: (handle: TimerHandle) => void;
  intervalMs?: number;
}

export interface LineupPoller {
  start(): void;
  stop(): void;
  isRunning(): boolean;
  /** Bir tur — test ve zamanlayıcı buradan çağırır. */
  tick(): Promise<void>;
}

export function createLineupPoller<T extends LineupPollInput>(deps: LineupPollerDeps<T>): LineupPoller {
  const interval = deps.intervalMs ?? LINEUP_POLL_INTERVAL_MS;
  let running = false;
  let inFlight = false;
  let handle: TimerHandle | null = null;

  const schedule = () => {
    if (!running) return;
    handle = deps.setTimer(() => {
      handle = null;
      void tick();
    }, interval);
  };

  async function tick(): Promise<void> {
    if (!running) return;
    // DUPLICATE YOK: önceki okuma sürüyorsa yeni istek atılmaz.
    if (inFlight) return;
    inFlight = true;
    let latest: T | undefined;
    try {
      latest = await deps.refresh();
    } catch {
      latest = undefined; // geçici hata: bir sonraki turda yeniden okunur
    } finally {
      inFlight = false;
    }
    if (!running) return;
    if (latest && !shouldPollLineup(latest, deps.now())) {
      stop();
      return;
    }
    schedule();
  }

  function stop() {
    running = false;
    if (handle !== null) {
      deps.clearTimer(handle);
      handle = null;
    }
  }

  return {
    start() {
      if (running) return;
      running = true;
      schedule();
    },
    stop,
    isRunning: () => running,
    tick,
  };
}
