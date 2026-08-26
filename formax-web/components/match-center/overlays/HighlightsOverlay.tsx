"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import type {
  MatchDetailDto,
  MatchHighlightMomentDto,
  MatchHighlightVideoDto,
  MatchHighlightsDto,
} from "@/types/api";
import { useMatchHighlights } from "@/hooks/useMatchHighlights";
import { CloseIcon } from "../icons";

/**
 * ÖNEMLİ ANLAR — "Önemli Anları İzle" paneli.
 *
 * AI MAÇ ANALİZİ İLE İLİŞKİSİ YOKTUR: ayrı uç (`/highlights`), ayrı sorgu, ayrı içerik.
 *
 * DIŞ SİTEYE OTOMATİK YÖNLENDİRME YOKTUR. Video FORMAX içindeki panelde, platformun
 * kendi izin verdiği embed player'ı ile oynatılır. Kaynak embed'e izin vermiyorsa video
 * indirilmez/yeniden yayınlanmaz — dürüst durum gösterilir ve kaynağa gitmek yalnızca
 * kullanıcının açıkça tıklayacağı İKİNCİL seçenek olarak kalır.
 *
 * İLERLEME: gerçek yüzde ölçülemediği için SAHTE progress animasyonu yapılmaz;
 * belirsiz (indeterminate) yükleme çubuğu kullanılır.
 */
export function HighlightsOverlay({
  match,
  onClose,
}: {
  match: MatchDetailDto;
  onClose: () => void;
}) {
  const { data, isLoading, isError, refetch } = useMatchHighlights(match.matchId, true);
  const [playing, setPlaying] = useState<MatchHighlightVideoDto | null>(null);

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.25 }}
      className="absolute inset-0 z-40 flex items-end bg-black/60 backdrop-blur-sm"
      onClick={onClose}
    >
      <motion.section
        initial={{ y: 40, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        exit={{ y: 40, opacity: 0 }}
        transition={{ duration: 0.28, ease: "easeOut" }}
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[82%] w-full flex-col rounded-t-3xl border-t border-goalai-border bg-goalai-surface px-4 pb-6 pt-4"
      >
        <div className="mb-3 flex shrink-0 items-center justify-between">
          <h2 className="text-lg font-bold uppercase tracking-wide text-white">Önemli Anlar</h2>
          <button
            type="button"
            onClick={playing ? () => setPlaying(null) : onClose}
            aria-label={playing ? "Videoyu kapat" : "Kapat"}
            className="flex h-8 w-8 items-center justify-center rounded-full border border-goalai-border text-white/70 transition-colors hover:bg-white/10 hover:text-white active:scale-95"
          >
            <CloseIcon size={18} />
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto pr-0.5">
          {playing ? (
            <VideoPlayer video={playing} onBack={() => setPlaying(null)} />
          ) : isLoading ? (
            <AnalyzingState />
          ) : isError ? (
            <EmptyState
              text="Önemli anlar şu an yüklenemiyor."
              action={{ label: "Tekrar dene", onClick: () => refetch() }}
            />
          ) : data ? (
            <HighlightsBody data={data} onPlay={setPlaying} />
          ) : null}
        </div>
      </motion.section>
    </motion.div>
  );
}

/**
 * "Analiz ediliyor" durumu — gerçek işlem sürerken gösterilir.
 * SAHTE YÜZDE YOK: ilerleme ölçülemediği için belirsiz (indeterminate) çubuk kullanılır.
 */
function AnalyzingState() {
  return (
    <div className="flex min-h-[200px] flex-col items-center justify-center px-6 text-center">
      <p className="text-[14px] font-semibold text-white">
        Maçın önemli anları analiz ediliyor...
      </p>
      <div className="mt-4 h-1.5 w-48 overflow-hidden rounded-full bg-white/10">
        <motion.div
          className="h-full w-1/3 rounded-full bg-goalai-accent"
          animate={{ x: ["-100%", "300%"] }}
          transition={{ duration: 1.1, repeat: Infinity, ease: "easeInOut" }}
        />
      </div>
    </div>
  );
}

