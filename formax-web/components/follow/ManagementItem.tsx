"use client";

import { memo } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { HeartIcon } from "@/components/discover/icons";
import type { ManageEntity } from "./types";

/**
 * ManagementItem — takip edilen tek varlık (takım/lig/maç). Kalp ikonu takibi
 * anında (optimistic) kaldırır/geri getirir. `memo` ile liste performansı korunur.
 */
export const ManagementItem = memo(function ManagementItem({
  entity,
  isFollowing,
  onToggle,
}: {
  entity: ManageEntity;
  isFollowing: boolean;
  onToggle: (entity: ManageEntity) => void;
}) {
  const reduce = useReducedMotion();
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-3 py-3">
      <TeamCrest name={entity.name} logoUrl={entity.logoUrl} size={40} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[14px] font-semibold text-text-primary">{entity.name}</p>
        <p className="text-[12px] text-text-muted">{entity.subtitle}</p>
      </div>
      <motion.button
        type="button"
        whileTap={reduce ? undefined : { scale: 0.9 }}
        onClick={() => onToggle(entity)}
        aria-pressed={isFollowing}
        aria-label={isFollowing ? "Takibi bırak" : "Takip et"}
        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full transition-colors ${
          isFollowing ? "bg-goalai-accent/15 text-goalai-accent" : "bg-white/[0.06] text-text-muted"
        }`}
      >
        <HeartIcon size={18} className={isFollowing ? "fill-current" : ""} />
      </motion.button>
    </div>
  );
});
