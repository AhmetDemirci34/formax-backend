"use client";

import { useCallback, useSyncExternalStore } from "react";

/**
 * Tercih anahtarı — içerik bazlı (FORMAX bildirim felsefesi: event tipi YOK).
 *   "match:123" · "team:45" · "league:7" · "formax:ai-combo" · "formax:system"
 */
export type NotificationPrefKey = string;

const STORAGE_KEY = "formax_notification_prefs";

/**
 * Bildirim tercihleri — içerik bazlı aç/kapat, toggle değişince otomatik kayıt.
 *
 * TODO(backend): Bildirim tercihi ucu YOK. `NotificationsController` yalnızca
 * `GET me`, `GET me/unread-count`, `POST {id}/read`, `POST read-all` sunar;
 * tercih/sessiz saat için tablo, DTO veya uç tanımlı değil. Uç eklendiğinde
 * (ör. `GET/PATCH /api/notifications/me/preferences`) okuma/yazma buraya
 * bağlanacak — ekranda değişiklik gerekmez.
 *
 * Uç gelene kadar tercihler cihazda saklanır (projede yerleşik desen:
 * `formax_predictions`). Bu bir mock veri DEĞİLDİR; kullanıcının kendi
 * seçimidir, yalnızca sunucuya taşınamıyor.
 */

function read(): Record<string, boolean> {
  if (typeof window === "undefined") return {};
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as Record<string, boolean>) : {};
  } catch {
    return {};
  }
}

let cache: Record<string, boolean> | null = null;
const listeners = new Set<() => void>();

function getSnapshot(): Record<string, boolean> {
  if (cache === null) cache = read();
  return cache;
}
const getServerSnapshot = (): Record<string, boolean> => ({});

function subscribe(cb: () => void): () => void {
  listeners.add(cb);
  return () => listeners.delete(cb);
}

function write(next: Record<string, boolean>) {
  cache = next;
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Depolama kullanılamıyorsa tercih yalnızca bu oturumda geçerli olur.
  }
  listeners.forEach((l) => l());
}

export interface NotificationPreferences {
  /** Kayıtlı tercih yoksa varsayılan AÇIK'tır (takip edilen içerik bildirim gönderir). */
  isEnabled: (key: NotificationPrefKey) => boolean;
  /** Toggle değiştiği anda çağrılır; Kaydet butonu yoktur. */
  setEnabled: (key: NotificationPrefKey, value: boolean) => void;
}

export function useNotificationPreferences(): NotificationPreferences {
  const prefs = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);

  const isEnabled = useCallback(
    (key: NotificationPrefKey) => prefs[key] ?? true,
    [prefs]
  );

  const setEnabled = useCallback((key: NotificationPrefKey, value: boolean) => {
    write({ ...getSnapshot(), [key]: value });
  }, []);

  return { isEnabled, setEnabled };
}
