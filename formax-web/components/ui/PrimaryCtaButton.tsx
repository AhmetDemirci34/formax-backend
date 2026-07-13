"use client";

import { motion } from "framer-motion";
import { ArrowRightIcon } from "@/components/discover/icons";

interface Props {
  label: string;
  onClick?: () => void;
  className?: string;
  /** primary = dolu neon gradient; ghost = koyu/yeşil çerçeveli (referans: KOMBİNİ İNCELE). */
  variant?: "primary" | "ghost";
}

/**
 * FORMAX · PrimaryCtaButton (05, shared)
 * PNG referansı: tam-genişlik buton (etiket ortada) + sağda dairesel ok. Glow + press.
 * `primary` = MAÇI KEŞFET (dolu neon), `ghost` = KOMBİNİ İNCELE (koyu/yeşil çerçeve).
 */
export function PrimaryCtaButton({ label, onClick, className = "", variant = "primary" }: Props) {
  const isGhost = variant === "ghost";
  const shell = isGhost
    ? "border border-neon/30 bg-neon/[0.06] fx-glow-soft-green"
    : "fx-grad-cta fx-glow-green";
  const labelColor = isGhost ? "text-neon" : "text-bg-deep";
  const arrowShell = isGhost ? "bg-neon/15 text-neon" : "bg-bg-deep/85 text-neon";

  return (
    <motion.button
      type="button"
      onClick={onClick}
      whileTap={{ scale: 0.98 }}
      className={`relative flex w-full items-center justify-center rounded-[16px] px-5 py-4 ${shell} ${className}`}
    >
      <span className={`text-[15px] font-extrabold uppercase tracking-wide ${labelColor}`}>
        {label}
      </span>
      <span
        className={`absolute right-3 top-1/2 grid h-7 w-7 -translate-y-1/2 place-items-center rounded-full ${arrowShell}`}
      >
        <ArrowRightIcon size={16} />
      </span>
    </motion.button>
  );
}
