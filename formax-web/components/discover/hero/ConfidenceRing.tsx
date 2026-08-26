"use client";

import { useEffect, useState } from "react";
import { animate, motion, useMotionValue, useReducedMotion } from "framer-motion";

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
 * Premium motion: halka çok hafif "nefes alır" (scale 1.00↔1.02, ~2.6s) ve maç değişince
 * sayı önceki değerden yeni değere yumuşak sayar (~420 ms). prefers-reduced-motion saygılı.
 */
export function ConfidenceRing({ value, label, size = 132 }: Props) {
  const color = toneVar(value);
  const offset = CIRC * (1 - Math.min(100, Math.max(0, value)) / 100);
  const reduce = useReducedMotion();

  // Sayı sayacı — maç değişince önceki değerden yeni değere yumuşak yükselir/iner.
  // onUpdate bir callback'tir (effect gövdesinde senkron setState yok).
  const count = useMotionValue(value);
  const [display, setDisplay] = useState(value);
  useEffect(() => {
    if (reduce) return; // reduced-motion: sayaç animasyonu yok, değer doğrudan gösterilir
    const controls = animate(count, value, {
      duration: 0.42,
      ease: "easeOut",
      onUpdate: (v) => setDisplay(Math.round(v)),
    });
    return () => controls.stop();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, reduce]);
  const shown = reduce ? value : display;

  return (
    <motion.div
      className="relative grid place-items-center"
      style={{ width: size, height: size }}
      animate={reduce ? undefined : { scale: [1, 1.02, 1] }}
      transition={reduce ? undefined : { duration: 2.6, ease: "easeInOut", repeat: Infinity }}
    >
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
          {shown}
        </span>
        <span className="text-[8px] font-bold uppercase tracking-[0.1em] text-neon">{label}</span>
      </div>
    </motion.div>
  );
}
