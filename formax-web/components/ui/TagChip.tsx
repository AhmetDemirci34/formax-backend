interface Props {
  label: string;
  variant?: "default" | "accent" | "green" | "amber" | "red";
}

const VARIANT_CLASSES: Record<NonNullable<Props["variant"]>, string> = {
  default: "bg-bg-elevated text-text-secondary border border-border",
  accent:  "bg-accent/10 text-accent border border-accent/20",
  green:   "bg-formax-green/10 text-formax-green border border-formax-green/20",
  amber:   "bg-formax-amber/10 text-formax-amber border border-formax-amber/20",
  red:     "bg-formax-red/10 text-formax-red border border-formax-red/20",
};

export function TagChip({ label, variant = "default" }: Props) {
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${VARIANT_CLASSES[variant]}`}
    >
      {label}
    </span>
  );
}
