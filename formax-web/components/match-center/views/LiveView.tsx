"use client";

import type { MatchDetailDto, MatchLiveEventItemDto, MatchLiveFeedDto } from "@/types/api";
import { useMatchLiveFeed } from "@/hooks/useMatchLiveFeed";
import { ViewShell } from "./ViewShell";

/**
 * CANLI TAKİP — "Maçta ne oluyor?"
 *
 * TEK KAYNAK: `GET /api/matches/{id}/livefeed` → kanonik MAÇ OLAYLARI.
 *
 * BU EKRAN VİDEO GÖSTERMEZ. Video "Önemli Anları İzle" özelliğine aittir; iki özellik
 * ayrı uçlar, ayrı sorgular, ayrı hatlardır. Burada LLM/Gemma da yoktur.
 *
 * AI MAÇ ANALİZİ'NE DOKUNMAZ: ayrı queryKey, ayrı uç; canlı olaylar AI metnini
 * değiştirmez.
 *
 * DÖRT DURUM (kararı BACKEND verir; frontend maç durumu hesaplamaz):
 *   NotStarted → dürüst bilgi ekranı. Live → yalnız global kaynak doğruladıysa.
 *   Finished   → "Maç sona erdi." + kesin sonuç. Unknown → "CANLI" etiketi YOK.
 */
export function LiveView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const { data, isLoading, isError, refetch } = useMatchLiveFeed(match.matchId);

  return (
    <ViewShell title="Canlı Takip" onClose={onClose}>
      {isLoading && <Centered>Canlı takip yükleniyor...</Centered>}

      {isError && (
        <Centered>
          <span className="block">Canlı takip şu an yüklenemiyor.</span>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 h-9 rounded-xl border border-goalai-border px-4 text-[13px] font-semibold text-white/80 transition-colors hover:bg-white/10 active:scale-95"
          >
            Tekrar dene
          </button>
        </Centered>
      )}

      {data && <LiveBody feed={data} />}
    </ViewShell>
  );
}

