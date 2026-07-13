import type { RecommendationCardDto } from "@/types/api";

// Lig · Saat · Stadyum — tek satır. Yalnız backend'de GERÇEKTEN olan alanlar gösterilir.
// Hiçbiri yoksa null döner (boş satır basılmaz). Veri uydurulmaz.
function formatKickoff(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (isNaN(d.getTime())) return null;
  return d.toLocaleString("tr-TR", {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function MatchMeta({ card }: { card: RecommendationCardDto }) {
  const league = card.leagueName?.trim() || null;
  const time = formatKickoff(card.kickoffTime ?? card.matchDate);

  const parts = [league, time].filter(Boolean) as string[];
  if (parts.length === 0) return null;

  return (
    <div className="flex items-center justify-center gap-2 text-xs font-medium text-white/55">
      {parts.map((p, i) => (
        <span key={i} className="flex items-center gap-2">
          {i > 0 && <span className="text-white/20">·</span>}
          {p}
        </span>
      ))}
    </div>
  );
}
