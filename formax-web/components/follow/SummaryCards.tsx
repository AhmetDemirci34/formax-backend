"use client";

import type { ComponentType } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { ShieldIcon, TrophyIcon, GoalIcon } from "@/components/discover/icons";
import type { ManageFilter } from "./types";

/**
 * SummaryCards — "Takip Ettiklerim" özet kartları (Takımlar / Ligler / Maçlar).
 * Sayımlar gerçek: takımlar `/api/users/me/teams`, maçlar `/api/follows/me`.
 * NOT: Lig takibi backend'de YOK → leaguesCount daima 0 (uydurulmaz).
 */
export function SummaryCards({
  teamsCount,
  leaguesCount,
  matchesCount,
  onOpen,
}: {
  teamsCount: number;
  leaguesCount: number;
  matchesCount: number;
  onOpen: (filter: ManageFilter) => void;
}) {
  return (
    <div className="grid grid-cols-3 gap-3">
      <Card Icon={ShieldIcon} label="Takımlar" count={teamsCount} onClick={() => onOpen("teams")} />
      <Card Icon={TrophyIcon} label="Ligler" count={leaguesCount} onClick={() => onOpen("leagues")} />
      <Card Icon={GoalIcon} label="Maçlar" count={matchesCount} onClick={() => onOpen("matches")} />
    </div>
  );
}

function Card({
  Icon,
  label,
  count,
  onClick,
}: {
  Icon: ComponentType<{ size?: number; className?: string }>;
  label: string;
  count: number;
  onClick: () => void;
}) {
  const reduce = useReducedMotion();
  return (
    <motion.button
      type="button"
      whileTap={reduce ? undefined : { scale: 0.97 }}
      onClick={onClick}
      className="flex flex-col items-center gap-1.5 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-2 py-3.5"
    >
      <Icon size={20} className="text-goalai-accent" />
      <span className="text-[13px] font-bold text-text-primary">{label}</span>
      <span className="text-[11px] font-medium text-text-muted">{count} Takip</span>
    </motion.button>
  );
}
