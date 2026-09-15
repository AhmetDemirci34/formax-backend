"use client";

import { useState } from "react";
import type { OutcomeCandidateDto, OutcomeSnapshotDto } from "@/types/outcomes";
import { OUTCOME_DISCLAIMER, OUTCOME_EXPECTATION_LABEL } from "@/lib/outcomes/outcomeView";

/**
 * AI OLASI SONUÇLAR — üç ana kart (Maç Sonucu · Gol Beklentisi · İki Takımın Gol Durumu) + "Tüm Olasılıkları Gör".
 *
 * KURALLAR:
 *  • Kartlar, sırası ve yüzdeleri backend snapshot'ından gelir; burada sıralama/filtre/hesap YOK.
 *  • Çifte şans (1X/X2/12) ana kartta ASLA yok (backend koymaz); yalnız ayrıntıda "Diğer" ailesinde.
 *  • Gerekçe backend'in gerçek gerekçe kodlarından ürettiği metindir; ekran cümle yazmaz.
 *  • Oran gösterilmez: bookmaker oranı AI tahmini değildir.
 */
export function OutcomeCards({
  snapshot,
  selectedMarkets,
  onSelect,
  selectionDisabled,
  compact = false,
}: {
  snapshot: OutcomeSnapshotDto;
  selectedMarkets?: ReadonlySet<string>;
  onSelect?: (card: OutcomeCandidateDto) => void;
  selectionDisabled?: boolean;
  compact?: boolean;
}) {
  const [open, setOpen] = useState(false);

  return (
    <div className="flex w-full max-w-full flex-col gap-2" data-snapshot-id={snapshot.snapshotId ?? ""} data-model-version={snapshot.modelVersion}>
      <ul className="flex flex-col gap-2">
        {snapshot.mainCards.map((c) => (
          <li key={c.family}>
            <OutcomeCard
              card={c}
              compact={compact}
              active={selectedMarkets?.has(c.market) ?? false}
              onSelect={onSelect && c.marketKey ? () => onSelect(c) : undefined}
              disabled={selectionDisabled}
            />
          </li>
        ))}
      </ul>

      {snapshot.limitation && (
        <p className="px-1 text-[11px] leading-relaxed text-white/60" data-testid="outcome-limitation">
          {snapshot.limitation}
        </p>
      )}

      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        className="self-start rounded-lg px-1 py-1 text-[12px] font-semibold text-goalai-accent underline-offset-2 hover:underline"
      >
        {open ? "Tüm Olasılıkları Gizle" : "Tüm Olasılıkları Gör"}
      </button>

      {open && (
        <div className="flex flex-col gap-3 rounded-xl border border-white/[0.07] bg-black/20 p-3" data-testid="all-outcomes">
          {snapshot.families.map((f) => (
            <section key={f.family}>
              <h4 className="mb-1 text-[10.5px] font-bold uppercase tracking-wide text-white/50">{f.title}</h4>
              <ul className="flex flex-col">
                {f.items.map((i) => (
                  <li key={i.market} className="flex items-center justify-between gap-2 py-[3px] text-[12px]">
                    <span className="min-w-0 truncate text-white/80">{i.market}</span>
                    <span className="shrink-0 font-bold tabular-nums text-text-primary">%{i.probability}</span>
                  </li>
                ))}
              </ul>
            </section>
          ))}
          {snapshot.topScores.length > 0 && (
            <section>
              <h4 className="mb-1 text-[10.5px] font-bold uppercase tracking-wide text-white/50">En Olası Skorlar</h4>
              <p className="flex flex-wrap gap-x-3 gap-y-1 text-[12px] text-white/80">
                {snapshot.topScores.map((s) => (
                  <span key={`${s.home}-${s.away}`} className="tabular-nums">
                    {s.home}-{s.away} <span className="text-white/50">%{s.probability}</span>
                  </span>
                ))}
              </p>
            </section>
          )}
          <p className="text-[10.5px] leading-relaxed text-white/45">{OUTCOME_DISCLAIMER}</p>
        </div>
      )}
    </div>
  );
}

function OutcomeCard({
  card,
  active,
  onSelect,
  disabled,
  compact,
}: {
  card: OutcomeCandidateDto;
  active: boolean;
  onSelect?: () => void;
  disabled?: boolean;
  compact: boolean;
}) {
  const body = (
    <>
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <p className="text-[10px] font-bold uppercase tracking-wide text-white/50">{card.familyTitle}</p>
          <p className={`break-words text-[14px] font-bold leading-tight ${active ? "text-neon" : "text-text-primary"}`}>{card.market}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end">
          <span className={`text-[18px] font-black leading-none tabular-nums ${active ? "text-neon" : "text-text-primary"}`}>%{card.probability}</span>
          <span className="mt-0.5 text-[9.5px] font-semibold uppercase tracking-wide text-white/45">{OUTCOME_EXPECTATION_LABEL}</span>
        </div>
      </div>
      {!compact && card.reason && <p className="mt-1.5 text-left text-[11.5px] leading-relaxed text-white/70">{card.reason}</p>}
      {active && <p className="mt-1 text-left text-[10px] font-semibold uppercase tracking-wide text-neon">Senin seçimin</p>}
    </>
  );
  const cls = `block w-full max-w-full rounded-xl border px-3 py-2.5 text-left transition-colors ${
    active ? "border-neon bg-neon/[0.08]" : "border-white/[0.08] bg-white/[0.03]"
  }`;
  return onSelect ? (
    <button type="button" onClick={onSelect} disabled={disabled} aria-pressed={active} className={`${cls} disabled:opacity-60`}>
      {body}
    </button>
  ) : (
    <div className={cls}>{body}</div>
  );
}
