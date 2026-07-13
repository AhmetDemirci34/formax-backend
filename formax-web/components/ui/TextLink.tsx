import { ChevronRightIcon } from "@/components/discover/icons";

interface TextLinkProps {
  label: string;
  /** Neon (vurgulu) veya muted (ikincil) ton. */
  tone?: "neon" | "muted";
}

/**
 * FORMAX · TextLink (08, shared)
 * "Detaylı istatistikler ›" / "Tüm sinyalleri gör ›" gibi section aksiyon linki.
 * Design Token; hardcoded renk yok. (Görsel; iş mantığı yok.)
 */
export function TextLink({ label, tone = "muted" }: TextLinkProps) {
  const color = tone === "neon" ? "text-neon" : "text-text-secondary";
  return (
    <button
      type="button"
      className={`inline-flex items-center gap-0.5 whitespace-nowrap text-[11px] font-semibold transition-opacity hover:opacity-80 ${color}`}
    >
      {label}
      <ChevronRightIcon size={13} />
    </button>
  );
}
