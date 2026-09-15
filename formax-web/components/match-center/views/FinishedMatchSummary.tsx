"use client";

import { TeamCrest } from "@/components/ui/TeamCrest";
import { formatMatchDateTR } from "@/lib/matchClock";
import {
  ANALYSIS_INSUFFICIENT_TEXT,
  ANALYSIS_PENDING_TEXT,
  STATISTICS_NOT_PUBLISHED_PREFIX,
} from "@/lib/matches/postMatchTexts";
import { eventLabel } from "@/lib/matches/eventLabels";
import { hasVerifiedLineup } from "@/lib/lineup/lineupStatus";
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";
import type { MatchDetailDto, MatchEventDto, MatchStatisticsDto } from "@/types/api";

/**
 * BİTMİŞ MAÇ ÖZETİ — kilitli ekran (02.09.2026 ürün kararı).
 *
 * Bu ekran bir haber portalı, sosyal akış veya yapay zekâ yorum ekranı DEĞİLDİR.
 * Kullanıcı tek bir şey ister: maçta ne olduğunu hızlı ve doğru görmek.
 *
 * KAPSAM (kapalı liste): lig/aşama → tarih → TR saati → stadyum → takımlar → MS →
 * İY/2Y/MS → maç sonrası analiz metni (arka planda doğrulanmış skor/olay/istatistikten;
 * maç öncesi AI yorumu DEĞİL) → varsa doğrulanmış olaylar → varsa doğrulanmış istatistikler → yalnız uygun lig
 * maçında puan durumu.
 *
 * KAPSAM DIŞI (UI'da karşılığı YOK, boş başlık olarak da yok): maç sonrası haber,
 * teknik direktör/oyuncu açıklaması, basın/sosyal yorum, AI maç hikâyesi.
 *
 * KAYNAK: yalnız backend DTO'su (DB). Bu ekran hiçbir sağlayıcıya istek ÜRETMEZ ve
 * hiçbir veriyi kendisi türetmez — 2Y çıkarması bile backend'de yapılır.
 *
 * BOŞ BÖLÜM YOK: verisi olmayan bölümün BAŞLIĞI da basılmaz.
 *
 * VİDEO YOK (15.09.2026 ürün kararı): maç özeti/gol videosu, oynatıcı ve video arama durumu bu ekrandan
 * tamamen kaldırıldı; DTO video alanı taşımaz.
 */
export function FinishedMatchSummary({ match }: { match: MatchDetailDto }) {
  const sb = match.scoreBreakdown ?? null;
  const events = match.events ?? [];
  const stats = match.statistics ?? null;
  const when = formatMatchDateTR(match.matchDate);
  // KADRO — maç bitmiş olsa bile DB'de doğrulanmış kadro varsa kaybolmaz.
  const showLineup = hasVerifiedLineup(match.lineup);

  const fmt = (s?: { home: number; away: number } | null) => (s ? `${s.home}-${s.away}` : "—");

  // PUAN DURUMU: yalnız backend "Table" derse. UEFA eleme/play-off karşılaşmasında
  // tablo KAVRAM OLARAK yoktur; frontend bu kararı yeniden HESAPLAMAZ.
  const showStandings = match.standing?.standingsAvailability === "Table";

  // Sonuç kartı dışında hiçbir ayrıntı yoksa tek bir genel mesaj.
  // MAÇ SONRASI ANALİZ — yalnız arka planda yazılmış DB metni; ekran cümle ÜRETMEZ.
  const analysis = match.postMatchSummary?.sentences?.filter((x) => x.trim().length > 0) ?? [];
  // Analiz durumu backend'den: yetersiz veri → dürüst cümle; henüz yazılmadı → "hazırlanıyor". Cümle UYDURULMAZ.
  const analysisStatus = match.postMatchSummary?.status ?? (analysis.length > 0 ? "Available" : null);
  const analysisNotice =
    analysis.length > 0
      ? null
      : analysisStatus === "InsufficientData"
        ? ANALYSIS_INSUFFICIENT_TEXT
        : analysisStatus === "Pending"
          ? ANALYSIS_PENDING_TEXT
          : null;

  const hasAnyDetail =
    events.length > 0 || !!stats || showStandings || showLineup || analysis.length > 0 || !!analysisNotice;

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
              {match.resultDetail === "AET" ? "Uzatmalarda Bitti" : match.resultDetail === "PEN" ? "Penaltılarla Bitti" : "Maç Bitti"}
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

      {/* Hiçbir ayrıntı yoksa TEK genel mesaj. */}
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
      {analysisNotice && (
        <Panel title="Maç Sonrası Analiz">
          <div data-testid="post-match-analysis-notice">
            <Empty text={analysisNotice} />
          </div>
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

/** İstatistik tablosu — ev solda, deplasman sağda, arada oransal bar. */
function StatisticsTable({ stats }: { stats: MatchStatisticsDto }) {
  const notPublished = stats.notPublished ?? [];
  return (
    <>
    <ul className="flex flex-col gap-2.5 px-3 py-3" data-testid="statistics-rows">
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
    {/* VERİ YOK ≠ 0: kaynağın yayımlamadığı alan satır olarak "0" basılmaz, burada adıyla söylenir. */}
    {(notPublished.length > 0 || stats.sourceName) && (
      <div className="flex flex-col gap-0.5 border-t border-goalai-border/40 px-3 py-2">
        {notPublished.length > 0 && (
          <p className="text-[10.5px] leading-relaxed text-white/55" data-testid="statistics-not-published">
            {STATISTICS_NOT_PUBLISHED_PREFIX} {notPublished.join(", ")}
          </p>
        )}
        {stats.sourceName && <p className="text-[10px] text-white/45">Resmî kaynak: {stats.sourceName}</p>}
      </div>
    )}
    </>
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
