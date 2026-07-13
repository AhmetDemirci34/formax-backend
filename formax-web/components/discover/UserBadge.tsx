import type { UserBadgeVM } from "./cardSignals";
import { ICONS } from "./icons";

// Kullanıcı rozeti — "neden senin için". Tek aksan, monokrom SVG (emoji yok).
// VM null ise parent gizler (boş rozet yok).
export function UserBadge({ badge }: { badge: UserBadgeVM }) {
  const Icon = ICONS[badge.iconKey];
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-accent/25 bg-accent/10 px-3 py-1.5">
      <Icon size={14} className="text-accent" />
      <span className="text-xs font-semibold tracking-wide text-white/90">{badge.label}</span>
    </span>
  );
}
