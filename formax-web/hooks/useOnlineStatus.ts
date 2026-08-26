"use client";

import { useSyncExternalStore } from "react";

function subscribe(onChange: () => void): () => void {
  window.addEventListener("online", onChange);
  window.addEventListener("offline", onChange);
  return () => {
    window.removeEventListener("online", onChange);
    window.removeEventListener("offline", onChange);
  };
}

const getSnapshot = () => navigator.onLine;
/** SSR anlık görüntüsü — sunucuda bağlantı daima "var" kabul edilir. */
const getServerSnapshot = () => true;

/**
 * Tarayıcı bağlantı durumu — Interaction Spec §6 (`isOffline`).
 * Harici bir kaynağa abone olunduğu için `useSyncExternalStore` kullanılır;
 * hidrasyon uyumsuzluğu ve zincirleme render oluşmaz.
 */
export function useOnlineStatus(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
