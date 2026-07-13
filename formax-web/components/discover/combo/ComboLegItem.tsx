import { TeamLogoPair } from "@/components/ui/TeamLogoPair";
import type { ComboLegVM } from "./comboData";

/**
 * FORMAX · ComboLegItem (09, new)
 * Kombindeki tek maç ayağı: saat + iki arma + "home vs away" + bahis tipi/oran.
 * AI Olası Sonuçlar kartlarıyla aynı yüzey dili (border-white/[0.06] · bg-white/[0.03]).
 */
export function ComboLegItem({ time, home, away, betLabel, odds }: ComboLegVM) {
  return (
    <div className="flex w-[118px] shrink-0 flex-col items-center gap-1.5 rounded-2xl border border-white/[0.06] bg-white/[0.03] px-2.5 py-2.5 text-center">
      <span className="text-[9.5px] font-medium text-text-muted">{time}</span>

      <TeamLogoPair home={home} away={away} size={26} showVs={false} />

      <div className="flex flex-col items-center leading-tight">
        <span className="text-[10.5px] font-bold text-text-primary">{home.name}</span>
        <span className="text-[7.5px] font-semibold uppercase tracking-wide text-text-muted">vs</span>
        <span className="text-[10.5px] font-bold text-text-primary">{away.name}</span>
      </div>

      <div className="mt-auto flex w-full items-center justify-center gap-1.5 border-t border-white/[0.06] pt-1.5">
        <span className="text-[10.5px] font-bold text-text-secondary">{betLabel}</span>
        <span className="text-[12.5px] font-extrabold tabular-nums text-neon">{odds}</span>
      </div>
    </div>
  );
}
