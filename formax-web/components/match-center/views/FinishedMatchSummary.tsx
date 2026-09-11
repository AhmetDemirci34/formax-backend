"use client";

import { useState } from "react";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { formatMatchDateTR, isVideoSearchWindowOver } from "@/lib/matchClock";
import type {
  MatchDetailDto,
  MatchEventDto,
  MatchStatisticsDto,
  MatchVideoDto,
} from "@/types/api";

/**
 * BİTMİŞ MAÇ ÖZETİ — kilitli ekran (02.09.2026 ürün kararı).
 *
 * Bu ekran bir haber portalı, sosyal akış veya yapay zekâ yorum ekranı DEĞİLDİR.
 * Kullanıcı tek bir şey ister: maçta ne olduğunu hızlı ve doğru görmek.
 *
 * KAPSAM (kapalı liste): lig/aşama → tarih → TR saati → stadyum → takımlar → MS →
 * İY/2Y/MS → resmî maç özeti videosu → varsa ayrı gol/önemli an klipleri →
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

  // ANA VİDEO = oynatılabilen ilk özet. Backend zaten oynatılabilirliğe ve türe göre
  // sıralar; ekran o kararı yeniden yorumlamaz, yalnız ilkini ana karta alır.
  const main = videos.find((v) => v.canPlayInApp && isMainHighlight(v.videoType)) ?? null;

  // GOLLER VE ÖNEMLİ ANLAR yalnız AYRI kliplerdir. Ana özet buraya TEKRAR düşmez ve
  // tam özet sahte gol kliplerine BÖLÜNMEZ.
  const moments = videos.filter((v) => isMoment(v.videoType) && v !== main);

  // Oynatılamayan ama gerçek olan resmî kaynaklar (embed yasağı / bölgesel kısıt):
  // ana video yoksa kullanıcı hiç değilse kaynağa gidebilsin.
  const blocked = main ? [] : videos.filter((v) => !v.canPlayInApp && isMainHighlight(v.videoType));

  // ARAMA HÂLÂ SÜRÜYOR MU? Backend'in tekrar takvimi (PostMatchEnrichmentJob) son
  // düdükten sonra FT+60dk → FT+3sa → FT+6sa → FT+24sa olmak üzere DÖRT kez bakar;
  // dördü de boş dönerse arama BİTER.
  //
  // NEDEN İKİ AYRI METİN: "henüz bulunamadı" ile "aranıyor" kullanıcı için aynı şey
  // değildir. Maç biteli 40 dakika olmuşken "bulunamadı" demek yanlıştır — daha hiç
  // bakılmamıştır ve kullanıcı ekranı bir daha açmaz. Arama bittikten sonra hâlâ
  // "kontrol ediliyor" demek ise sonu gelmeyen bir bekleyiş vaat etmektir.
  const searchWindowOver = isVideoSearchWindowOver(match.matchDate);

  const fmt = (s?: { home: number; away: number } | null) => (s ? `${s.home}-${s.away}` : "—");

  // PUAN DURUMU: yalnız backend "Table" derse. UEFA eleme/play-off karşılaşmasında
  // tablo KAVRAM OLARAK yoktur; frontend bu kararı yeniden HESAPLAMAZ.
  const showStandings = match.standing?.standingsAvailability === "Table";

  // Sonuç kartı dışında hiçbir ayrıntı yoksa tek bir genel mesaj. Ana video boş
  // durumu zaten aynı anlamı verdiği için ikisi ASLA birlikte gösterilmez.
  const hasAnyDetail = videos.length > 0 || events.length > 0 || !!stats || showStandings;

  return (
    <div className="flex w-full max-w-full flex-col gap-3 overflow-x-hidden px-3 pb-28">
      {/* ── SONUÇ KARTI ───────────────────────────────────────────────────── */}
      <section className="w-full max-w-full overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright">
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

      {/* ── MAÇ ÖZETİ ─────────────────────────────────────────────────────── */}
      {hasAnyDetail && (
        <Panel title="Maç Özeti">
          {main ? (
            <div className="p-3">
              <VideoPlayerCard video={main} />
            </div>
          ) : (
            <>
              <Empty
                text={
                  searchWindowOver
                    ? "Bu maç için uygulama içinde oynatılabilen resmî özet videosu bulunamadı."
                    : "Resmî maç özeti kontrol ediliyor."
                }
              />
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

      {/* ── GOLLER VE ÖNEMLİ ANLAR — yalnız AYRI klipler ──────────────────── */}
      {moments.length > 0 && (
        <Panel title="Goller ve Önemli Anlar">
          <ul className="flex flex-col gap-2.5 p-3">
            {moments.map((v) => (
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

/** Ana özet türleri — ekranın büyük kartına yalnız bunlar çıkar. */
function isMainHighlight(videoType: string) {
  return videoType === "MatchHighlights" || videoType === "ExtendedHighlights";
}

/** "Goller ve önemli anlar" listesine giren AYRI klip türleri. */
function isMoment(videoType: string) {
  return (
    videoType === "Goal" ||
    videoType === "Penalty" ||
    videoType === "RedCard" ||
    videoType === "VAR" ||
    videoType === "ImportantMoment"
  );
}

const VIDEO_TYPE_LABEL: Record<string, string> = {
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
 * KURALLAR:
 *  • Oynatıcı FORMAX ekranının İÇİNDE açılır; yeni sekmeye yönlendirme yoktur.
 *  • SAYFA AÇILIRKEN IFRAME OLUŞTURULMAZ. iframe yalnız kullanıcı oynat düğmesine
 *    bastıktan sonra kurulur — yani sayfa açılışı hiçbir dış istek üretmez.
 *  • Otomatik oynatma yoktur; autoplay yalnız kullanıcının kendi tıklamasından
 *    doğan adreste bulunur.
 *  • canPlayInApp false ise oynatıcı HİÇ kurulmaz: sonsuz spinner ya da boş siyah
 *    kutu yerine sebebi yazılır ve resmî kaynağa gitme seçeneği verilir.
 *  • Bölgesel kısıt ayrı bir cümledir: "oynatılamıyor" ile "senin bölgende
 *    oynatılamıyor" aynı şey değildir.
 */
function VideoPlayerCard({ video, compact = false }: { video: MatchVideoDto; compact?: boolean }) {
  const [playing, setPlaying] = useState(false);
  const [failed, setFailed] = useState(false);
  const label = VIDEO_TYPE_LABEL[video.videoType] ?? video.videoType;
  const countries = video.availableCountries ?? [];

  if (!video.canPlayInApp) {
    return (
      <div className="w-full max-w-full rounded-xl border border-goalai-border/70 bg-black/20 p-3">
        <p className="line-clamp-2 text-[12px] font-semibold leading-tight text-text-primary">{video.title}</p>
        <p className="mt-1.5 flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[10px] text-text-muted">
          <SourceBadge publisher={video.publisher} />
          <span>{label}</span>
        </p>
        {/* Dürüstlük: bu video VAR ama uygulama içinde oynatılamıyor. */}
        <p className="mt-1.5 text-[11px] leading-relaxed text-white/50">
          Uygulama içinde oynatılamıyor.
        </p>
        <ExternalSourceLink url={video.sourcePageUrl} />
      </div>
    );
  }

  return (
    <div className="w-full max-w-full overflow-hidden rounded-xl border border-goalai-border/70 bg-black/30">
      {/* 16:9 — sabit piksel yok, 375px'te taşmaz. */}
      <div className="relative aspect-video w-full max-w-full bg-black">
        {playing && !failed ? (
          <iframe
            src={`${video.embedUrl}?rel=0&modestbranding=1&playsinline=1&autoplay=1`}
            title={video.title}
            allow="accelerometer; encrypted-media; gyroscope; picture-in-picture; fullscreen"
            allowFullScreen
            onError={() => setFailed(true)}
            className="absolute inset-0 h-full w-full border-0"
          />
        ) : failed ? (
          // Player hata verdi: SONSUZ SPINNER YOK, dürüst mesaj ve kaynak bağlantısı.
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-2 px-4 text-center">
            <p className="text-[12px] leading-relaxed text-white/60">
              Video şu an oynatılamıyor.
            </p>
            <ExternalSourceLink url={video.sourcePageUrl} />
          </div>
        ) : (
          <button
            type="button"
            onClick={() => setPlaying(true)}
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

        {/* BÖLGESEL KISIT — sonsuz yükleme yerine açık cümle. Korsan alternatif YOK. */}
        {video.isRegionRestricted && countries.length > 0 && (
          <p className="text-[10.5px] leading-relaxed text-white/45">
            Bu resmî video yalnız {countries.join(", ")} bölgesinde oynatılabilir. Bulunduğunuz
            bölge dışındaysa oynatılamayabilir.
          </p>
        )}
      </div>
    </div>
  );
}

function ExternalSourceLink({ url }: { url: string }) {
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
function SourceBadge({ publisher }: { publisher: string }) {
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
    <section className="w-full max-w-full overflow-hidden rounded-2xl border border-goalai-border bg-goalai-surface-bright">
      <h3 className="border-b border-goalai-border/60 px-3 py-2 text-[11px] font-bold uppercase tracking-wide text-white/60">
        {title}
      </h3>
      {children}
    </section>
  );
}

function Empty({ text }: { text: string }) {
  return <p className="px-3 py-5 text-center text-[12px] leading-relaxed text-white/50">{text}</p>;
}

const EVENT_LABEL: Record<string, string> = {
  Goal: "Gol",
  "Own Goal": "Kendi kalesine gol",
  Penalty: "Penaltı",
  "Missed Penalty": "Kaçan penaltı",
  "Yellow Card": "Sarı kart",
  "Red Card": "Kırmızı kart",
  Subst: "Oyuncu değişikliği",
  Var: "VAR kararı",
  VAR: "VAR kararı",
};

/** Tek olay satırı — yalnız DOLU alanlar gösterilir, eksik alan uydurulmaz. */
function EventRow({ e }: { e: MatchEventDto }) {
  const label = e.detail ? EVENT_LABEL[e.detail] ?? e.detail : EVENT_LABEL[e.eventType] ?? e.eventType;
  return (
    <li className="flex items-start gap-2.5 px-3 py-2">
      <span className="w-[38px] shrink-0 whitespace-nowrap pt-[1px] text-right text-[11px] font-bold tabular-nums text-goalai-accent">
        {e.minute}
        {e.extraMinute ? `+${e.extraMinute}` : ""}&apos;
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-0.5">
        <span className="break-words text-[12px] font-semibold leading-tight text-text-primary">{label}</span>
        {(e.player || e.team) && (
          <span className="truncate text-[11px] text-text-muted">
            {e.player ?? ""}
            {e.player && e.team ? " · " : ""}
            {e.team ?? ""}
          </span>
        )}
        {e.assist && <span className="truncate text-[10px] text-text-muted">Asist: {e.assist}</span>}
      </div>
    </li>
  );
}

function formatDuration(seconds: number) {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${String(s).padStart(2, "0")}`;
}
