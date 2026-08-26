"use client";

import { useEffect, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";

/**
 * InfoMessage — Kadro ekranındaki yüzer bildirim (Teknik Doküman §8).
 * 4 saniye sonra opacity-0 ile kendiliğinden kaybolur.
 */
export function InfoMessage({ text, durationMs = 4000 }: { text: string; durationMs?: number }) {
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const t = setTimeout(() => setVisible(false), durationMs);
    return () => clearTimeout(t);
  }, [durationMs]);

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          initial={{ opacity: 0, y: -8 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.4, ease: "easeOut" }}
          role="status"
          className="pointer-events-none absolute left-1/2 top-3 z-40 w-[calc(100%-2rem)] -translate-x-1/2 rounded-xl border border-goalai-accent/40 bg-goalai-surface/95 px-4 py-2.5 text-center text-[13px] text-white/90 shadow-lg backdrop-blur-md"
        >
          {text}
        </motion.div>
      )}
    </AnimatePresence>
  );
}
