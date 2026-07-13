import { StarIcon } from "@/components/discover/icons";

interface Props {
  label: string;
}

/**
 * FORMAX · AIBadge (05)
 * PNG üst-orta pill: "AI'NIN BUGÜN İÇİN 1 NUMARALI MAÇI" — yıldız + neon cam rozet.
 */
export function AIBadge({ label }: Props) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full border border-neon/25 bg-bg-deep/70 px-2.5 py-1.5 backdrop-blur-md">
      <StarIcon size={11} className="shrink-0 text-neon" />
      <span className="text-[9px] font-bold uppercase leading-none tracking-[0.02em] text-text-primary">
        {label}
      </span>
    </span>
  );
}
