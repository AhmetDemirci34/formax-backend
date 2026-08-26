"use client";

import { memo } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { ActivityIcon, ChevronRightIcon } from "@/components/discover/icons";
import type { ActivityItem } from "./types";

/**
 * FeedCard — tek aktivite kartı (~80px). Basıldığında okundu işaretlenir ve
 * hedefe (Match Detail) yönlendirir. `memo` ile liste performansı korunur.
 *
 * NOT: Backend notification DTO'sunda `event_type` ve `logo_url` YOK; bu yüzden
 * karta özel ikon/logo yerine nötr aktivite ikonu kullanılır (uydurma yapılmaz).
 */
export const FeedCard = memo(function FeedCard({
  item,
  onOpen,
}: {
  item: ActivityItem;
  onOpen: (item: ActivityItem) => void;
}) {
  const reduce = useReducedMotion();
  return (
    <button
      type="button"
      onClick={() => onOpen(item)}
      className="flex w-full items-center gap-3 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-3 py-3 text-left transition-opacity active:opacity-70"
    >
      {/* Nötr avatar (gerçek logo/event_type yok) */}
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white/[0.04] text-text-secondary">
        <ActivityIcon size={18} />
      </span>

      <div className="min-w-0 flex-1">
        <p className="truncate text-[14px] font-semibold text-text-primary">{item.title}</p>
        <p className="truncate text-[12px] text-text-muted">{item.description}</p>
      </div>

      <div className="flex shrink-0 flex-col items-end gap-1.5">
        <span className="text-[11px] text-text-muted">{relativeTime(item.createdAt)}</span>
        <span className="flex items-center gap-1.5">
          {!item.isRead && (
            <motion.span
              className="h-2 w-2 rounded-full bg-goalai-accent"
              initial={reduce ? false : { scale: 0 }}
              animate={{ scale: [0, 1.2, 1] }}
              transition={{ duration: 0.35, ease: "easeOut" }}
            />
          )}
          <ChevronRightIcon size={16} className="text-text-muted" />
        </span>
      </div>
    </button>
  );
});

function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "";
  const mins = Math.max(0, Math.round((Date.now() - then) / 60000));
  if (mins < 60) return `${mins} dk`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs} sa`;
  return `${Math.round(hrs / 24)} g`;
}
