"use client";

import { motion, useReducedMotion } from "framer-motion";
import { AI_GREETING } from "../aiContext";

/**
 * AssistantCard — statik karşılama kartı (Teknik Doküman §2.2: "statik ama
 * belirgin"). Yalnızca başlık yanındaki yeşil nokta pulse yapar (§6 Indicator).
 * Karşılama metni: 18px / line-height 1.4 (§12).
 */
export function AssistantCard() {
  const reduce = useReducedMotion();
  return (
    <div className="w-full rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4">
      <div className="mb-2 flex items-center gap-2">
        <motion.span
          className="h-2.5 w-2.5 rounded-full bg-goalai-accent"
          style={{ boxShadow: "0 0 8px var(--goalai-accent)" }}
          animate={reduce ? undefined : { opacity: [1, 0.35, 1], scale: [1, 1.15, 1] }}
          transition={{ duration: 1.6, ease: "easeInOut", repeat: Infinity }}
        />
        <span className="text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
          GOALAI Asistan
        </span>
      </div>
      <p className="text-[18px] font-medium leading-[1.4] text-white">{AI_GREETING}</p>
    </div>
  );
}
