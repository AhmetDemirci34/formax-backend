"use client";

import { TeamCrest } from "@/components/ui/TeamCrest";
import { formatMatchDateTR } from "@/lib/matchClock";
import { useState } from "react";
import {
  arrangeVideos,
  mainHighlightCandidates,
  nextCandidateAfterError,
  videoEmptyStateText,
} from "@/lib/video/videoSearch";
import { eventLabel } from "@/lib/matches/eventLabels";
import { hasVerifiedLineup } from "@/lib/lineup/lineupStatus";
import { VideoPlayerCard } from "./VideoPlayerCard";
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";
import type { MatchDetailDto, MatchEventDto, MatchStatisticsDto, MatchVideoDto } from "@/types/api";

/**
 * BİTMİŞ MAÇ ÖZETİ — kilitli ekran (02.09.2026 ürün kararı).
 *
 * Bu ekran bir haber portalı, sosyal akış veya yapay zekâ yorum ekranı DEĞİLDİR.
 * Kullanıcı tek bir şey ister: maçta ne olduğunu hızlı ve doğru görmek.
 *
 * KAPSAM (kapalı liste): lig/aşama → tarih → TR saati → stadyum → takımlar → MS →
 * İY/2Y/MS → maç sonrası analiz metni (arka planda doğrulanmış skor/olay/istatistikten;
 * maç öncesi AI yorumu DEĞİL) → resmî maç özeti videosu → varsa ayrı GOLLER klipleri →
 * varsa doğrulanmış olaylar → varsa doğrulanmış istatistikler → yalnız uygun lig
 * maçında puan durumu.
 *
 * KAPSAM DIŞI (UI'da karşılığı YOK, boş başlık olarak da yok): maç sonrası haber,
 * teknik direktör/oyuncu açıklaması, basın/sosyal yorum, AI maç hikâyesi.
 *
 * KAYNAK: yalnız backend DTO'su (DB). Bu ekran hiçbir sağlayıcıya istek ÜRETMEZ ve
 * hiçbir veriyi kendisi türetmez — 2Y çıkarması bile backend'de yapılır.
 *
 * BOŞ BÖLÜM YOK: verisi olmayan bölümün BAŞLIĞI da basılmaz. Tek istisna MAÇ ÖZETİ
 * video bölümüdür; kullanıcının aradığı asıl şey odur ve sessizce kaybolması
 * "ekran bozuk" hissi verir.
 */
