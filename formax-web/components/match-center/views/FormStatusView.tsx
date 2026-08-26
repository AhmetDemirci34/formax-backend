"use client";

import { useState } from "react";
import type {
  MatchDetailDto,
  LastMatchDto,
  StandingTableDto,
  TeamStandingDto,
} from "@/types/api";
import { ViewShell } from "./ViewShell";

/**
 * FormStatusView (activeView === 'stats') — Form Durumu.
 *
 * ÜÇ BÖLÜM
 *  1) Aralarındaki son maçlar   → match.h2h.matches      (yalnız oynanmış)
 *  2) Ev sahibi ligde son maçlar → homeTeamLastMatches   (yalnız oynanmış)
 *  3) Deplasman ligde son maçlar → awayTeamLastMatches   (yalnız oynanmış)
 * (Altta mevcut Puan Durumu paneli korunur.)
 *
 * VERİ KURALLARI
 *  • Backend her iki kaynağı da `finishedOnly: true` ile üretir → gelecek, canlı,
 *    iptal veya oynanmamış maç GELMEZ; 0-0 hayalet satır oluşmaz.
 *  • Başlıklar gerçek satır sayısını söyler ("Son 3 Maç"); 5'e tamamlanmaz.
 *  • İLK YARI skoru sağlayıcının score.halftime alanından gelir (Matches.HalfTime*Score).
 *    Sağlayıcı vermediyse null → İY sütunu "—" gösterir; MS'ten TÜRETİLMEZ.
 *  • Bu dosya istatistik HESAPLAMAZ; yalnız backend alanlarını gösterir.
 */
export function FormStatusView({ match, onClose }: { match: MatchDetailDto; onClose: () => void }) {
  const h2h = (match.h2h?.matches ?? []).slice(0, 5); // backend 10 döner; ekranda en fazla son 5
  const home = match.homeTeamLastMatches ?? [];
  const away = match.awayTeamLastMatches ?? [];

  return (
    <ViewShell title="Form Durumları" onClose={onClose}>
      <div className="space-y-3 pb-4">
        <Section
          title="Aralarındaki Son Maçlar"
          subtitle={h2h.length > 0 ? `Son ${h2h.length} Karşılaşma` : undefined}
          empty={h2h.length === 0 ? "Bu iki takım arasında kayıtlı karşılaşma bulunmuyor." : undefined}
        >
          {h2h.map((m, i) => (
            <MatchRow
              key={`h2h-${i}`}
              date={m.matchDate}
              homeName={m.homeTeamName}
              awayName={m.awayTeamName}
              score={`${m.homeScore}-${m.awayScore}`}
              halfTime={formatHalfTime(m.halfTimeHomeScore, m.halfTimeAwayScore)}
              competition={m.competition}
            />
          ))}
        </Section>

        <TeamLeagueForm
          teamName={match.homeTeam.name}
          matches={home}
          leagueName={match.homeTeamFormLeague}
        />
        <TeamLeagueForm
          teamName={match.awayTeam.name}
          matches={away}
          leagueName={match.awayTeamFormLeague}
        />

        <StandingsPanel standing={match.standing} />
      </div>
    </ViewShell>
  );
}

// ── Ortak kabuk + satır ───────────────────────────────────────────────────────

function Section({
  title,
  subtitle,
  empty,
  children,
}: {
  title: string;
  subtitle?: string;
  empty?: string;
  children?: React.ReactNode;
}) {
  return (
    <section className="overflow-hidden rounded-xl border border-goalai-border bg-goalai-surface-bright">
      <header className="flex items-baseline justify-between gap-3 border-b border-goalai-border/70 px-3 py-2">
        <h3 className="truncate text-[12px] font-semibold uppercase tracking-wide text-white">
          {title}
        </h3>
        {subtitle && (
          <span className="shrink-0 text-[10px] uppercase tracking-wide text-white/40">
            {subtitle}
          </span>
        )}
      </header>

      {empty ? (
        <p className="px-3 py-4 text-center text-[12px] text-white/50">{empty}</p>
      ) : (
        <>
          {/* Sütun başlıkları — satır düzeniyle birebir hizalı */}
          <div className="flex items-center gap-2 border-b border-goalai-border/40 px-3 py-1 text-[9px] uppercase tracking-wide text-white/30">
            <span className="w-[62px] shrink-0">Tarih</span>
            <span className="min-w-0 flex-1">Ev Sahibi</span>
            <span className="min-w-0 flex-1">Deplasman</span>
            <span className="w-9 shrink-0 text-center">İY</span>
            <span className="w-10 shrink-0 text-center">MS</span>
          </div>
          <ul className="divide-y divide-goalai-border/40">{children}</ul>
        </>
      )}
    </section>
  );
}

