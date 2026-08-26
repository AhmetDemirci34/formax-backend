"use client";

import type { ComponentType } from "react";
import { motion } from "framer-motion";

export interface SegmentTab<T extends string> {
  key: T;
  label: string;
  Icon?: ComponentType<{ size?: number; className?: string }>;
}

/**
 * FollowSegments — segment kontrol (Framer Motion `layoutId` ile lime "pill").
 * Feed ve Management aynı anda mount olabildiği için `id` ile ayrı layoutId kullanılır.
 */
export function FollowSegments<T extends string>({
  id,
  tabs,
  active,
  onChange,
}: {
  id: string;
  tabs: SegmentTab<T>[];
  active: T;
  onChange: (t: T) => void;
}) {
  return (
    <div className="flex gap-1.5 overflow-x-auto pb-0.5 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
      {tabs.map((t) => {
        const isActive = active === t.key;
        const Icon = t.Icon;
        return (
          <button
            key={t.key}
            type="button"
            onClick={() => onChange(t.key)}
            className="relative flex shrink-0 items-center gap-1.5 rounded-full px-3.5 py-2"
          >
            {isActive && (
              <motion.span
                layoutId={`seg-${id}`}
                transition={{ type: "spring", stiffness: 380, damping: 32 }}
                className="absolute inset-0 rounded-full bg-goalai-accent"
              />
            )}
            {Icon && (
              <Icon
                size={15}
                className={`relative z-10 ${isActive ? "text-[#0a0e16]" : "text-text-secondary"}`}
              />
            )}
            <span
              className={`relative z-10 text-[13px] font-semibold uppercase tracking-wide ${
                isActive ? "text-[#0a0e16]" : "text-text-secondary"
              }`}
            >
              {t.label}
            </span>
          </button>
        );
      })}
    </div>
  );
}