function LiveBody({ feed }: { feed: MatchLiveFeedDto }) {
  if (feed.state === "NotStarted") {
    return (
      <div className="flex h-full min-h-[220px] flex-col items-center justify-center px-6 text-center">
        <p className="text-[15px] font-semibold text-white">
          {feed.stateMessage ?? "Canlı takip henüz başlamadı."}
        </p>
        <p className="mt-1.5 text-[13px] leading-relaxed text-white/55">
          Maç başladığında canlı gelişmeleri burada takip edebilirsiniz.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4 pb-4">
      <StatusBar feed={feed} />

      {feed.events.length === 0 ? (
        <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-6 text-center">
          <p className="text-[13px] leading-relaxed text-white/55">
            Bu maç için global kaynaklarda doğrulanmış bir maç olayı bulunmadı.
          </p>
        </div>
      ) : (
        <ol className="space-y-2">
          {/* Backend ters kronolojik döndürür: EN YENİ EN ÜSTTE. Sıralama değiştirilmez. */}
          {feed.events.map((e) => (
            <EventRow key={e.id} event={e} />
          ))}
        </ol>
      )}
    </div>
  );
}

/**
 * "CANLI" rozeti YALNIZ backend liveConfirmed=true derse çizilir — bayat sağlayıcı
 * durumu ya da saat penceresi bu rozeti açtırmaz.
 */
function StatusBar({ feed }: { feed: MatchLiveFeedDto }) {
  const badge = feed.liveConfirmed
    ? { text: "Canlı", tone: "text-goalai-accent" }
    : feed.state === "Finished"
      ? { text: "Maç Sona Erdi", tone: "text-white/45" }
      : { text: "Durum Doğrulanamadı", tone: "text-white/45" };

  return (
    <div className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
      <div className="mb-2 flex items-center justify-between gap-3">
        <span className={`text-[11px] font-bold uppercase tracking-wide ${badge.tone}`}>
          {badge.text}
        </span>
        {feed.score?.phase && (
          <span className="font-mono text-[11px] font-bold uppercase tabular-nums text-white/50">
            {feed.score.phase}
          </span>
        )}
      </div>

      {feed.score ? (
        <>
          <div className="flex items-center justify-between gap-3">
            <span className="min-w-0 flex-1 truncate text-[13px] text-white/85">{feed.homeTeam}</span>
            <span className="shrink-0 font-mono text-xl font-bold tabular-nums text-white">
              {feed.score.homeScore} - {feed.score.awayScore}
            </span>
            <span className="min-w-0 flex-1 truncate text-right text-[13px] text-white/85">
              {feed.awayTeam}
            </span>
          </div>
          {feed.score.origin === "global" && (
            <p className="mt-1.5 text-center text-[11px] text-white/35">
              Skor global kaynaktan · {formatStamp(feed.score.updatedAt)}
            </p>
          )}

          {/* Penaltı serisi gerçekten oynandıysa sonucu ayrı gösterilir. */}
          {feed.shootoutHome != null && feed.shootoutAway != null && (
            <p className="mt-1.5 text-center text-[12px] font-semibold text-white/70">
              Penaltılar: {feed.shootoutHome} - {feed.shootoutAway}
            </p>
          )}

          {/* ÇELİŞKİ GİZLENMEZ: kayıtlı skor maçın tamamını kapsamıyorsa açıkça yazılır. */}
          {feed.scoreNote && (
            <p className="mt-1.5 text-[11px] leading-relaxed text-amber-300/80">
              {feed.scoreNote}
            </p>
          )}
        </>
      ) : (
        <p className="text-[13px] leading-relaxed text-white/55">
          {feed.scoreUnavailableReason ?? "Skor bilgisi bulunmuyor."}
        </p>
      )}

      {feed.stateMessage && (
        <p className="mt-2 text-[12px] leading-relaxed text-white/45">{feed.stateMessage}</p>
      )}
    </div>
  );
}

/** Olay türü işareti — yalnız kanonik türden seçilir, yorum eklemez. */
function eventIcon(type: string): string {
  switch (type) {
    case "GOAL":
    case "PENALTY_GOAL":
      return "⚽";
    case "MISSED_PENALTY":
      return "✖";
    case "PENALTY_AWARDED":
      return "◎";
    case "RED_CARD":
      return "🟥";
    case "SECOND_YELLOW":
      return "🟨🟥";
    case "YELLOW_CARD":
      return "🟨";
    case "VAR":
    case "VAR_GOAL_DISALLOWED":
      return "📺";
    case "SUBSTITUTION":
      return "🔁";
    case "SCORE_UPDATE":
      return "📊";
    case "HALF_TIME":
    case "SECOND_HALF":
    case "EXTRA_TIME":
    case "KICKOFF":
    case "FULL_TIME":
      return "⏱";
    default:
      return "•";
  }
}

/**
 * Olay satırı: [DAKİKA (varsa)] [ETİKET] [TAKIM/OYUNCU] + kaynağın kendi metni,
 * kaynak adı ve yayın saati. Dakika yoksa UYDURULMAZ.
 */
function EventRow({ event }: { event: MatchLiveEventItemDto }) {
  const subject = [event.player, event.team].filter(Boolean).join(" · ");

  return (
    <li className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-3.5 py-3">
      <div className="flex items-center gap-2">
        {event.minuteLabel && (
          <span className="font-mono text-[13px] font-bold tabular-nums text-goalai-accent">
            {event.minuteLabel}&apos;
          </span>
        )}
        <span className="min-w-0 flex-1 truncate text-[13px] font-bold tracking-wide text-white">
          {eventIcon(event.eventType)} {event.label}
        </span>
        <span className="shrink-0 text-[11px] tabular-nums text-white/35">
          {formatStamp(event.publishedAt)}
        </span>
      </div>

      {subject && (
        <p className="mt-0.5 text-[12.5px] font-semibold text-white/75">{subject}</p>
      )}

      <p className="mt-1 text-[13px] leading-relaxed text-white/80">{event.description}</p>

      <p className="mt-1 truncate text-[11px] uppercase tracking-wide text-white/35">
        {event.source}
      </p>
    </li>
  );
}

function Centered({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-full min-h-[220px] flex-col items-center justify-center px-6 text-center text-[13px] text-white/55">
      {children}
    </div>
  );
}

/** "19:42" — backend'in UTC damgası tarayıcı tarafından yerel saate çevrilir. */
function formatStamp(iso?: string): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });
}
