"use client";

import { motion } from "framer-motion";

interface Props {
  /** 0–100 */
  value: number;
  label: string;
  size?: number;
}

const R = 52;
const CIRC = 2 * Math.PI * R;

/** Güven seviyesine göre halka rengi (token). */
function toneVar(value: number): string {
  if (value >= 70) return "var(--neon)";
  if (value >= 40) return "var(--signal-amber)";
  return "var(--signal-red)";
}

/**
 * FORMAX · ConfidenceRing (05)
 * PNG'deki "AI GÜVENİ / 92 / ÇOK YÜKSEK" dairesel göstergesi — SVG, glow'lu, dolum animasyonlu.
 */
export function ConfidenceRing({ value, label, size = 132 }: Props) {
  const color = toneVar(value);
  const offset = CIRC * (1 - Math.min(100, Math.max(0, value)) / 100);

  return (
    <div className="relative grid place-items-center" style={{ width: size, height: size }}>
      <svg viewBox="0 0 120 120" width={size} height={size} className="-rotate-90">
        <circle cx="60" cy="60" r={R} fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth="8" />
        <motion.circle
          cx="60"
          cy="60"
          r={R}
          fill="none"
          stroke={color}
          strokeWidth="8"
          strokeLinecap="round"
          strokeDasharray={CIRC}
          className="fx-ring-glow-green"
          initial={{ strokeDashoffset: CIRC }}
          animate={{ strokeDashoffset: offset }}
          transition={{ duration: 0.9, ease: "easeOut", delay: 0.15 }}
        />
      </svg>

      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-[8px] font-bold uppercase tracking-[0.12em] text-text-secondary">
          AI Güveni
        </span>
        <span className="text-[34px] font-black leading-none tabular-nums text-text-primary">
          {value}
        </span>
        <span className="text-[8px] font-bold uppercase tracking-[0.1em] text-neon">{label}</span>
      </div>
    </div>
  );
}
