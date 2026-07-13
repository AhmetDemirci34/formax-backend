interface Props {
  league: string;
}

/**
 * FORMAX · LeagueBadge (05)
 * PNG orta-üst lig etiketi. Gerçek lig logosu yok → nötr placeholder işaret + ad.
 */
export function LeagueBadge({ league }: Props) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span
        aria-hidden
        className="grid h-4 w-4 place-items-center rounded-full bg-white/90 text-[8px] font-black text-bg-deep"
      >
        {league.slice(0, 1)}
      </span>
      <span className="text-[11px] font-semibold text-text-secondary">{league}</span>
    </span>
  );
}
