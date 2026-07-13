import type { InsightDto, ProbabilityItemDto, MarketIntelligenceDto } from "@/types/api";

// ── Form insight ─────────────────────────────────────────────────────────────

export function InsightBlock({ insight }: { insight: InsightDto }) {
  // Don't render an empty hero — value-less cards are removed entirely.
  if (!insight?.headline && !insight?.summary) return null;

  return (
    <div className="bg-bg-card rounded-xl border border-accent/20 p-5 space-y-2">
      {/* Small label — the message is the hero, not the label */}
      <h3 className="text-[10px] font-semibold text-text-muted uppercase tracking-[0.15em]">
        Insight
      </h3>
      {/* Hero message — the single most prominent line on the screen */}
      {insight.headline && (
        <p className="text-lg font-bold text-text-primary leading-snug">
          {insight.headline}
        </p>
      )}
      {insight.summary && (
        <p className="text-sm text-text-secondary leading-relaxed">
          {insight.summary}
        </p>
      )}
    </div>
  );
}

// ── Probabilities ─────────────────────────────────────────────────────────────

const PROB_COLOR = (p: number) =>
  p >= 70 ? "text-formax-green" : p >= 55 ? "text-formax-amber" : "text-text-secondary";

export function ProbabilitiesBlock({ items }: { items: ProbabilityItemDto[] }) {
  if (!items?.length) return null;
  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Olasılık Analizi
      </h3>
      <div className="space-y-3">
        {items.map((item) => (
          <div key={item.market} className="flex items-center justify-between gap-3">
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between mb-1">
                <span className="text-sm font-medium text-text-primary">{item.market}</span>
                <span className={`text-sm font-bold ${PROB_COLOR(item.probability)}`}>
                  %{item.probability}
                </span>
              </div>
              <div className="h-1.5 bg-bg-elevated rounded-full overflow-hidden">
                <div
                  className={`h-full rounded-full transition-all ${
                    item.probability >= 70
                      ? "bg-formax-green"
                      : item.probability >= 55
                      ? "bg-formax-amber"
                      : "bg-text-muted/50"
                  }`}
                  style={{ width: `${item.probability}%` }}
                />
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ── Market intelligence ───────────────────────────────────────────────────────

const TONE_COLORS: Record<string, string> = {
  positive: "border-formax-green/30 bg-formax-green/5",
  negative: "border-formax-red/30 bg-formax-red/5",
  neutral:  "border-border bg-bg-elevated/50",
};

export function MarketIntelligenceBlock({ market }: { market: MarketIntelligenceDto }) {
  // Supporting data — render nothing when empty (no value-less card).
  if (!market?.headline && !market?.detail) return null;

  // Lower visual weight: market supports the story, it does not compete with Insight.
  return (
    <div className={`rounded-xl border p-3.5 ${TONE_COLORS[market.tone] ?? TONE_COLORS.neutral}`}>
      <h3 className="text-[10px] font-semibold text-text-muted uppercase tracking-[0.15em] mb-1.5">
        Piyasa İstihbaratı
      </h3>
      {market.headline && (
        <p className="text-xs font-medium text-text-secondary mb-0.5">{market.headline}</p>
      )}
      {market.detail && (
        <p className="text-xs text-text-muted leading-relaxed">{market.detail}</p>
      )}
    </div>
  );
}
