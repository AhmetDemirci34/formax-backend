import type { ReactNode } from "react";

type Glow = "none" | "green" | "purple" | "amber";

interface GlassCardProps {
  children: ReactNode;
  className?: string;
  /** Renk-eşli dış parıltı (Glow System). */
  glow?: Glow;
  /** Sağ-üstten yayılan dekoratif section parıltısı. */
  sectionGlow?: boolean;
  as?: "div" | "section";
}

const GLOW_CLASS: Record<Glow, string> = {
  none: "",
  green: "fx-glow-green",
  purple: "fx-glow-purple",
  amber: "fx-glow-amber",
};

/**
 * FORMAX · GlassCard (02)
 * Tek cam yüzey reçetesi (.fx-glass) + token radius. Tüm section/kart sarmalayıcısı.
 */
export function GlassCard({
  children,
  className = "",
  glow = "none",
  sectionGlow = false,
  as = "div",
}: GlassCardProps) {
  const Tag = as;
  return (
    <Tag
      className={`fx-glass relative overflow-hidden rounded-[var(--radius-section)] ${GLOW_CLASS[glow]} ${className}`}
    >
      {sectionGlow ? (
        <span className="fx-section-glow pointer-events-none absolute inset-0" aria-hidden />
      ) : null}
      <div className="relative">{children}</div>
    </Tag>
  );
}
