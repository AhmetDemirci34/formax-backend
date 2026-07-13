import { Sparkline } from "@/components/ui/Sparkline";

export type MetricTone = "green" | "blue" | "purple" | "amber" | "red";

interface MetricStatCardProps {
  label: string;
  value: string;
  valueTag: string;
  data: number[];
  tone: MetricTone;
}

const TONE_TEXT: Record<MetricTone, string> = {
  green: "text-neon",
  blue: "text-signal-blue",
  purple: "text-signal-purple",
  amber: "text-signal-amber",
  red: "text-signal-red",
};

/**
 * FORMAX · MetricStatCard (08, shared)
 * Başlık + büyük değer + ton-renkli etiket + sparkline. Hero ile aynı kart dili.
 * Design Token; hardcoded renk yok.
 */
export function MetricStatCard({ label, value, valueTag, data, tone }: MetricStatCardProps) {
  const toneText = TONE_TEXT[tone];
  return (
    <div className="flex flex-col gap-1.5 rounded-2xl border border-white/[0.06] bg-white/[0.03] p-3">
      <span className="text-[9px] font-medium uppercase leading-tight tracking-wide text-text-muted">
        {label}
      </span>
      <span className="text-[19px] font-extrabold leading-none tabular-nums text-text-primary">
        {value}
      </span>
      <span className={`text-[9px] font-bold uppercase tracking-wide ${toneText}`}>{valueTag}</span>
      <div className={`mt-0.5 ${toneText}`}>
        <Sparkline data={data} height={20} />
      </div>
    </div>
  );
}
