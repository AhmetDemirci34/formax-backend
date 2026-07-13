import type { ReactNode } from "react";

type Accent = "neon" | "purple" | "amber" | "red" | "muted";

interface SectionHeaderProps {
  /** Sol ikon (inline SVG). */
  icon?: ReactNode;
  title: string;
  subtitle?: string;
  /** Başlığın hemen yanındaki küçük slot (ör. info ikonu). */
  titleAfter?: ReactNode;
  /** Sağ slot: LiveUpdateBadge | TextLink | InfoLink. */
  right?: ReactNode;
  /** İkon/aksan rengi. */
  accent?: Accent;
}

const ACCENT_TEXT: Record<Accent, string> = {
  neon: "text-neon",
  purple: "text-signal-purple",
  amber: "text-signal-amber",
  red: "text-signal-red",
  muted: "text-text-secondary",
};

/**
 * FORMAX · SectionHeader (02)
 * 8 section'ın ortak başlığı: ikon + başlık + alt başlık + sağ aksiyon slotu.
 */
export function SectionHeader({
  icon,
  title,
  subtitle,
  titleAfter,
  right,
  accent = "neon",
}: SectionHeaderProps) {
  return (
    <div className="mb-3 flex items-start justify-between gap-3">
      <div className="flex min-w-0 items-start gap-2.5">
        {icon ? <span className={`mt-0.5 shrink-0 ${ACCENT_TEXT[accent]}`}>{icon}</span> : null}
        <div className="min-w-0">
          <div className="flex items-center gap-1.5">
            <h2 className="text-[15px] font-bold uppercase leading-tight tracking-[0.04em] text-text-primary">
              {title}
            </h2>
            {titleAfter ? <span className="shrink-0 leading-none">{titleAfter}</span> : null}
          </div>
          {subtitle ? (
            <p className="mt-0.5 truncate text-[11px] font-medium text-text-muted">{subtitle}</p>
          ) : null}
        </div>
      </div>
      {right ? <div className="shrink-0">{right}</div> : null}
    </div>
  );
}
