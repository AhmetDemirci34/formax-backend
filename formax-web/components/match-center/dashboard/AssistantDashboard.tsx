"use client";

import { motion } from "framer-motion";
import type { MatchAction } from "../aiContext";
import { AssistantCard } from "./AssistantCard";
import { ActionGrid } from "./ActionGrid";

/**
 * AssistantDashboard — ana görünüm (activeView === 'dashboard').
 * AssistantCard + ActionGrid. Bir aksiyona basılınca sayfa activeView'i
 * değiştirir ve bu blok unmount olur (Teknik Doküman §3).
 */
export function AssistantDashboard({ onSelect }: { onSelect: (action: MatchAction) => void }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: 8 }}
      transition={{ duration: 0.25, ease: "easeOut" }}
      className="flex h-full w-full flex-col gap-4 overflow-y-auto"
    >
      <AssistantCard />
      <ActionGrid onSelect={onSelect} />
    </motion.div>
  );
}
