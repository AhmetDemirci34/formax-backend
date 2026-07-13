type ProbTone = "blue" | "muted" | "red";

interface Segment {
  label: string;
  value: number; // yüzde
  tone: ProbTone;
}

interface WinProbabilityCardProps {
  label?: string;
  segments?: Segment[];
}

const TEXT: Record<ProbTone, string> = {
  blue: "text-signal-blue",
  muted: "text-text-secondary",
  red: "text-signal-red",
};
const BAR: Record<ProbTone, string> = {
  blue: "bg-signal-blue",
  muted: "bg-white/25",
  red: "bg-signal-red",
};

// PNG-2 referansı (statik).
const DEFAULT_SEGMENTS: Segment[] = [
  { label: "MCI", value: 47, tone: "blue" },
  { label: "Beraberlik", value: 27, tone: "muted" },
  { label: "LIV", value: 26, tone: "red" },
];

/**
 * FORMAX · WinProbabilityCard (08)
 * KAZANMA İHTİMALİ — üç sonuç + oransal tri-bar. Hero ile aynı kart dili, Design Token.
 */
export function WinProbabilityCard({
  label = "Kazanma İhtimali",
  segments = DEFAULT_SEGMENTS,
}: WinProbabilityCardProps) {
  return (
    <div className="rounded-2xl border border-white/[0.06] bg-white/[0.03] p-3">
      <span className="text-[9px] font-medium uppercase tracking-wide text-text-muted">{label}</span>

      <div className="mt-2 flex items-end justify-between">
        {segments.map((s) => (
          <div key={s.label} className="flex flex-col items-center gap-0.5">
            <span className={`text-[17px] font-extrabold leading-none tabular-nums ${TEXT[s.tone]}`}>
              %{s.value}
            </span>
            <span className="text-[9px] font-semibold uppercase tracking-wide text-text-muted">
              {s.label}
            </span>
          </div>
        ))}
      </div>

      <div className="mt-2.5 flex h-1.5 overflow-hidden rounded-full">
        {segments.map((s) => (
          <span key={s.label} className={BAR[s.tone]} style={{ width: `${s.value}%` }} />
        ))}
      </div>
    </div>
  );
}
