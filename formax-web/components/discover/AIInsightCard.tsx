import type { RecommendationCardDto } from "@/types/api";
import { aiIntelligence, AI_DISCLAIMER } from "./cardSignals";
import { SparklesIcon } from "./icons";

// FORMAX AI — kullanıcıyı değil, maçı/dünyayı anlatan Intelligence sentezi.
// 2–3 satır, çok-kaynak hissi (gündem · tempo · öne çıkan başlık), her satır gerçek alandan.
// Kesin tahmin değil — alt notla ayrım korunur. Veri yoksa null → kart gizlenir.
export function AIInsightCard({ card }: { card: RecommendationCardDto }) {
  const lines = aiIntelligence(card);
  if (lines.length === 0) return null;

  return (
    <section className="relative overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      {/* İnce aksan kenarı — AI'ın imzası */}
      <span className="absolute inset-y-0 left-0 w-0.5 bg-accent/70" aria-hidden />

      <div className="mb-2.5 flex items-center gap-2">
        <SparklesIcon size={15} className="text-accent" />
        <span className="text-[11px] font-bold uppercase tracking-[0.18em] text-accent">
          FORMAX AI
        </span>
      </div>

      <div className="space-y-1.5">
        {lines.map((line, i) => (
          <p key={i} className="text-[14px] font-medium leading-relaxed text-white/90">
            {line}
          </p>
        ))}
      </div>

      <p className="mt-3 border-t border-white/[0.06] pt-2.5 text-[10px] font-medium text-text-muted">
        {AI_DISCLAIMER}
      </p>
    </section>
  );
}
