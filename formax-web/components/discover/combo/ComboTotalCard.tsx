import { Sparkline } from "@/components/ui/Sparkline";
import { StarRating } from "@/components/ui/StarRating";

interface ComboTotalCardProps {
  totalOdds: string;
  stars: number;
  trend: number[];
}

/**
 * FORMAX · ComboTotalCard (09, new)
 * Kombin özet kartı (satır sonu, vurgulu): TOPLAM ORAN + büyük oran + AI GÜVEN yıldızları + trend.
 * Neon çerçeve + soft-green glow ile diğer ayaklardan ayrışır.
 */
export function ComboTotalCard({ totalOdds, stars, trend }: ComboTotalCardProps) {
  return (
    <div className="fx-glow-soft-green flex w-[118px] shrink-0 flex-col items-center justify-center gap-1 rounded-2xl border border-neon/25 bg-neon/[0.05] px-2.5 py-2.5 text-center">
      <span className="text-[8.5px] font-bold uppercase tracking-wide text-text-muted">Toplam Oran</span>
      <span className="text-[24px] font-black leading-none tabular-nums text-neon">{totalOdds}</span>
      <span className="mt-0.5 text-[8.5px] font-bold uppercase tracking-wide text-text-muted">AI Güven</span>
      <StarRating value={stars} size={12} />
      <div className="mt-0.5 w-full text-neon">
        <Sparkline data={trend} height={16} />
      </div>
    </div>
  );
}
