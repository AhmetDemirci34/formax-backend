// Tek "neden" chip'i — somut faktör. Aksan noktası + sakin pill (tek tasarım dili).
export function ReasonChip({ label }: { label: string }) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-text-secondary">
      <span className="h-1.5 w-1.5 rounded-full bg-accent/80" aria-hidden />
      {label}
    </span>
  );
}
