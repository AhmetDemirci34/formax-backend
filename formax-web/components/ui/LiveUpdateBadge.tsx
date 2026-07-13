interface LiveUpdateBadgeProps {
  label: string;
}

/**
 * FORMAX · LiveUpdateBadge (07, shared)
 * "Son güncelleme: 2 dk önce" — nabız atan neon nokta + metin.
 * AI Comment / AI Scenarios / Market Movements ortak kullanır.
 */
export function LiveUpdateBadge({ label }: LiveUpdateBadgeProps) {
  return (
    <span className="inline-flex items-center gap-1.5 whitespace-nowrap text-[10px] font-medium text-text-muted">
      <span className="relative flex h-1.5 w-1.5">
        <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-neon/70" />
        <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-neon" />
      </span>
      {label}
    </span>
  );
}
