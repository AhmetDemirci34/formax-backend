"use client";

import { useCallback, useSyncExternalStore } from "react";

export interface QuietHoursState {
  enabled: boolean;
  /** Başlangıç saati "HH:mm". */
  start: string;
  /** Bitiş saati "HH:mm". */
  end: string;
}

const STORAGE_KEY = "formax_quiet_hours";

/**
 * Varsayılanlar:
 *   • enabled: false — bildirimleri susturan bir özellik, kullanıcı onayı
 *     olmadan AÇIK başlatılmaz.
 *   • start/end: Stitch'te gösterilen saatler (22:00–08:00). Bu değerler
 *     yer tutucu DEĞİL, zaman seçicinin ihtiyaç duyduğu başlangıç konfigürasyonu
 *     ve tek doğruluk kaynağı olan görseldeki değerlerdir.
 */
const DEFAULT: QuietHoursState = { enabled: false, start: "22:00", end: "08:00" };

/**
 * Sessiz Saatler tercihi (SCREEN_07) — toggle/saat değişince otomatik kaydeder,
 * Kaydet butonu yoktur.
 *
 * TODO(backend): Sessiz saat ucu YOK. NotificationsController yalnızca
 * me / unread-count / read / read-all sunar; sessiz saat tablosu, DTO'su veya
 * uç tanımlı değil. Uç eklendiğinde (ör.
 * `GET/PATCH /api/notifications/me/quiet-hours` → { enabled, start, end })
 * okuma/yazma buraya bağlanacak — ekranda değişiklik gerekmez.
 *
 * Uç gelene kadar tercih cihazda saklanır (projedeki yerleşik desen:
 * `formax_notification_prefs`, `formax_predictions`). Bu bir mock veri DEĞİLDİR;
 * kullanıcının kendi seçimidir, yalnızca sunucuya taşınamıyor.
 */

function read(): QuietHoursState {
  if (typeof window === "undefined") return DEFAULT;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT;
    return { ...DEFAULT, ...(JSON.parse(raw) as Partial<QuietHoursState>) };
  } catch {
    return DEFAULT;
  }
}

let cache: QuietHoursState | null = null;
const listeners = new Set<() => void>();

function getSnapshot(): QuietHoursState {
  if (cache === null) cache = read();
  return cache;
}
const getServerSnapshot = (): QuietHoursState => DEFAULT;

function subscribe(cb: () => void): () => void {
  listeners.add(cb);
  return () => listeners.delete(cb);
}

function write(next: QuietHoursState) {
  cache = next;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Depolama kullanılamıyorsa tercih yalnızca bu oturumda geçerli olur.
  }
  listeners.forEach((l) => l());
}

export interface QuietHoursController extends QuietHoursState {
  setEnabled: (value: boolean) => void;
  setStart: (value: string) => void;
  setEnd: (value: string) => void;
}

export function useQuietHours(): QuietHoursController {
  const state = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const setEnabled = useCallback((value: boolean) => {
    write({ ...getSnapshot(), enabled: value });
  }, []);
  const setStart = useCallback((value: string) => {
    write({ ...getSnapshot(), start: value });
  }, []);
  const setEnd = useCallback((value: string) => {
    write({ ...getSnapshot(), end: value });
  }, []);

  return { ...state, setEnabled, setStart, setEnd };
}