function HighlightsBody({
  data,
  onPlay,
}: {
  data: MatchHighlightsDto;
  onPlay: (v: MatchHighlightVideoDto) => void;
}) {
  if (data.status === "NotStartedYet") {
    return <EmptyState text="Maç başladıktan sonra önemli anlar burada gösterilecektir." />;
  }

  if (data.status === "NoContent") {
    return (
      <EmptyState text="Bu maç için doğrulanmış önemli an veya video içeriği bulunamadı." />
    );
  }

  return (
    <div className="space-y-5 pb-2">
      {data.videos.length > 0 && (
        <section>
          <p className="mb-2 text-[11px] font-bold uppercase tracking-wide text-white/45">
            Video
          </p>
          <div className="space-y-2">
            {data.videos.map((v) => (
              <VideoCard key={v.id} video={v} onPlay={onPlay} />
            ))}
          </div>
        </section>
      )}

      {data.moments.length > 0 && (
        <section>
          <p className="mb-2 text-[11px] font-bold uppercase tracking-wide text-white/45">
            Maçın Anları
          </p>
          <ol className="space-y-1.5">
            {data.moments.map((m, i) => (
              <MomentRow
                key={`${m.minute}-${m.type}-${i}`}
                moment={m}
                video={m.videoId ? data.videos.find((v) => v.id === m.videoId) : undefined}
                onPlay={onPlay}
              />
            ))}
          </ol>
        </section>
      )}

      {data.videos.length === 0 && (
        <p className="px-1 text-[12px] leading-relaxed text-white/40">
          Bu maç için FORMAX&apos;ın izinli kaynaklarında video içerik bulunamadı.
        </p>
      )}
    </div>
  );
}

/** Olay türüne göre işaret — yalnız gerçek olay türünden seçilir, yorum eklemez. */
function momentIcon(type: string, label: string): string {
  const t = type.toLowerCase();
  if (t === "goal") return label.includes("Kaçan") ? "✖" : "⚽";
  if (t === "card") return label.includes("Kırmızı") ? "🟥" : "🟨";
  if (t === "var") return "📺";
  return "•";
}

/**
 * Bir önemli an: DAKİKA / BAŞLIK / KISA AÇIKLAMA / (varsa) VİDEO.
 * Video butonu YALNIZ o ana bağlanmış doğrulanmış bir video varsa çizilir.
 */
function MomentRow({
  moment,
  video,
  onPlay,
}: {
  moment: MatchHighlightMomentDto;
  video?: MatchHighlightVideoDto;
  onPlay: (v: MatchHighlightVideoDto) => void;
}) {
  return (
    <li className="rounded-xl border border-goalai-border bg-goalai-surface-bright px-3 py-2.5">
      <div className="flex items-center gap-3">
        <span className="w-10 shrink-0 text-center font-mono text-[12px] font-bold tabular-nums text-goalai-accent">
          {moment.minuteLabel}{moment.minuteLabel === "PEN" ? "" : "'"}
        </span>
        <span className="min-w-0 flex-1 truncate text-[13px] font-semibold text-white/90">
          {momentIcon(moment.type, moment.label)} {moment.label}
        </span>
        {moment.team && (
          <span className="max-w-[35%] shrink-0 truncate text-[11px] uppercase tracking-wide text-white/40">
            {moment.team}
          </span>
        )}
      </div>

      {moment.description && (
        <p className="mt-1 pl-[52px] text-[12px] leading-relaxed text-white/55">
          {moment.description}
        </p>
      )}

      {video && (
        <button
          type="button"
          onClick={() => onPlay(video)}
          className="ml-[52px] mt-1.5 text-[12px] font-semibold text-goalai-accent underline underline-offset-2"
        >
          Önemli anı izle
        </button>
      )}
    </li>
  );
}

