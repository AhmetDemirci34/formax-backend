import type { ReactNode } from "react";

type Tone = "neon" | "purple" | "amber" | "red" | "blue" | "muted";

interface BadgeProps {
  label: string;
  tone?: Tone;
  /** Sol tarafta nabız atan canlı nokta (live/güncelleme rozetleri). */
  dot?: boolean;
  icon?: ReactNode;
  className?: string;
}

const TONE: Record<Tone, { text: string; bg: string; ring: string; dot: string }> = {
  neon:   { text: "text-neon",          bg: "bg-neon/10",          ring: "ring-neon/25",          dot: "bg-neon" },
  purple: { text: "text-signal-purple", bg: "bg-signal-purple/10", ring: "ring-signal-purple/25", dot: "bg-signal-purple" },
  amber:  { text: "text-signal-amber",  bg: "bg-signal-amber/10",  ring: "ring-signal-amber/25",  dot: "bg-signal-amber" },
  red:    { text: "text-signal-red",    bg: "bg-signal-red/10",    ring: "ring-signal-red/25",    dot: "bg-signal-red" },
  blue:   { text: "text-signal-blue",   bg: "bg-signal-blue/10",   ring: "ring-signal-blue/25",   dot: "bg-signal-blue" },
  muted:  { text: "text-text-secondary", bg: "bg-white/5",         ring: "ring-white/10",         dot: "bg-text-muted" },
};

/**
 * FORMAX · Badge (02)
 * Küçük pill rozet — live/news/shield/featured/rank varyantları `tone` + `dot` ile.
 */
export function Badge({ label, tone = "muted", dot = false, icon, className = "" }: BadgeProps) {
  const t = TONE[tone];
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-wide ring-1 ${t.bg} ${t.text} ${t.ring} ${className}`}
    >
      {dot ? (
        <span className="relative flex h-1.5 w-1.5">
          <span className={`absolute inline-flex h-full w-full animate-ping rounded-full opacity-70 ${t.dot}`} />
          <span className={`relative inline-flex h-1.5 w-1.5 rounded-full ${t.dot}`} />
        </span>
      ) : null}
      {icon ? <span className="shrink-0">{icon}</span> : null}
      {label}
    </span>
  );
}
