"use client";

// FORMAX · Sonuç satırı — lig panelinin içindeki tek bitmiş maç.
//
// TEK HEDEF: kullanıcıyı BİTMİŞ MAÇ ÖZETİ ekranına götürmek. Satırın gövdesi ve
// "MAÇ ÖZETİ" butonu AYNI rotaya gider: /match/{matchId}. Eski /match/{id}/ai
// rotası KULLANILMAZ.
//
// Lig adı SATIRDA YOKTUR: onu panel başlığı taşır (bkz. ResultLeagueGroup). Satırda
// yalnız o maça ait olan bilgi durur — aşama, saat, takımlar, skor, İY.
//
// Hiçbir alan üretilmez. İY yoksa satır hiç gösterilmez — "İY 0-0" uydurulmaz.

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
    <article className="w-full max-w-full">
      {/* Üst satır: saat · aşama · Maç Bitti */}
      <div className="flex items-center gap-2 px-3 pt-2">
        <span className="shrink-0 whitespace-nowrap text-[12px] font-semibold tabular-nums text-text-secondary">
          {istanbulTime(r.matchDateUtc)}
        </span>
        {r.matchTypeLabel ? (
          <span className="min-w-0 flex-1 truncate text-[11px] text-text-muted">
            {r.matchTypeLabel}
          </span>
        ) : (
          <span className="min-w-0 flex-1" />
        )}
        <span className="shrink-0 whitespace-nowrap rounded-[6px] bg-white/[0.07] px-1.5 py-[2px] text-[9px] font-bold uppercase tracking-wide text-text-muted">
          Maç Bitti
        </span>
      </div>

      {/* Gövde → Maç Özeti */}
      <button
        type="button"
        onClick={() => onOpen(r.matchId)}
        aria-label={`${r.homeTeam.name} ${r.homeScore}-${r.awayScore} ${r.awayTeam.name} maç özeti`}
        className="flex w-full flex-col gap-1.5 px-3 py-2 text-left transition-colors hover:bg-white/[0.035]"
      >
        <TeamLine name={r.homeTeam.name} logoUrl={r.homeTeam.logoUrl} score={r.homeScore} />
        <TeamLine name={r.awayTeam.name} logoUrl={r.awayTeam.logoUrl} score={r.awayScore} />
      </button>

      {/* Alt satır: İY (varsa) + video işareti + MAÇ ÖZETİ */}
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5 px-3 pb-2">
        {hasHalfTime ? (
          <span className="whitespace-nowrap text-[11px] tabular-nums text-text-muted">
            İY {r.halfTimeHomeScore}-{r.halfTimeAwayScore}
          </span>
        ) : null}

        {/* İşaret YALNIZ backend "doğrulanmış ve oynatılabilir video var" derse.
            Rejected / NeedsManualReview kayıtlar bu bayrağı ASLA true yapmaz. */}
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
      </div>
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
