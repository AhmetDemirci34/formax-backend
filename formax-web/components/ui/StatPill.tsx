import type { ReactNode } from "react";

interface StatPillProps {
  icon: ReactNode;
  value: string;
  label: string;
}

/**
 * FORMAX · StatPill (07, shared)
 * Küçük istatistik kartı — ikon + değer + etiket. AI Comment / AI Expectations kullanır.
 * Hero ile aynı glass/border dili; Design Token, hardcoded renk yok.
 */
export function StatPill({ icon, value, label }: StatPillProps) {
  return (
    <div className="flex flex-col gap-1.5 rounded-2xl border border-white/[0.06] bg-white/[0.03] px-2.5 py-3">
      <span className="text-neon">{icon}</span>
      <span className="text-[17px] font-extrabold leading-none tabular-nums text-text-primary">
        {value}
      </span>
      <span className="text-[9px] font-medium leading-tight text-text-muted">{label}</span>
    </div>
  );
}
