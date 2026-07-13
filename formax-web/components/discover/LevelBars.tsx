import type { Level } from "./cardSignals";

// 3 segmentli seviye göstergesi — tek aksan. Yüksek sesli "DÜŞÜK" kelimesi yerine sakin görsel.
const HEIGHTS = ["h-1.5", "h-2.5", "h-3.5"];

export function LevelBars({ level }: { level: Level }) {
  return (
    <span className="flex items-end gap-0.5" aria-hidden>
      {[1, 2, 3].map((i) => (
        <span
          key={i}
          className={`w-1 rounded-sm ${HEIGHTS[i - 1]} ${
            i <= level ? "bg-accent" : "bg-white/15"
          }`}
        />
      ))}
    </span>
  );
}
