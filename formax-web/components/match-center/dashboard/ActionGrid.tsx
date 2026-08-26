"use client";

import { motion, useReducedMotion } from "framer-motion";
import { MATCH_ACTIONS, type MatchAction, type ActionKey } from "../aiContext";
import {
  BulbIcon,
  TrendIcon,
  GridIcon,
  BroadcastIcon,
  NewsIcon,
  PlayIcon,
} from "../icons";

const ICONS: Record<ActionKey, (p: { className?: string; size?: number }) => React.ReactNode> = {
  analysis: BulbIcon,
  stats: TrendIcon,
  lineup: GridIcon,
  live: BroadcastIcon,
  news: NewsIcon,
  video: PlayIcon,
};

/**
 * ActionGrid — dashboard aksiyonları (Teknik Doküman §4):
 *   • primary  → tam genişlik "AI Maç Analizi"
 *   • grid     → 2×2 (Form, Kadro, Canlı, Son Dakika)
 *   • video    → tam genişlik "ÖNEMLİ ANLARI İZLE" (alt CTA)
 * Gap 12px. Tüm butonlar active:scale-95.
 */
export function ActionGrid({ onSelect }: { onSelect: (action: MatchAction) => void }) {
  const primary = MATCH_ACTIONS.find((a) => a.slot === "primary")!;
  const grid = MATCH_ACTIONS.filter((a) => a.slot === "grid");
  const video = MATCH_ACTIONS.find((a) => a.slot === "video")!;

  return (
    <div className="flex w-full flex-col gap-3">
      <PrimaryButton action={primary} onSelect={onSelect} />

      <div className="grid grid-cols-2 gap-3">
        {grid.map((a) => (
          <GridButton key={a.key} action={a} onSelect={onSelect} />
        ))}
      </div>

      <VideoButton action={video} onSelect={onSelect} />
    </div>
  );
}

function useTap() {
  const reduce = useReducedMotion();
  return reduce ? {} : { whileTap: { scale: 0.95 }, transition: { duration: 0.2, ease: "easeOut" as const } };
}

function PrimaryButton({ action, onSelect }: { action: MatchAction; onSelect: (a: MatchAction) => void }) {
  const Icon = ICONS[action.key];
  const tap = useTap();
  return (
    <motion.button
      {...tap}
      type="button"
      onClick={() => onSelect(action)}
      className="flex h-14 w-full items-center justify-center gap-2.5 rounded-2xl border border-goalai-accent/60 bg-goalai-surface-bright px-4 text-[15px] font-semibold text-white transition-colors hover:border-goalai-accent"
    >
      <Icon className="text-goalai-accent" size={22} />
      <span>{action.label}</span>
    </motion.button>
  );
}

function GridButton({ action, onSelect }: { action: MatchAction; onSelect: (a: MatchAction) => void }) {
  const Icon = ICONS[action.key];
  const tap = useTap();
  return (
    <motion.button
      {...tap}
      type="button"
      onClick={() => onSelect(action)}
      className="flex h-24 w-full flex-col items-center justify-center gap-2 rounded-2xl border border-goalai-border bg-goalai-surface-bright px-3 text-center text-[13px] font-medium text-white/90 transition-colors hover:border-white/25"
    >
      <Icon className="text-goalai-accent" size={24} />
      <span>{action.label}</span>
    </motion.button>
  );
}

function VideoButton({ action, onSelect }: { action: MatchAction; onSelect: (a: MatchAction) => void }) {
  const Icon = ICONS[action.key];
  const tap = useTap();
  return (
    <motion.button
      {...tap}
      type="button"
      onClick={() => onSelect(action)}
      className="flex h-14 w-full items-center justify-center gap-2.5 rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 text-[15px] font-semibold text-white transition-colors hover:border-white/25"
    >
      <Icon className="text-goalai-accent" size={22} />
      <span>{action.label}</span>
    </motion.button>
  );
}