export function FinishedMatchSummary({ match }: { match: MatchDetailDto }) {
  const sb = match.scoreBreakdown ?? null;
  const events = match.events ?? [];
  const stats = match.statistics ?? null;
  const when = formatMatchDateTR(match.matchDate);
  const videos = match.videos ?? [];

  // ANA VİDEO = oynatılabilen ilk özet; önemli anlar yalnız AYRI klipler (ana özet
  // TEKRAR düşmez); ana video yoksa oynatılamayan ama gerçek resmî kaynaklar. Backend
  // sıralaması korunur — kural lib/video/videoSearch.arrangeVideos'tadır.
  const { main, goals, otherMoments, blocked } = arrangeVideos(videos);

  // ARAMA DURUMU KALICI DEFTERDEN (11.09.2026 ürün kuralı): "bulunamadı" YALNIZ backend
  // dört gerçek denemenin tamamlandığını söylediğinde. Maçtan sonra geçen süre tek başına
  // hiçbir şey kanıtlamaz — iş hiç çalışmamış ya da rate limit'e takılmış olabilir.
  const videoEmptyText = videoEmptyStateText(match.videoSearch);

  // KADRO — maç bitmiş olsa bile DB'de doğrulanmış kadro varsa kaybolmaz.
  const showLineup = hasVerifiedLineup(match.lineup);

  const fmt = (s?: { home: number; away: number } | null) => (s ? `${s.home}-${s.away}` : "—");

  // PUAN DURUMU: yalnız backend "Table" derse. UEFA eleme/play-off karşılaşmasında
  // tablo KAVRAM OLARAK yoktur; frontend bu kararı yeniden HESAPLAMAZ.
  const showStandings = match.standing?.standingsAvailability === "Table";

  // Sonuç kartı dışında hiçbir ayrıntı yoksa tek bir genel mesaj. Ana video boş
  // durumu zaten aynı anlamı verdiği için ikisi ASLA birlikte gösterilmez.
  // Arama durumu (videoSearch) da bir ayrıntıdır: "kontrol ediliyor" cümlesi, genel
  // "ayrıntı yok" mesajının yerine geçer ve ikisi yine ASLA birlikte gösterilmez.
  // MAÇ SONRASI ANALİZ — yalnız arka planda yazılmış DB metni; ekran cümle ÜRETMEZ.
  const analysis = match.postMatchSummary?.sentences?.filter((x) => x.trim().length > 0) ?? [];

  // Tam özet yoksa ama oynatılabilir gol klipleri varsa ekran GOLLER der; boş "Maç Özeti"
  // kutusu ve "kontrol ediliyor" cümlesi o hâlde basılmaz (gol klibi tam özet sayılmaz).
  const showSummaryVideoPanel = !!main || blocked.length > 0 || goals.length === 0;

  const hasAnyDetail =
    videos.length > 0 || events.length > 0 || !!stats || showStandings || showLineup || !!match.videoSearch ||
    analysis.length > 0;

  return (
    <div className="flex min-h-0 w-full max-w-full flex-1 flex-col gap-3 overflow-y-auto overflow-x-hidden px-3 pb-28">
      {/* ── SONUÇ KARTI ───────────────────────────────────────────────────── */}
      <section className="w-full max-w-full shrink-0 overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright">
        <div className="flex flex-col gap-1 border-b border-goalai-border/60 px-3 py-2.5">
          <div className="flex items-start justify-between gap-2">
            <span className="min-w-0 flex-1 truncate text-[11px] font-semibold text-white/60">
              {match.league || "—"}
              {match.matchTypeLabel ? ` · ${match.matchTypeLabel}` : ""}
            </span>
            <span className="shrink-0 whitespace-nowrap rounded-md bg-white/[0.08] px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white/70">
              Maç Bitti
            </span>
          </div>

          {/* GERÇEK MAÇ TARİHİ VE TÜRKİYE SAATİ. Yoksa satır gizlenir — uydurulmaz. */}
          {when && (
            <span className="truncate text-[13px] font-bold text-text-primary">
              {when.date} · {when.time}
            </span>
          )}
          {match.venue && <span className="truncate text-[11px] text-text-muted">{match.venue}</span>}
        </div>

        {/* Ev sahibi SOLDA, deplasman SAĞDA — yön backend'den geldiği gibi. */}
        <div className="flex items-center gap-2 px-3 py-4">
          <div className="flex min-w-0 flex-1 flex-col items-center gap-1.5">
            <TeamCrest name={match.homeTeam?.name ?? ""} logoUrl={match.homeTeam?.logoUrl} size={44} />
            <span className="line-clamp-2 text-center text-[12px] font-semibold leading-tight text-text-primary">
              {match.homeTeam?.name ?? "—"}
            </span>
          </div>

          <div className="flex shrink-0 flex-col items-center px-2">
            <span className="whitespace-nowrap text-[30px] font-bold leading-none tabular-nums text-text-primary">
              {fmt(sb?.fullTime)}
            </span>
          </div>

          <div className="flex min-w-0 flex-1 flex-col items-center gap-1.5">
            <TeamCrest name={match.awayTeam?.name ?? ""} logoUrl={match.awayTeam?.logoUrl} size={44} />
            <span className="line-clamp-2 text-center text-[12px] font-semibold leading-tight text-text-primary">
              {match.awayTeam?.name ?? "—"}
            </span>
          </div>
        </div>

        {/* İY / 2Y / MS — eksik alan "—", 0-0 uydurulmaz. 2Y çıkarması backend'de
            yapılır ve negatif çıkarsa oraya null yazılır. */}
        <div className="grid grid-cols-3 border-t border-goalai-border/60">
          {[
            { k: "İY", v: fmt(sb?.halfTime) },
            { k: "2Y", v: fmt(sb?.secondHalf) },
            { k: "MS", v: fmt(sb?.fullTime) },
          ].map((c) => (
            <div key={c.k} className="flex flex-col items-center gap-0.5 py-2">
              <span className="text-[10px] font-semibold text-text-muted">{c.k}</span>
              <span className="whitespace-nowrap text-[15px] font-bold tabular-nums text-text-primary">{c.v}</span>
            </div>
          ))}
        </div>

        {/* Uzatma/penaltı YALNIZ kaynakta varsa; 90 dk skoruyla karıştırılmaz. */}
        {(sb?.extraTime || sb?.penalties) && (
          <div className="flex flex-wrap items-center justify-center gap-4 border-t border-goalai-border/60 py-2">
            {sb?.extraTime && (
              <span className="whitespace-nowrap text-[11px] text-text-muted">
                Uzatma <span className="font-bold text-text-primary">{fmt(sb.extraTime)}</span>
              </span>
            )}
            {sb?.penalties && (
              <span className="whitespace-nowrap text-[11px] text-text-muted">
                Penaltılar <span className="font-bold text-text-primary">{fmt(sb.penalties)}</span>
              </span>
            )}
          </div>
        )}
      </section>

      {/* Hiçbir ayrıntı yoksa TEK genel mesaj (video boş durumu ile birlikte ASLA). */}
      {!hasAnyDetail && (
        <p className="px-3 py-4 text-center text-[12px] leading-relaxed text-white/50">
          Bu maçın sonucu kesinleşti. Ayrıntılı özet verileri henüz bulunmuyor.
        </p>
      )}

      {/* ── MAÇ SONRASI ANALİZ — doğrulanmış skor/olay/istatistikten kısa metin ── */}
      {analysis.length > 0 && (
        <Panel title="Maç Sonrası Analiz">
          <div className="flex flex-col gap-1.5 px-3 py-3" data-testid="post-match-analysis">
            {analysis.map((line, i) => (
              <p key={i} className="break-words text-[12.5px] leading-relaxed text-white/85">
                {line}
              </p>
            ))}
          </div>
        </Panel>
      )}

      {/* ── MAÇ ÖZETİ ─────────────────────────────────────────────────────── */}
      {hasAnyDetail && showSummaryVideoPanel && (
        <Panel title="Maç Özeti">
          {main ? (
            <div className="p-3">
              <MainHighlightPlayer candidates={mainHighlightCandidates(videos)} />
            </div>
          ) : (
            <>
              {goals.length === 0 && <Empty text={videoEmptyText} />}
              {blocked.length > 0 && (
                <ul className="flex flex-col gap-2 px-3 pb-3">
                  {blocked.map((v) => (
                    <li key={v.sourcePageUrl}>
                      <VideoPlayerCard video={v} />
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </Panel>
      )}

      {/* ── GOLLER — yalnız oynatılabilir AYRI gol klipleri (tam özet değildir) ── */}
      {goals.length > 0 && (
        <Panel title="Goller">
          <ul className="flex flex-col gap-2.5 p-3">
            {goals.map((v) => (
              <li key={v.sourcePageUrl}>
                <VideoPlayerCard video={v} compact />
              </li>
            ))}
          </ul>
        </Panel>
      )}

      {/* ── DİĞER RESMÎ KLİPLER (kart/VAR/önemli an) — yalnız AYRI klipler ──── */}
      {otherMoments.length > 0 && (
        <Panel title="Önemli Anlar">
          <ul className="flex flex-col gap-2.5 p-3">
            {otherMoments.map((v) => (
              <li key={v.sourcePageUrl}>
                <VideoPlayerCard video={v} compact />
              </li>
            ))}
          </ul>
        </Panel>
      )}

      {/* ── MAÇIN ÖNEMLİ ANLARI — doğrulanmış olay zaman çizelgesi ────────── */}
      {events.length > 0 && (
        <Panel title="Maçın Önemli Anları">
          <ul className="divide-y divide-goalai-border/40">
            {events.map((e, i) => (
              <EventRow key={`${e.minute}-${e.eventType}-${i}`} e={e} />
            ))}
          </ul>
        </Panel>
      )}

      {/* ── KADROLAR — yalnız DOĞRULANMIŞ kadro varsa (boş bölüm başlığı yok) ── */}
      {showLineup && (
        <Panel title="Kadrolar">
          <div className="p-3">
            <LineupPanel match={match} />
          </div>
        </Panel>
      )}

      {/* ── MAÇ İSTATİSTİKLERİ — backend null derse bölüm HİÇ yok ─────────── */}
      {stats && stats.rows.length > 0 && (
        <Panel title="Maç İstatistikleri">
          <StatisticsTable stats={stats} />
        </Panel>
      )}

      {/* ── PUAN DURUMU — yalnız backend "Table" derse ────────────────────── */}
      {showStandings && match.standing?.standingsNotice && (
        <Panel title="Puan Durumu">
          <Empty text={match.standing.standingsNotice} />
        </Panel>
      )}
    </div>
  );
}

/**
 * ANA ÖZET OYNATICISI — oynatıcı gömme/bölge engeli bildirirse aynı maçın bir sonraki doğrulanmış resmî
 * özetine geçer ve bunu açıkça yazar. Aday kalmazsa kartın kendi dürüst hata metni kalır.
 */
function MainHighlightPlayer({ candidates }: { candidates: MatchVideoDto[] }) {
  const [index, setIndex] = useState(0);
  const video = candidates[Math.min(index, candidates.length - 1)];
  if (!video) return null;
  return (
    <div className="flex flex-col gap-2">
      {index > 0 && (
        <p className="text-[11px] leading-relaxed text-white/70" data-testid="video-fallback-note">
          Önceki resmî video bu bölgede ya da uygulama içinde oynatılamadı; aynı maçın başka bir resmî kaynağı gösteriliyor.
        </p>
      )}
      <VideoPlayerCard
        key={video.sourcePageUrl}
        video={video}
        autoStart={index > 0}
        onPlaybackError={(code) => {
          const next = nextCandidateAfterError(candidates.length, index, code);
          if (next !== null) setIndex(next);
        }}
      />
    </div>
  );
}

/** İstatistik tablosu — ev solda, deplasman sağda, arada oransal bar. */
function StatisticsTable({ stats }: { stats: MatchStatisticsDto }) {
  return (
    <ul className="flex flex-col gap-2.5 px-3 py-3">
      {stats.rows.map((r) => {
        const total = r.home + r.away;
        const homePct = total > 0 ? (r.home / total) * 100 : 50;
        const unit = r.isPercentage ? "%" : "";
        return (
          <li key={r.key} className="flex flex-col gap-1">
            <div className="flex items-center justify-between gap-2 text-[11px]">
              <span className="w-[46px] shrink-0 whitespace-nowrap text-left font-bold tabular-nums text-text-primary">
                {r.home}
                {unit}
              </span>
              <span className="min-w-0 flex-1 truncate text-center text-text-muted">{r.label}</span>
              <span className="w-[46px] shrink-0 whitespace-nowrap text-right font-bold tabular-nums text-text-primary">
                {r.away}
                {unit}
              </span>
            </div>
            <div className="flex h-1 w-full max-w-full overflow-hidden rounded-full bg-white/[0.06]">
              <span className="h-full bg-goalai-accent/70" style={{ width: `${homePct}%` }} />
              <span className="h-full bg-white/20" style={{ width: `${100 - homePct}%` }} />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

/** Ortak bölüm kabuğu. Yalnız İÇERİĞİ olan bölüm için çağrılır. */
function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="w-full max-w-full shrink-0 overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright">
      <h3 className="border-b border-goalai-border/60 px-3 py-2 text-[11px] font-bold uppercase tracking-wide text-white/60">
        {title}
      </h3>
      {children}
    </section>
  );
}

function Empty({ text }: { text: string }) {
  // Kontrast: koyu zeminde %50 beyaz mobilde okunmuyordu; durum cümlesi asıl bilgidir.
  return <p className="px-3 py-5 text-center text-[13px] leading-relaxed text-white/85">{text}</p>;
}


/**
 * Tek olay satırı — yalnız DOLU alanlar gösterilir, eksik alan uydurulmaz.
 *
 * ETİKET backend'in deterministik Türkçe eşlemesinden (label) gelir; ham sağlayıcı
 * terimi ("Substitution 1", "Normal Goal") kullanıcıya GÖSTERİLMEZ.
 * Oyuncu değişikliğinde "Asist" yazılmaz: giren ve çıkan ayrı ayrı gösterilir.
 */
function EventRow({ e }: { e: MatchEventDto }) {
  const isSub = e.kind === "Substitution";
  return (
    <li className="flex items-start gap-2.5 px-3 py-2">
      <span className="w-[38px] shrink-0 whitespace-nowrap pt-[1px] text-right text-[11px] font-bold tabular-nums text-goalai-accent">
        {e.minute}
        {e.extraMinute ? `+${e.extraMinute}` : ""}&apos;
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="break-words text-[12px] font-semibold leading-tight text-text-primary">{eventLabel(e)}</span>
        {isSub ? (
          <span className="truncate text-[11px] text-text-muted">
            {e.playerIn ? `Giren: ${e.playerIn}` : ""}
            {e.playerIn && e.playerOut ? " · " : ""}
            {e.playerOut ? `Çıkan: ${e.playerOut}` : ""}
            {e.team ? ` · ${e.team}` : ""}
          </span>
        ) : (
          (e.player || e.team) && (
            <span className="truncate text-[11px] text-text-muted">
              {e.player ?? ""}
              {e.player && e.team ? " · " : ""}
              {e.team ?? ""}
            </span>
          )
        )}
        {!isSub && e.assist && <span className="truncate text-[10px] text-text-muted">Asist: {e.assist}</span>}
      </div>
    </li>
  );
}
