import Link from "next/link";
import type { MatchDetailDto, RecommendationCardDto } from "@/types/api";
import { ChevronRightIcon } from "./icons";
import { QuickSignalCard } from "./QuickSignalCard";
import { quickSignalsRich } from "./detailFacts";

// "Hızlı Sinyaller" — gerçek metrik + Intelligence katman etiketi (uydurma yüzde yok).
// Model tahminleri (2.5 Üst / KG) aktif maçın gerçek Match Detail'inden gelir.
export function QuickSignals({ card, detail }: { card: RecommendationCardDto; detail?: MatchDetailDto }) {
  const signals = quickSignalsRich(card, detail);
  if (signals.length === 0) return null;

  return (
    <section>
      <div className="mb-2 flex items-center justify-between px-0.5">
        <h2 className="text-[11px] font-bold uppercase tracking-[0.15em] text-text-secondary">
          Hızlı Sinyaller
        </h2>
        <Link
          href={`/signals/${card.matchId}`}
          className="inline-flex items-center gap-0.5 text-[11px] font-medium text-text-muted transition-colors hover:text-[#A855F7]"
        >
          Tüm sinyaller
          <ChevronRightIcon size={13} />
        </Link>
      </div>
      <div className="-mx-5 flex gap-2.5 overflow-x-auto px-5 pb-1 [&::-webkit-scrollbar]:hidden [scrollbar-width:none]">
        {signals.map((s) => (
          <QuickSignalCard key={s.key} signal={s} />
        ))}
      </div>
    </section>
  );
}
