"use client";

import { useEffect, useRef, useState } from "react";
import type { MatchVideoDto } from "@/types/api";
import {
  YOUTUBE_ORIGINS,
  buildEmbedSrc,
  nextPlaybackState,
  parsePlayerMessage,
  playbackErrorText,
  type PlaybackState,
} from "@/lib/video/youtubePlayback";
import { reportPlaybackError } from "@/lib/video/playbackReport";

export const VIDEO_TYPE_LABEL: Record<string, string> = {
  MatchHighlights: "Maç Özeti",
  ExtendedHighlights: "Uzun Özet",
  Goal: "Gol",
  Penalty: "Penaltı",
  RedCard: "Kırmızı Kart",
  VAR: "VAR Kararı",
  ImportantMoment: "Önemli An",
};

/**
 * VİDEO KARTI — poster + tıklayınca AÇILAN oynatıcı.
 *
 *  • canPlayInApp false ise oynatıcı HİÇ kurulmaz; sebep yazılır, resmî kaynağa gidilir.
 *  • Oynatıcı açıldığında GERÇEK durumu YouTube'un olay kanalından okunur
 *    (lib/video/youtubePlayback). iframe'in yüklenmesi "oynuyor" SAYILMAZ.
 *  • Oynatıcı hata bildirirse (bölge/gömme engeli, kaldırılmış video) iframe KALDIRILIR;
 *    boş siyah kutu ya da içinde hata kartı olan "aktif" bir player bırakılmaz.
 *  • Bölgesel kısıt ayrı bir cümledir: "oynatılamıyor" ≠ "senin bölgende oynatılamıyor".
 */
