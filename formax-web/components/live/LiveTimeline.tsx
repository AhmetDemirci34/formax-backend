import type { LiveEventDto } from "@/types/api";

const EVENT_ICON: Record<string, string> = {
  Goal:        "⚽",
  YellowCard:  "🟨",
  RedCard:     "🟥",
  Substitution:"🔄",
  PenaltyMiss: "❌",
  PenaltyGoal: "⚽🎯",
  OwnGoal:     "⚽🔁",
  Offside:     "🚩",
  FreeKick:    "🎯",
  Corner:      "📐",
  VarReview:   "📺",
};

interface Props {
  events: LiveEventDto[];
  /** Override heading — use "Maç Olayları" for finished matches */
  title?: string;
}

export function LiveTimeline({ events, title = "Canlı Olaylar" }: Props) {
  if (!events?.length) {
    return (
      <div className="bg-bg-card rounded-xl border border-border p-4">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2">
          {title}
        </h3>
        <p className="text-sm text-text-muted">Henüz olay yok.</p>
      </div>
    );
  }

  const sorted = [...events].sort((a, b) => b.minute - a.minute);

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        {title}
      </h3>
      <div className="space-y-2">
        {sorted.map((ev, i) => (
          <div key={i} className="flex items-start gap-3">
            {/* Minute */}
            <span className="text-xs font-bold text-text-muted w-7 shrink-0 pt-0.5">
              {ev.minute}&apos;
            </span>
            {/* Icon */}
            <span className="text-base shrink-0">
              {EVENT_ICON[ev.eventType] ?? "•"}
            </span>
            {/* Content */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-1.5">
                <span className="text-sm font-medium text-text-primary">
                  {ev.playerName || ev.eventType}
                </span>
                {ev.teamName && (
                  <span className="text-xs text-text-muted">({ev.teamName})</span>
                )}
              </div>
              {ev.detail && (
                <p className="text-xs text-text-secondary mt-0.5">{ev.detail}</p>
              )}
            </div>
            {/* Impact */}
            {ev.impactScore > 0 && (
              <div className="shrink-0">
                <div
                  className="w-1 rounded-full bg-accent opacity-70"
                  style={{ height: `${Math.max(8, ev.impactScore / 10)}px` }}
                />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
