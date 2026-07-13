import { StarIcon } from "@/components/discover/icons";

interface StarRatingProps {
  /** Dolu yıldız sayısı. */
  value: number;
  max?: number;
  size?: number;
}

/**
 * FORMAX · StarRating (09, shared)
 * Dolu (neon) + boş (soluk) yıldız dizisi. AI Combo güven göstergesi kullanır.
 * StarIcon fill override ile dolu; Design Token, hardcoded renk yok.
 */
export function StarRating({ value, max = 5, size = 13 }: StarRatingProps) {
  return (
    <div className="flex items-center gap-0.5" role="img" aria-label={`${value}/${max}`}>
      {Array.from({ length: max }).map((_, i) => (
        <StarIcon
          key={i}
          size={size}
          fill="currentColor"
          className={i < value ? "text-neon" : "text-white/15"}
        />
      ))}
    </div>
  );
}
