"use client";

import { motion } from "framer-motion";
import type { PredictionTab, PredictionCounts } from "@/types/predictions";

interface TabDef {
  key: PredictionTab;
  label: string;
  count?: number;
}

/**
 * PredictionTabs — Segmented Control (AKTİF / BEKLEYEN / TÜMÜ).
 * Seçili alan `spring` animasyonu ile kayar (layoutId). Default: 'active'.
 */
export function PredictionTabs({
  active,
  counts,
  onChange,
}: {
  active: PredictionTab;
  counts: PredictionCounts;
  onChange: (t: PredictionTab) => void;
}) {
  const tabs: TabDef[] = [
    { key: "active", label: "Aktif", count: counts.active },
    { key: "pending", label: "Bekleyen", count: counts.pending },
    { key: "all", label: "Tümü" },
  ];

  return (
    <div className="flex gap-1.5 rounded-[20px] bg-goalai-surface-bright p-1">
      {tabs.map((t) => {
        const isActive = active === t.key;
        return (
          <button
            key={t.key}
            type="button"
            onClick={() => onChange(t.key)}
            className="relative flex flex-1 items-center justify-center gap-1.5 rounded-2xl px-2 py-2"
          >
            {isActive && (
              <motion.span
                layoutId="predTabPill"
                transition={{ type: "spring", stiffness: 380, damping: 32 }}
                className="absolute inset-0 rounded-2xl bg-goalai-accent"
              />
            )}
            <span
              className={`relative z-10 text-[13px] font-semibold uppercase tracking-wide ${
                isActive ? "text-[#0a0e16]" : "text-text-secondary"
              }`}
            >
              {t.label}
            </span>
            {t.count != null && (
              <span
                className={`relative z-10 flex h-4 min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-bold ${
                  isActive ? "bg-[#0a0e16]/20 text-[#0a0e16]" : "bg-white/10 text-text-secondary"
                }`}
              >
                {t.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
