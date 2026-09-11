// FORMAX · YouTube gömülü oynatıcısının GERÇEK durumu — saf fonksiyonlar.
//
// NEDEN: iframe'in yüklenmesi videonun oynatılabildiğini KANITLAMAZ. Bölge kısıtlı ya da
// gömmeye kapatılmış bir video da iframe'i "başarıyla" yükler; içinde yalnız bir hata
// kartı durur. Gerçek durum oynatıcının kendi olay kanalından (postMessage) okunur:
//   • infoDelivery.playerState === 1 (oynatılıyor) ya da currentTime > 0 → oynuyor
//   • onError → oynatılamaz (kod: 100 kaldırılmış/gizli, 101/150/152 gömme ya da
//     bölge engeli, 2 geçersiz, 5 HTML5 oynatıcı hatası)
// Engel dolanılmaz: hata gelirse oynatıcı KALDIRILIR ve kullanıcıya dürüst cümle +
// resmî kaynak bağlantısı gösterilir.

export type PlaybackState = "idle" | "loading" | "playing" | "error";

export type PlayerSignal =
  | { kind: "ready" }
  | { kind: "state"; state: number }
  | { kind: "time"; currentTime: number }
  | { kind: "error"; code: number }
  | { kind: "other" };

/** YouTube'un gömme kökenleri — başka kökenden gelen mesaj YOK SAYILIR. */
export const YOUTUBE_ORIGINS = new Set([
  "https://www.youtube-nocookie.com",
  "https://www.youtube.com",
]);

/** Oynatıcı mesajını tek bir sinyale indirger. Tanınmayan her şey "other". */
export function parsePlayerMessage(data: unknown): PlayerSignal[] {
  let msg: unknown = data;
  if (typeof data === "string") {
    try {
      msg = JSON.parse(data);
    } catch {
      return [{ kind: "other" }];
    }
  }
  if (!msg || typeof msg !== "object") return [{ kind: "other" }];
  const m = msg as { event?: string; info?: unknown };

  if (m.event === "onReady") return [{ kind: "ready" }];
  if (m.event === "onError") {
    const code = typeof m.info === "number" ? m.info : Number(m.info);
    return [{ kind: "error", code: Number.isFinite(code) ? code : -1 }];
  }
  if (m.event === "onStateChange" && typeof m.info === "number") {
    return [{ kind: "state", state: m.info }];
  }
  if ((m.event === "infoDelivery" || m.event === "initialDelivery") && m.info && typeof m.info === "object") {
    const info = m.info as { playerState?: unknown; currentTime?: unknown };
    const out: PlayerSignal[] = [];
    if (typeof info.playerState === "number") out.push({ kind: "state", state: info.playerState });
    if (typeof info.currentTime === "number") out.push({ kind: "time", currentTime: info.currentTime });
    return out.length ? out : [{ kind: "other" }];
  }
  return [{ kind: "other" }];
}

/** Mevcut durumdan ve yeni sinyalden bir sonraki durum. Hata kalıcıdır. */
export function nextPlaybackState(current: PlaybackState, signal: PlayerSignal): PlaybackState {
  if (current === "error") return "error";
  switch (signal.kind) {
    case "error":
      return "error";
    case "state":
      return signal.state === 1 ? "playing" : current;
    case "time":
      return signal.currentTime > 0 ? "playing" : current;
    default:
      return current;
  }
}

/**
 * Hata kodunun kullanıcı cümlesi. Bölge kısıtı biliniyorsa (backend AvailableCountries)
 * gömme/bölge kodları "bu bölgede oynatılamıyor" olarak söylenir — "oynatılamıyor" ile
 * "SENİN BÖLGENDE oynatılamıyor" aynı şey değildir.
 */
export function playbackErrorText(
  code: number,
  video: { isRegionRestricted?: boolean; availableCountries?: string[] | null }
): string {
  const countries = video.availableCountries ?? [];
  const embedOrRegion = code === 101 || code === 150 || code === 152;
  if (embedOrRegion && video.isRegionRestricted && countries.length > 0) {
    return `Bu resmî video bulunduğunuz bölgede oynatılamıyor (yalnız ${countries.join(", ")}).`;
  }
  if (embedOrRegion) return "Yayıncı bu videonun uygulama içinde oynatılmasına izin vermiyor.";
  if (code === 100) return "Bu video resmî kaynakta artık erişilebilir değil.";
  return "Video şu an oynatılamıyor.";
}

/** Gömme adresine oynatıcı olay kanalını açan parametreleri ekler. */
export function buildEmbedSrc(embedUrl: string, origin: string): string {
  const sep = embedUrl.includes("?") ? "&" : "?";
  return (
    `${embedUrl}${sep}rel=0&modestbranding=1&playsinline=1&autoplay=1` +
    `&enablejsapi=1&origin=${encodeURIComponent(origin)}`
  );
}
