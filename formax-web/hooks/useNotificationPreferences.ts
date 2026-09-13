"use client";

import { useCallback, useEffect, useSyncExternalStore } from "react";
import { getNotificationPreferences, putNotificationPreference } from "@/lib/api/notifications";

/**
 * Tercih anahtarı — içerik bazlı (FORMAX bildirim felsefesi: event tipi YOK).
 *   "match:123" · "team:45" · "league:7" · "formax:ai-combo" · "formax:system"
 */
export type NotificationPrefKey = string;

const STORAGE_KEY = "formax_notification_prefs";

/**
 * Bildirim tercihleri — içerik bazlı aç/kapat, toggle değişince otomatik kayıt.
 *
 * SUNUCU: `GET/PUT /api/notifications/me/preferences`. Kadro ve kritik gelişme
 * bildirimlerini üreten job'lar göndermeden önce SUNUCUDAKİ tercihe bakar; bu yüzden
 * toggle değişince tercih sunucuya da yazılır. Oturum yoksa (401) yalnız cihazda kalır.
 * Açılışta sunucudaki tercih cihazdakinin üzerine yazılır (tek gerçek sunucudur).
 * Sessiz saatler hâlâ yalnız cihazdadır (sunucu ucu yok).
 */

/** Oturum var mı? (token yoksa sunucuya boşuna 401 isteği atılmaz) */
function hasSession(): boolean {
  try {
    return typeof window !== "undefined" && !!window.localStorage.getItem("formax_token");
  } catch {
    return false;
  }
}

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

  // Sunucudaki tercihler bir kez okunur; oturum yoksa sessizce cihazdaki kalır.
  useEffect(() => {
    if (!hasSession()) return;
    let cancelled = false;
    getNotificationPreferences()
      .then((rows) => {
        if (cancelled || rows.length === 0) return;
        const merged = { ...getSnapshot() };
        for (const r of rows) merged[r.key] = r.enabled;
        write(merged);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  const setEnabled = useCallback((key: NotificationPrefKey, value: boolean) => {
    write({ ...getSnapshot(), [key]: value });
    if (!hasSession()) return;
    putNotificationPreference(key, value).catch(() => {
      // Oturum yok / ağ hatası: tercih cihazda kalır.
    });
  }, []);

  return { isEnabled, setEnabled };
}