export function VideoPlayerCard({
  video,
  compact = false,
  onPlaybackError,
  autoStart = false,
  matchId,
}: {
  video: MatchVideoDto;
  compact?: boolean;
  /** Oynatıcı hatası backend'e bu maç için bildirilir (kayıt SourceBlocked, maç yeniden kuyruğa). */
  matchId?: number;
  /** Oynatıcı hata kodu bildirdiğinde (ör. 150) üst bileşene haber verir. */
  onPlaybackError?: (code: number) => void;
  /** Kullanıcı zaten oynat'a bastıysa yedek adayda poster atlanır. */
  autoStart?: boolean;
}) {
  const [state, setState] = useState<PlaybackState>(autoStart ? "loading" : "idle");
  const [errorCode, setErrorCode] = useState<number | null>(null);
  const frameRef = useRef<HTMLIFrameElement | null>(null);
  const label = VIDEO_TYPE_LABEL[video.videoType] ?? video.videoType;
  const countries = video.availableCountries ?? [];

  // Oynatıcının olay kanalı — yalnız YouTube kökeninden ve YALNIZ bu iframe'den gelen mesaj.
  useEffect(() => {
    if (state === "idle" || state === "error") return;
    const onMessage = (ev: MessageEvent) => {
      if (!YOUTUBE_ORIGINS.has(ev.origin)) return;
      if (frameRef.current && ev.source !== frameRef.current.contentWindow) return;
      for (const s of parsePlayerMessage(ev.data)) {
        if (s.kind === "error") {
          setErrorCode(s.code);
          if (matchId) void reportPlaybackError(matchId, video, s.code);
          onPlaybackError?.(s.code);
        }
        setState((cur) => nextPlaybackState(cur, s));
      }
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, [state, onPlaybackError, matchId, video]);

  /** iframe yüklendiğinde oynatıcıya "olayları bana bildir" denir (IFrame API protokolü). */
  const subscribe = () => {
    const win = frameRef.current?.contentWindow;
    if (!win || !video.embedUrl) return;
    const target = new URL(video.embedUrl).origin;
    win.postMessage(JSON.stringify({ event: "listening", id: 1, channel: "widget" }), target);
    for (const ev of ["onReady", "onStateChange", "onError"]) {
      win.postMessage(
        JSON.stringify({ event: "command", func: "addEventListener", args: [ev], id: 1, channel: "widget" }),
        target
      );
    }
  };

  if (!video.canPlayInApp) {
    return (
      <div className="w-full max-w-full rounded-xl border border-goalai-border/70 bg-black/20 p-3">
        <p className="line-clamp-2 text-[12px] font-semibold leading-tight text-text-primary">{video.title}</p>
        <p className="mt-1.5 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[10px] text-text-muted">
          <SourceBadge publisher={video.publisher} />
          <span>{label}</span>
        </p>
        {/* Dürüstlük: bu video VAR ama uygulama içinde oynatılamıyor. */}
        <p className="mt-1.5 text-[11px] leading-relaxed text-white/75">Uygulama içinde oynatılamıyor.</p>
        <ExternalSourceLink url={video.sourcePageUrl} />
      </div>
    );
  }

  return (
    <div
      className="w-full max-w-full overflow-hidden rounded-xl border border-goalai-border/70 bg-black/30"
      data-playback={state === "error" ? `error:${errorCode ?? -1}` : state}
    >
      {/* 16:9 — sabit piksel yok, 375px'te taşmaz. */}
      <div className="relative aspect-video w-full max-w-full bg-black">
        {state === "error" ? (
          // Oynatıcı hata bildirdi: player KALDIRILDI, dürüst cümle + resmî kaynak.
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 px-5 text-center">
            <p className="text-[12.5px] leading-relaxed text-white/85" role="status">
              {playbackErrorText(errorCode ?? -1, video)}
            </p>
            <ExternalSourceLink url={video.sourcePageUrl} />
          </div>
        ) : state !== "idle" && video.embedUrl ? (
          <iframe
            ref={frameRef}
            src={buildEmbedSrc(video.embedUrl, typeof window === "undefined" ? "" : window.location.origin)}
            title={video.title}
            // autoplay: kullanıcı posterdeki oynat düğmesine bastıktan sonra oynatıcının
            // İKİNCİ bir dokunuş istemeden başlayabilmesi için gerekir.
            allow="accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture; fullscreen"
            allowFullScreen
            onLoad={subscribe}
            className="absolute inset-0 h-full w-full border-0"
          />
        ) : (
          <button
            type="button"
            onClick={() => setState("loading")}
            aria-label={`${video.title} — oynat`}
            className="absolute inset-0 flex h-full w-full items-center justify-center"
          >
            {video.thumbnailUrl && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={video.thumbnailUrl}
                alt=""
                loading="lazy"
                className="absolute inset-0 h-full w-full object-cover opacity-85"
              />
            )}
            <span className="relative flex h-14 w-14 items-center justify-center rounded-full bg-black/70 ring-1 ring-goalai-accent/40">
              <span className="ml-[4px] border-y-[10px] border-l-[17px] border-y-transparent border-l-goalai-accent" />
            </span>
            {video.durationSeconds != null && (
              <span className="absolute bottom-2 right-2 rounded bg-black/80 px-1.5 py-0.5 text-[10px] font-semibold tabular-nums text-white">
                {formatDuration(video.durationSeconds)}
              </span>
            )}
          </button>
        )}
      </div>

      <div className="flex flex-col gap-1 px-3 py-2.5">
        {/* Olay klibiyse dakika ve oyuncu/takım — YALNIZ kaynakta varsa. */}
        {(video.eventMinute != null || video.eventPlayer || video.eventTeam) && (
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-text-muted">
            {video.eventMinute != null && (
              <span className="whitespace-nowrap font-bold tabular-nums text-goalai-accent">
                {video.eventMinute}
                {video.eventExtraMinute ? `+${video.eventExtraMinute}` : ""}&apos;
              </span>
            )}
            <span className="truncate">
              {video.eventPlayer ?? ""}
              {video.eventPlayer && video.eventTeam ? " · " : ""}
              {video.eventTeam ?? ""}
            </span>
          </p>
        )}

        <p
          className={`${compact ? "line-clamp-2" : "line-clamp-2"} break-words text-[12px] font-semibold leading-tight text-text-primary`}
        >
          {video.title}
        </p>

        <p className="flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[10px] text-text-muted">
          <SourceBadge publisher={video.publisher} />
          <span className="truncate">{label}</span>
          {video.durationSeconds != null && (
            <span className="whitespace-nowrap tabular-nums">{formatDuration(video.durationSeconds)}</span>
          )}
        </p>

        {/* BÖLGESEL KISIT — oynatmadan ÖNCE de açıkça söylenir. Korsan alternatif YOK. */}
        {video.isRegionRestricted && countries.length > 0 && state !== "error" && (
          <p className="text-[10.5px] leading-relaxed text-white/70">
            Bu resmî video yalnız {countries.join(", ")} bölgesinde oynatılabilir. Bulunduğunuz
            bölge dışındaysa oynatılamaz.
          </p>
        )}
      </div>
    </div>
  );
}

export function ExternalSourceLink({ url }: { url: string }) {
  if (!url) return null;
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      className="mt-2 inline-block rounded-md border border-goalai-border px-2.5 py-1 text-[10px] font-semibold text-text-primary active:opacity-70"
    >
      Resmî kaynakta izle
    </a>
  );
}

/** Kaynak adı + "Resmî kaynak" etiketi. Bu etiket yalnız izin listesindeki yayıncıya çıkar. */
export function SourceBadge({ publisher }: { publisher: string }) {
  return (
    <span className="flex items-center gap-1">
      <span className="max-w-[120px] truncate rounded bg-white/[0.06] px-1.5 py-0.5 font-semibold text-text-primary">
        {publisher}
      </span>
      <span className="whitespace-nowrap rounded bg-goalai-accent/15 px-1.5 py-0.5 font-semibold text-goalai-accent">
        Resmî kaynak
      </span>
    </span>
  );
}

export function formatDuration(seconds: number) {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${String(s).padStart(2, "0")}`;
}