/** Maçtaki/ilgili takım — çok hafif mavi vurgu. */
const HIGHLIGHT = "text-sky-200";

/**
 * İlk yarı skorunu biçimler. HER İKİ değer de gelmişse "h-a", aksi hâlde null → "—".
 * 0 GEÇERLİ bir skordur (null ile karıştırılmaz); hesaplama/tahmin yapılmaz.
 */
function formatHalfTime(home?: number | null, away?: number | null): string | null {
  if (home == null || away == null) return null;
  return `${home}-${away}`;
}

/**
 * Tek maç satırı: TARİH | EV SAHİBİ | DEPLASMAN | İY | MS + altta ORGANİZASYON.
 * Sonuç rozeti (W/D/L) YOKTUR — kullanıcı sonucu skordan okur.
 *
 * Organizasyon adı ikinci satıra alınır: mobilde beş sütunun yanına altıncı bir
 * sütun sığmıyor ("UEFA Champions League" tek başına satırı taşırıyordu). Ad
 * backend'in verdiği GERÇEK competition değeridir; kısaltılmaz, sınıflandırılmaz,
 * yalnız kutuya sığmazsa görsel olarak kırpılır.
 */
function MatchRow({
  date,
  homeName,
  awayName,
  score,
  halfTime,
  competition,
  highlight,
}: {
  date: string;
  homeName: string;
  awayName: string;
  score: string;
  /** Backend'in verdiği ilk yarı skoru; yoksa null → "—". Frontend ÜRETMEZ. */
  halfTime: string | null;
  /** Backend'in verdiği gerçek turnuva adı; boşsa hiç gösterilmez. */
  competition?: string | null;
  /** Bu satırda vurgulanacak taraf (takımın kendi form listesinde kullanılır). */
  highlight?: "home" | "away";
}) {
  const comp = competition?.trim();

  return (
    <li className="px-3 py-2">
      <div className="flex items-center gap-2 text-[12px]">
        <span className="w-[62px] shrink-0 text-[10.5px] tabular-nums text-white/45">{date}</span>
        <span
          className={`min-w-0 flex-1 truncate ${
            highlight === "home" ? `font-semibold ${HIGHLIGHT}` : "text-white/85"
          }`}
        >
          {homeName}
        </span>
        <span
          className={`min-w-0 flex-1 truncate ${
            highlight === "away" ? `font-semibold ${HIGHLIGHT}` : "text-white/85"
          }`}
        >
          {awayName}
        </span>
        {/* İlk yarı — backend değeri; sağlayıcı vermediyse "—". Asla türetilmez. */}
        <span className="w-9 shrink-0 text-center font-mono text-[11px] tabular-nums text-white/45">
          {halfTime ?? "—"}
        </span>
        <span className="w-10 shrink-0 text-center font-mono text-[12.5px] font-semibold tabular-nums text-white">
          {score}
        </span>
      </div>
      {comp && (
        <div className="mt-0.5 truncate pl-[70px] text-[9.5px] uppercase tracking-wide text-white/30">
          {comp}
        </div>
      )}
    </li>
  );
}

// ── Takımın lig formu ─────────────────────────────────────────────────────────

/**
 * `LastMatchDto` maçı TAKIM PERSPEKTİFİNDEN taşır: `opponent` + `isHome` + `score`
 * ("attığı-yediği"). Ev/deplasman sütunları için bu alanlar yeniden YÖNLENDİRİLİR —
 * yeni veri üretilmez, yalnız aynı gerçek değerler doğru sütuna yazılır.
 */
function orientRow(teamName: string, m: LastMatchDto) {
  const [gf, ga] = m.score.split("-");
  return m.isHome
    ? { homeName: teamName, awayName: m.opponent, score: `${gf}-${ga}`, highlight: "home" as const }
    : { homeName: m.opponent, awayName: teamName, score: `${ga}-${gf}`, highlight: "away" as const };
}