function VideoCard({
  video,
  onPlay,
}: {
  video: MatchHighlightVideoDto;
  onPlay: (v: MatchHighlightVideoDto) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onPlay(video)}
      className="flex w-full items-center gap-3 rounded-xl border border-goalai-border bg-goalai-surface-bright p-2.5 text-left transition-colors hover:bg-white/[0.06] active:scale-[0.99]"
    >
      {video.thumbnailUrl ? (
        // Platformun kendi küçük görseli — kopyalanmaz, kaynaktan gösterilir.
        <img
          src={video.thumbnailUrl}
          alt=""
          className="h-14 w-24 shrink-0 rounded-lg object-cover"
          loading="lazy"
        />
      ) : (
        <span className="flex h-14 w-24 shrink-0 items-center justify-center rounded-lg bg-white/5 text-white/40">
          ▶
        </span>
      )}
      <span className="min-w-0 flex-1">
        <span className="block truncate text-[13px] font-semibold text-white/90">
          {video.minute != null ? `${video.minute}' — ` : ""}
          {video.title}
        </span>
        <span className="mt-0.5 block truncate text-[11px] uppercase tracking-wide text-white/40">
          {video.source} · {video.platform}
        </span>
        {!video.embeddable && (
          <span className="mt-0.5 block text-[11px] text-white/45">
            FORMAX içinde oynatılamıyor
          </span>
        )}
      </span>
    </button>
  );
}

/**
 * FORMAX içi oynatıcı. Embed edilebilen kaynak iframe ile burada oynatılır;
 * edilemeyen kaynakta dürüst durum + İKİNCİL kaynak bağlantısı gösterilir
 * (otomatik yönlendirme yapılmaz).
 */
function VideoPlayer({
  video,
  onBack,
}: {
  video: MatchHighlightVideoDto;
  onBack: () => void;
}) {
  return (
    <div className="pb-2">
      <p className="mb-2 text-[13px] font-semibold text-white/90">{video.title}</p>

      {video.embeddable && video.embedUrl ? (
        <div className="aspect-video w-full overflow-hidden rounded-xl border border-goalai-border bg-black">
          <iframe
            src={video.embedUrl}
            title={video.title}
            className="h-full w-full"
            allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowFullScreen
          />
        </div>
      ) : (
        <div className="rounded-xl border border-goalai-border bg-goalai-surface-bright px-4 py-6 text-center">
          <p className="text-[13px] text-white/75">Bu video FORMAX içinde görüntülenemiyor.</p>
          {video.url && (
            <a
              href={video.url}
              target="_blank"
              rel="noreferrer noopener"
              className="mt-3 inline-block text-[12px] font-semibold text-goalai-accent underline underline-offset-2"
            >
              KAYNAĞA GİT ({video.source})
            </a>
          )}
        </div>
      )}

      {/* Kaynak bilgisi — kullanıcı videonun nereden geldiğini görür. */}
      <p className="mt-2 text-[11px] uppercase tracking-wide text-white/40">
        Kaynak: {video.source} · {video.platform}
      </p>

      <div className="mt-3 flex items-center gap-2">
        <button
          type="button"
          onClick={onBack}
          className="h-9 rounded-xl border border-goalai-border px-4 text-[13px] font-semibold text-white/80 transition-colors hover:bg-white/10 active:scale-95"
        >
          Listeye dön
        </button>

        {/*
          KAYNAĞA GİT — video FORMAX içinde oynasa da oynamasa da panelde bulunur.
          GERÇEK kaynak URL'sine gider; adres backend'den gelir, ÜRETİLMEZ.
          Otomatik yönlendirme yoktur: yalnız kullanıcı tıklarsa açılır.
        */}
        {video.url && video.embeddable && (
          <a
            href={video.url}
            target="_blank"
            rel="noreferrer noopener"
            className="flex h-9 items-center rounded-xl border border-goalai-accent/50 px-4 text-[13px] font-semibold text-goalai-accent transition-colors hover:bg-goalai-accent/10 active:scale-95"
          >
            KAYNAĞA GİT
          </a>
        )}
      </div>
    </div>
  );
}

function EmptyState({
  text,
  action,
}: {
  text: string;
  action?: { label: string; onClick: () => void };
}) {
  return (
    <div className="flex min-h-[180px] flex-col items-center justify-center px-6 text-center">
      <p className="text-[13px] leading-relaxed text-white/60">{text}</p>
      {action && (
        <button
          type="button"
          onClick={action.onClick}
          className="mt-3 h-9 rounded-xl border border-goalai-border px-4 text-[13px] font-semibold text-white/80 transition-colors hover:bg-white/10 active:scale-95"
        >
          {action.label}
        </button>
      )}
    </div>
  );
}
