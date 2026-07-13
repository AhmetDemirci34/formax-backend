import { CalendarIcon } from "@/components/discover/icons";

interface Props {
  label: string;
  className?: string;
}

/**
 * FORMAX · MatchTime (05, shared)
 * Takvim ikonu + zaman metni ("Bugün 20:30"). Hero, Combo, Featured kullanır.
 */
export function MatchTime({ label, className = "" }: Props) {
  return (
    <span className={`inline-flex items-center gap-1.5 text-[13px] font-medium text-text-secondary ${className}`}>
      <CalendarIcon size={14} />
      {label}
    </span>
  );
}
