// Radar seviye göstergesi — hedef-ring ikon + "RADAR" + seviye (YÜKSEK/ORTA/DÜŞÜK).
// level prop'u çağırandan gelir (card.radarLevel ?? radarScore/score'tan türetilmiş).

type Level = "YÜKSEK" | "ORTA" | "DÜŞÜK";

// Tek aksan — seviye renkle değil, metinle ayrışır (premium, dağınık renk yok).
const TONE: Record<Level, string> = {
  YÜKSEK: "#4f6ef7",
  ORTA: "#4f6ef7",
  DÜŞÜK: "#4f6ef7",
};

interface Props {
  level: Level;
  variant?: "full" | "mini";
}

export function RadarBadge({ level, variant = "full" }: Props) {
  const color = TONE[level] ?? TONE.DÜŞÜK;

  if (variant === "mini") {
    // Peek kartlar için: üstte "RADAR", altında seviye
    return (
      <div className="flex flex-col items-end leading-tight">
        <span className="text-[10px] font-semibold tracking-wider text-text-muted">RADAR</span>
        <span className="text-xs font-bold" style={{ color }}>{level}</span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <RadarRing color={color} size={34} />
      <div className="flex flex-col leading-tight">
        <span className="text-sm font-semibold tracking-wide text-white">RADAR</span>
        <span className="text-sm font-bold" style={{ color }}>{level}</span>
      </div>
    </div>
  );
}

function RadarRing({ color, size }: { color: string; size: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none">
      <circle cx="12" cy="12" r="10" stroke={color} strokeWidth="1.4" opacity="0.35" />
      <circle cx="12" cy="12" r="6" stroke={color} strokeWidth="1.6" opacity="0.7" />
      <circle cx="12" cy="12" r="2.4" fill={color} />
    </svg>
  );
}
