"use client";

import { motion } from "framer-motion";

/**
 * LoadingSkeleton — kart formunda (100px) gri gradient iskeletler; stagger ile belirir.
 */
export function LoadingSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="flex flex-col gap-3">
      <div className="h-4 w-32 animate-pulse rounded bg-white/[0.06]" />
      {Array.from({ length: count }).map((_, i) => (
        <motion.div
          key={i}
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.25, delay: 0.08 * i, ease: "easeOut" }}
          className="h-[100px] w-full animate-pulse rounded-2xl bg-gradient-to-r from-white/[0.05] to-white/[0.02]"
        />
      ))}
    </div>
  );
}
