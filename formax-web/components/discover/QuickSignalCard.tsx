import type { IconKey } from "./icons";
import { ICONS } from "./icons";

export type SignalColor = "green" | "purple" | "amber" | "blue";

export interface SignalVM {
  key: string;
  iconKey: IconKey;
  label: string;
  value: string;
  caption: string;
  color: SignalColor;
}

const COLOR: Record<SignalColor, string> = {
  green: "text-formax-green",
  purple: "text-[#A855F7]",
  amber: "text-formax-amber",
  blue: "text-[#3B82F6]",
};

// Tek hızlı sinyal kartı — referans: ikon+etiket · büyük renkli değer · küçük kaynak.
export function QuickSignalCard({ signal }: { signal: SignalVM }) {
  const Icon = ICONS[signal.iconKey];
  const c = COLOR[signal.color];
  return (
    <div className="w-[125px] shrink-0 rounded-2xl border border-white/8 bg-white/[0.03] p-3">
      <div className="flex items-start gap-1.5">
        <Icon size={13} className={`${c} mt-px shrink-0`} />
        <span className="text-[9px] font-semibold uppercase leading-tight tracking-wide text-text-muted">
          {signal.label}
        </span>
      </div>
      <div className={`mt-2.5 text-lg font-extrabold leading-none tabular-nums ${c}`}>
        {signal.value}
      </div>
      <div className="mt-1.5 text-[9px] text-text-muted/70">{signal.caption}</div>
    </div>
  );
}