/**
 * Takımın LİG formu. Backend listeyi takımın kendi ulusal ligiyle SÜZER; burada
 * filtreleme yapılmaz. Ligde 5'ten az oynanmış maç varsa başlık gerçek sayıyı söyler
 * ("Ligde Son 3 Maç") — eksik başka turnuvadan TAMAMLANMAZ.
 *
 * `leagueName` null ise backend takımın ligini çözememiştir; o zaman liste süzülmemiştir
 * ve başlıkta "ligde" DENMEZ.
 */
function TeamLeagueForm({
  teamName,
  matches,
  leagueName,
}: {
  teamName: string;
  matches: LastMatchDto[];
  leagueName?: string | null;
}) {
  const shown = matches.slice(0, 5);
  const league = leagueName?.trim();

  const subtitle =
    shown.length === 0
      ? undefined
      : league
        ? `Ligde Son ${shown.length} Maç`
        : `Son ${shown.length} Maç`;

  return (
    <Section
      title={teamName}
      subtitle={subtitle}
      empty={
        shown.length === 0
          ? league
            ? `${league} kapsamında tamamlanmış maç verisi bulunmuyor.`
            : "Bu takım için tamamlanmış maç verisi bulunmuyor."
          : undefined
      }
    >
      {shown.map((m, i) => {
        const r = orientRow(teamName, m);
        return (
          <MatchRow
            key={`${m.matchId ?? i}`}
            date={m.date}
            homeName={r.homeName}
            awayName={r.awayName}
            score={r.score}
            halfTime={formatHalfTime(m.halfTimeHomeScore, m.halfTimeAwayScore)}
            competition={m.competition}
            highlight={r.highlight}
          />
        );
      })}
    </Section>
  );
}

// ── Puan Durumu (önceki turda onaylandı — korunuyor) ──────────────────────────

type StandingsTab = "table" | "form";

const FORM_LETTER: Record<string, string> = {
  W: "bg-formax-green/20 text-formax-green",
  D: "bg-formax-amber/20 text-formax-amber",
  L: "bg-formax-red/20 text-formax-red",
};

const ROW_HIGHLIGHT = "bg-sky-400/[0.09]";

/**
 * PUAN DURUMU — ligin TAMAMI. Satırlar backend'in verdiği sırada gösterilir; burada
 * yeniden sıralama/hesap YAPILMAZ.
 *
 * Avrupa kupası maçında iki takım farklı liglerde olur ve maçın kendi turnuvasının puan
 * durumu yoktur; backend o durumda takımların KENDİ lig tablolarını gönderir (ör. Süper Lig
 * + Ligue 1) ve her tablo ayrı başlıkla çizilir.
 */
function StandingsPanel({ standing }: { standing: MatchDetailDto["standing"] }) {
  const [tab, setTab] = useState<StandingsTab>("table");
  if (!standing) return null;

  // Yeni alan `tables`; eski yanıtlarla uyum için `tableSlice`e düşülür.
  const tables: StandingTableDto[] =
    standing.tables && standing.tables.length > 0
      ? standing.tables
      : standing.tableSlice.length > 0
        ? [
            {
              leagueId: standing.leagueId,
              leagueName: standing.leagueName ?? "",
              seasonYear: standing.seasonYear,
              rows: standing.tableSlice,
            },
          ]
        : [];

  if (tables.length === 0) return null;

  const multi = tables.length > 1;

  return (
    <section className="overflow-hidden rounded-xl border border-goalai-border bg-goalai-surface-bright">
      <div className="flex items-center gap-1 border-b border-goalai-border/70 px-2 py-1.5">
        <TabButton active={tab === "table"} onClick={() => setTab("table")}>
          Puan Durumu
        </TabButton>
        <TabButton active={tab === "form"} onClick={() => setTab("form")}>
          Form
        </TabButton>
      </div>

      {tables.map((t) => {
        // Boşluk kontrolü TABLO BAŞINA yapılır: aynı ekranda başlamış bir lig (Süper Lig)
        // ile henüz başlamamış bir lig (Ligue 1) yan yana gelebilir. Genel kontrol,
        // başlamış ligin gerçek tablosunu da gizliyordu.
        const hasPlayed = t.rows.some((r) => r.played > 0);
        const hasForm = t.rows.some((r) => (r.form ?? "").trim().length > 0);

        return (
          <div key={`${t.leagueId}-${t.seasonYear}`}>
            {/* Tek tabloda başlık gereksiz; iki lig varsa hangisi olduğu yazılmalı. */}
            {multi && t.leagueName && (
              <h4 className="border-b border-goalai-border/40 bg-white/[0.02] px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wide text-white/50">
                {t.leagueName}
              </h4>
            )}
            {!hasPlayed ? (
              <p className="px-3 py-4 text-center text-[12px] text-white/50">
                Bu lig bu sezon henüz başlamadı — puan durumu oluşmadı.
              </p>
            ) : tab === "table" ? (
              <StandingsTable rows={t.rows} />
            ) : hasForm ? (
              <StandingsForm rows={t.rows} />
            ) : (
              <p className="px-3 py-4 text-center text-[12px] text-white/50">
                Bu lig için form verisi henüz gelmedi.
              </p>
            )}
          </div>
        );
      })}
    </section>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`rounded-lg px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wide transition-colors ${
        active ? "bg-goalai-accent/15 text-goalai-accent" : "text-white/45 hover:text-white/70"
      }`}
    >
      {children}
    </button>
  );
}

