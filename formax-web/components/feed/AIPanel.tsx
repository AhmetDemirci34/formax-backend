// FORMAX AI — kompakt yorum bloğu. İçerik gerçek backend alanlarından.
// aiHeadline/aiSummary yoksa storyHeadline/storyBody'ye düşer; ikisi de yoksa panel gizlenir.
import type { RecommendationCardDto } from "@/types/api";

interface Props {
  card: RecommendationCardDto;
}

export function AIPanel({ card }: Props) {
  const headline = (card.aiHeadline ?? card.storyHeadline)?.trim();
  const summary = (card.aiSummary ?? card.storyBody)?.trim();

  if (!headline && !summary) return null;

  return (
    <div>
      <div className="flex items-center gap-1.5 mb-1.5">
        <SparkleIcon />
        <span className="text-[11px] font-bold tracking-wider text-[#A855F7]">FORMAX AI</span>
      </div>

      {headline && (
        <p className="text-[15px] font-semibold text-white leading-snug line-clamp-1">
          {headline}
        </p>
      )}
      {summary && (
        <p className="text-[13px] text-text-secondary leading-relaxed mt-1 line-clamp-2">
          {summary}
        </p>
      )}
    </div>
  );
}

function SparkleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="#A855F7">
      <path d="M12 2l1.6 5.1L19 8.7l-4.3 3.1L16 17l-4-3-4 3 1.3-5.2L5 8.7l5.4-1.6z" />
    </svg>
  );
}
