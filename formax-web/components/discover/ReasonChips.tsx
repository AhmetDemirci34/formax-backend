import type { MatchDetailDto, RecommendationCardDto } from "@/types/api";
import { reasonChipList } from "./cardSignals";
import { reasonFacts } from "./detailFacts";
import { ICONS, TrendingUpIcon } from "./icons";

// "Neden bu maç?" — spesifik gerçek faktörler (takım formu, istatistik, ilgi). Yoksa gizlenir.
export function ReasonChips({ card, detail }: { card: RecommendationCardDto; detail?: MatchDetailDto }) {
  const facts = reasonFacts(card, detail);
  const chips = facts.length > 0 ? facts : reasonChipList(card);
  if (chips.length === 0) return null;

  return (
    <section>
      <div className="mb-2 flex items-center gap-1.5 px-0.5">
        <TrendingUpIcon size={14} className="text-[#A855F7]" />
        <h2 className="text-[11px] font-bold uppercase tracking-[0.15em] text-text-secondary">
          Neden bu maç?
        </h2>
      </div>
      <div className="flex gap-2 overflow-x-auto pb-0.5 [&::-webkit-scrollbar]:hidden [scrollbar-width:none]">
        {chips.map((c) => {
          const Icon = ICONS[c.iconKey];
          return (
            <span
              key={c.label}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.04] px-3 py-2"
            >
              <Icon size={13} className="text-[#A855F7]" />
              <span className="text-xs font-medium text-text-secondary">{c.label}</span>
            </span>
          );
        })}
      </div>
    </section>
  );
}