function StandingsTable({ rows }: { rows: TeamStandingDto[] }) {
  return (
    <div className="overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
      <table className="w-full min-w-[330px] border-collapse">
        <thead>
          <tr className="text-[9.5px] uppercase tracking-wide text-white/35">
            <th className="w-7 px-2 py-1.5 text-left font-semibold">#</th>
            <th className="px-1 py-1.5 text-left font-semibold">Takım</th>
            <th className="w-7 py-1.5 text-center font-semibold">O</th>
            <th className="w-7 py-1.5 text-center font-semibold">G</th>
            <th className="w-7 py-1.5 text-center font-semibold">B</th>
            <th className="w-7 py-1.5 text-center font-semibold">M</th>
            <th className="w-9 py-1.5 text-center font-semibold">AV</th>
            <th className="w-8 px-2 py-1.5 text-center font-semibold">P</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr
              key={`${r.position}-${r.teamName}`}
              className={`border-t border-goalai-border/40 text-[11.5px] ${
                r.isHighlighted ? ROW_HIGHLIGHT : ""
              }`}
            >
              <td className="px-2 py-1.5 tabular-nums text-white/45">{r.position}</td>
              <td
                className={`max-w-[132px] truncate px-1 py-1.5 ${
                  r.isHighlighted ? "font-semibold text-white" : "text-white/85"
                }`}
              >
                {r.teamName}
              </td>
              <td className="py-1.5 text-center tabular-nums text-white/55">{r.played}</td>
              <td className="py-1.5 text-center tabular-nums text-white/55">{r.won}</td>
              <td className="py-1.5 text-center tabular-nums text-white/55">{r.drawn}</td>
              <td className="py-1.5 text-center tabular-nums text-white/55">{r.lost}</td>
              <td className="py-1.5 text-center tabular-nums text-white/55">
                {r.goalDifference > 0 ? `+${r.goalDifference}` : r.goalDifference}
              </td>
              <td className="px-2 py-1.5 text-center font-bold tabular-nums text-white">
                {r.points}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Backend'in `form` dizisi; satır sırası DEĞİŞTİRİLMEZ (yeniden sıralamak hesap olurdu). */
function StandingsForm({ rows }: { rows: TeamStandingDto[] }) {
  return (
    <ul className="divide-y divide-goalai-border/40">
      {rows.map((r) => {
        const letters = (r.form ?? "").trim().split("").slice(-5);
        return (
          <li
            key={`${r.position}-${r.teamName}-form`}
            className={`flex items-center gap-2 px-2 py-1.5 text-[11.5px] ${
              r.isHighlighted ? ROW_HIGHLIGHT : ""
            }`}
          >
            <span className="w-6 shrink-0 tabular-nums text-white/45">{r.position}</span>
            <span
              className={`min-w-0 flex-1 truncate ${
                r.isHighlighted ? "font-semibold text-white" : "text-white/85"
              }`}
            >
              {r.teamName}
            </span>
            <span className="flex shrink-0 items-center gap-1">
              {letters.length > 0 ? (
                letters.map((c, i) => (
                  <span
                    key={i}
                    className={`flex h-4 w-4 items-center justify-center rounded text-[9px] font-bold ${
                      FORM_LETTER[c] ?? "bg-white/10 text-white/60"
                    }`}
                  >
                    {c}
                  </span>
                ))
              ) : (
                <span className="text-[10px] text-white/35">—</span>
              )}
            </span>
          </li>
        );
      })}
    </ul>
  );
}
