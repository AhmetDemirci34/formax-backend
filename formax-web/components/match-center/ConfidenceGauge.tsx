"use client";

import { motion, useReducedMotion } from "framer-motion";
import type { AiConfidence } from "./aiContext";

/**
 * ConfidenceGauge — Hero merkezindeki "AI GÜVENİ" göstergesi.
 * Kompakt dairesel gösterge: ~270° yay, altta boşluk (referans hissi), ince stroke,
 * lime→turuncu premium gradient. Sayfa yüklenince 0'dan hedefe dolar.
 */
export function ConfidenceGauge({ score, level }: AiConfidence) {
  const reduce = useReducedMotion();
  const pct = Math.min(100, Math.max(0, score)) / 100;

  const R = 26;
  const C = 2 * Math.PI * R; // ≈163.36
  const ARC = 0.75; // 270° yay (alt 90° boşluk)
  const arcLen = C * ARC;
  const gapLen = C - arcLen;
  // Yay 135°'den başlayıp saat yönünde 45°'ye kadar → boşluk tam altta (90°).
  const ROT = "rotate(135 32 32)";

  return (
    <div className="relative h-[80px] w-[80px] shrink-0">
      {/* yumuşak, geniş arka glow */}
      <motion.div
        aria-hidden
        className="pointer-events-none absolute left-1/2 top-1/2 h-[72px] w-[72px] -translate-x-1/2 -translate-y-1/2 rounded-full"
        style={{ background: "radial-gradient(circle, rgba(204,255,0,.18), transparent 72%)", filter: "blur(14px)" }}
        animate={reduce ? undefined : { opacity: [0.45, 0.7, 0.45], scale: [0.95, 1.08, 0.95] }}
        transition={{ duration: 2.6, ease: "easeInOut", repeat: Infinity }}
      />

      <svg viewBox="0 0 64 64" className="absolute inset-0 h-full w-full" role="img" aria-label={`AI Beklentisi ${score}, ${level}`}>
        <defs>
          <linearGradient id="goalaiGaugeGrad" x1="0.15" y1="0" x2="0.9" y2="1">
            <stop offset="0%" stopColor="#CCFF00" />
            <stop offset="60%" stopColor="#E4D93B" />
            <stop offset="100%" stopColor="#F7A400" />
          </linearGradient>
        </defs>

        {/* boş iz */}
        <circle
          cx="32"
          cy="32"
          r={R}
          fill="none"
          stroke="#262626"
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={`${arcLen} ${gapLen}`}
          transform={ROT}
        />

        {/* dolu progress — yayın pct kadarı */}
        <motion.circle
          cx="32"
          cy="32"
          r={R}
          fill="none"
          stroke="url(#goalaiGaugeGrad)"
          strokeWidth="5"
          strokeLinecap="round"
          transform={ROT}
          style={{ filter: "drop-shadow(0 0 3px rgba(204,255,0,.3))" }}
          initial={{ pathLength: reduce ? pct * ARC : 0 }}
          animate={{ pathLength: pct * ARC }}
          transition={{ duration: 1.2, ease: "easeOut" }}
        />
      </svg>

      {/* iç metin — çemberin tam ortasında */}
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-[8px] font-semibold uppercase tracking-[0.12em] text-white/45">
          AI Beklentisi
        </span>
        <span className="text-[21px] font-bold leading-none text-white">{score}</span>
        <span className="text-[9px] font-bold uppercase tracking-wide text-goalai-accent">
          {level}
        </span>
      </div>
    </div>
  );
}
