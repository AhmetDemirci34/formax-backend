"use client";

// FORMAX · Sonuç kartı — bitmiş bir maçın listedeki tek satırı.
//
// TEK HEDEF: kullanıcıyı BİTMİŞ MAÇ ÖZETİ ekranına götürmek. Kartın tamamı ve
// "MAÇ ÖZETİ" butonu AYNI rotaya gider: /match/{matchId}. Eski /match/{id}/ai
// rotası KULLANILMAZ.
//
// Hiçbir alan üretilmez: skorlar, İY skorları, lig ve aşama backend'den gelir.
// İY yoksa satır hiç gösterilmez — "İY 0-0" uydurulmaz.

import { TeamCrest } from "@/components/ui/TeamCrest";
import { istanbulTime } from "@/lib/matches/resultDays";
import type { MatchResultItemDto } from "@/lib/api/matchResults";

interface Props {
  result: MatchResultItemDto;
  onOpen: (matchId: number) => void;
}

export function ResultCard({ result: r, onOpen }: Props) {
  const hasHalfTime = r.halfTimeHomeScore != null && r.halfTimeAwayScore != null;

  return (
    <article className="w-full max-w-full overflow-hidden rounded-[14px] bg-bg-glass">
      {/* Üst şerit: lig · aşama · saat · Maç Bitti */}
      <header className="flex items-center gap-2 border-b border-white/[0.06] px-3 py-2">
        <h3 className="min-w-0 flex-1 truncate text-[11.5px] font-semibold text-text-secondary">
          <span className="text-text-primary">{r.leagueName || "—"}</span>
          {r.matchTypeLabel ? (
            <>
              <span className="mx-1 text-text-muted">·</span>
              {r.matchTypeLabel}
            </>
          ) : null}
        </h3>
        <span className="shrink-0 whitespace-nowrap text-[11px] tabular-nums text-text-muted">
          {istanbulTime(r.matchDateUtc)}
        </span>
        <span className="shrink-0 whitespace-nowrap rounded-[6px] bg-white/[0.07] px-1.5 py-[2px] text-[9px] font-bold uppercase tracking-wide text-text-muted">
          Maç Bitti
        </span>
      </header>

      {/* Gövde → Maç Özeti */}
      <button
        type="button"
        onClick={() => onOpen(r.matchId)}
        aria-label={`${r.homeTeam.name} ${r.homeScore}-${r.awayScore} ${r.awayTeam.name} maç özeti`}
        className="flex w-full items-center gap-2 px-3 py-2.5 text-left transition-colors hover:bg-white/[0.035]"
      >
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <TeamLine name={r.homeTeam.name} logoUrl={r.homeTeam.logoUrl} score={r.homeScore} />
          <TeamLine name={r.awayTeam.name} logoUrl={r.awayTeam.logoUrl} score={r.awayScore} />
        </div>
      </button>

      {/* Alt şerit: İY (varsa) + video işareti + MAÇ ÖZETİ */}
      <footer className="flex flex-wrap items-center gap-x-2 gap-y-1.5 border-t border-white/[0.06] px-3 py-2">
        {hasHalfTime ? (
          <span className="whitespace-nowrap text-[11px] tabular-nums text-text-muted">
            İY {r.halfTimeHomeScore}-{r.halfTimeAwayScore}
          </span>
        ) : null}

        {/* İşaret YALNIZ gerçekten oynatılabilir video varsa. Yoksa hiç gösterilmez. */}
        {r.hasPlayableOfficialVideo ? (
          <span className="flex shrink-0 items-center gap-1 whitespace-nowrap rounded-[6px] bg-neon/[0.10] px-1.5 py-[2px] text-[9.5px] font-bold tracking-wide text-neon">
            <PlayGlyph />
            Video var
          </span>
        ) : null}

        <button
          type="button"
          onClick={() => onOpen(r.matchId)}
          className="ml-auto shrink-0 whitespace-nowrap rounded-[8px] border border-neon/25 bg-neon/[0.07] px-2.5 py-[5px] text-[10.5px] font-bold tracking-wide text-neon transition-colors hover:bg-neon/[0.14]"
        >
          MAÇ ÖZETİ
        </button>
      </footer>
    </article>
  );
}

function TeamLine({
  name,
  logoUrl,
  score,
}: {
  name: string;
  logoUrl?: string | null;
  score: number;
}) {
  return (
    <div className="flex items-center gap-2">
      <TeamCrest name={name} logoUrl={logoUrl} size={20} />
      <span className="min-w-0 flex-1 truncate text-[13.5px] font-medium text-text-primary">
        {name || "—"}
      </span>
      <span className="shrink-0 whitespace-nowrap text-[16px] font-bold tabular-nums text-text-primary">
        {score}
      </span>
    </div>
  );
}

function PlayGlyph() {
  return (
    <svg width="8" height="8" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M8 5l11 7-11 7z" />
    </svg>
  );
}
