import type { ComponentType } from "react";
import {
  TrendingUpIcon,
  GoalIcon,
  UsersIcon,
  FlameIcon,
  PulseIcon,
} from "@/components/discover/icons";
import { TONE_TEXT, type HeroMetricVM, type HeroMetricIcon } from "./heroData";

const ICONS: Record<HeroMetricIcon, ComponentType<{ size?: number; className?: string }>> = {
  news: TrendingUpIcon,
  goal: GoalIcon,
  fans: UsersIcon,
  social: FlameIcon,
  deviation: PulseIcon,
};

/**
 * FORMAX · HeroMetricsGrid (05)
 * PNG'de ConfidenceRing yanındaki 5 metrik şeridi: ikon + label + renkli değer.
 */
export function HeroMetricsGrid({ metrics }: { metrics: HeroMetricVM[] }) {
  return (
    <div className="flex items-stretch rounded-2xl border border-white/[0.07] bg-bg-deep/55 px-0.5 py-2.5 backdrop-blur-sm">
      {metrics.map((m, i) => (
        <HeroMetricItem key={m.label} metric={m} first={i === 0} />
      ))}
    </div>
  );
}

function HeroMetricItem({ metric, first }: { metric: HeroMetricVM; first: boolean }) {
  const Icon = ICONS[metric.icon];
  const tone = TONE_TEXT[metric.tone];
  return (
    <div
      className={`flex min-w-0 flex-1 flex-col items-center gap-1 px-0.5 text-center ${
        first ? "" : "border-l border-white/[0.06]"
      }`}
    >
      <Icon size={13} className={tone} />
      <span className="text-[6px] font-semibold uppercase leading-[1.1] tracking-tight text-text-muted">
        {metric.label}
      </span>
      <span className={`text-[7px] font-extrabold uppercase leading-[1.05] tracking-tight ${tone}`}>
        {metric.value}
      </span>
    </div>
  );
}
