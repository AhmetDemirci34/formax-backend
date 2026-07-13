import { Sparkline } from "@/components/ui/Sparkline";

export type PredictionTone = "green" | "purple";

interface PredictionCardProps {
  code: string;
  description: string;
  percent: string;
  odds: string;
  tone: PredictionTone;
  data: number[];
}

const TONE_TEXT: Record<PredictionTone, string> = {
  green: "text-neon",
  purple: "text-signal-purple",
};

export function PredictionCard({ code, description, percent, odds, tone, data }: PredictionCardProps) {
  const toneText = TONE_TEXT[tone];
  return (
    <div className="relative flex min-w-0 flex-1 flex-col items-center rounded-2xl border border-white/[0.07] bg-gradient-to-b from-white/[0.06] to-white/[0.015] px-2.5 py-2.5 text-center ring-1 ring-inset ring-white/[0.03]">
      <span className={`text-[12px] font-extrabold uppercase leading-none tracking-wide ${toneText}`}>
        {code}
      </span>
      <span className="mt-1.5 flex min-h-[22px] items-start justify-center text-[9px] font-medium leading-[1.2] text-text-muted">
        {description}
      </span>
      <span className={`mt-1.5 text-[21px] font-black leading-none tabular-nums ${toneText}`}>
        {percent}
      </span>
      <span className="mt-1 text-[12px] font-bold tabular-nums text-neon">{odds}</span>
      <div className={`mt-1.5 w-full ${toneText}`}>
        <Sparkline data={data} height={16} />
      </div>
    </div>
  );
}
