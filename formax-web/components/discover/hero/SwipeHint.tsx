"use client";

import { motion } from "framer-motion";

/**
 * FORMAX · SwipeHint (05, new)
 * Kartın kaydırılabildiğini bilinçaltına veren minimal gösterge — sayfa noktası DEĞİL.
 * Üç kısa rounded bar, soldan-sağa dalga (staggered) ile "kart hareket ediyor" hissi.
 * Çok düşük opacity, premium; yazı yok.
 */
export function SwipeHint() {
  return (
    <div className="flex items-center justify-center gap-1.5 pt-0.5" aria-hidden>
      {[0, 1, 2].map((i) => (
        <motion.span
          key={i}
          className="h-1 w-5 rounded-full bg-text-secondary"
          animate={{ opacity: [0.14, 0.42, 0.14], scaleX: [0.85, 1.1, 0.85] }}
          transition={{ duration: 1.6, repeat: Infinity, delay: i * 0.22, ease: "easeInOut" }}
        />
      ))}
    </div>
  );
}
