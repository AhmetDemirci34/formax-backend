import type { RecommendationCardDto } from "@/types/api";
import { StarIcon, TargetIcon } from "./icons";
import { radarLevel, userBadge } from "./cardSignals";

const RADAR_COLOR: Record<string, string> = {
  YÜKSEK: "#F5A623",
  ORTA: "#A855F7",
  DÜŞÜK: "#3B82F6",
};

// Hero üstü rozet satırı — sol: yeşil "SENİN İÇİN", sağ: "RADAR / seviye" (renk seviyeye göre).
export function DiscoverBadgeRow({ card }: { card: RecommendationCardDto }) {
  const badge = userBadge(card);
  const level = radarLevel(card);
  const radarColor = RADAR_COLOR[level] ?? "#3B82F6";

  return (
    <div className="flex items-center justify-between">
      {badge ? (
        <span className="inline-flex items-center gap-2 rounded-full border border-formax-green/60 bg-formax-green/10 px-4 py-2">
          <StarIcon size={15} className="text-formax-green" />
          <span className="text-xs font-bold uppercase tracking-wide text-formax-green">
            {badge.label}
          </span>
        </span>
      ) : (
        <span />
      )}

      <span className="inline-flex items-center gap-2">
        <TargetIcon size={22} style={{ color: radarColor }} />
        <span className="flex flex-col leading-tight">
          <span className="text-[13px] font-semibold tracking-wide text-white">RADAR</span>
          <span className="text-[13px] font-bold" style={{ color: radarColor }}>
            {level}
          </span>
        </span>
      </span>
    </div>
  );
}
