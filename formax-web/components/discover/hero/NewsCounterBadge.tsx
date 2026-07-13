import { TrendingUpIcon } from "@/components/discover/icons";

interface Props {
  count: number;
}

/**
 * FORMAX · NewsCounterBadge (05)
 * PNG sağ-üst: "624 haber analiz edildi" — küçük spark ikonu + sayı.
 */
export function NewsCounterBadge({ count }: Props) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-bg-deep/60 px-2 py-1 backdrop-blur-md">
      <TrendingUpIcon size={11} className="text-neon" />
      <span className="whitespace-nowrap text-[9px] font-semibold text-text-secondary">
        <span className="tabular-nums text-text-primary">{count}</span> haber analiz edildi
      </span>
    </span>
  );
}
