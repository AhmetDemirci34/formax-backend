interface SparklineProps {
  data: number[];
  /** Ton sınıfı (text-neon vb.) — stroke currentColor'dan gelir. */
  className?: string;
  height?: number;
}

/**
 * FORMAX · Sparkline (08, shared)
 * Küçük SVG trend çizgisi. Renk parent'tan (currentColor). Design Token; hardcoded yok.
 * Market/Combo/Expectations ortak kullanır.
 */
export function Sparkline({ data, className = "", height = 22 }: SparklineProps) {
  const w = 100;
  const h = 32;
  const pts = toPoints(data, w, h);

  return (
    <svg
      viewBox={`0 0 ${w} ${h}`}
      preserveAspectRatio="none"
      width="100%"
      height={height}
      className={className}
      aria-hidden
    >
      <polyline
        points={pts}
        fill="none"
        stroke="currentColor"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

function toPoints(data: number[], w: number, h: number): string {
  if (data.length < 2) return `0,${h} ${w},${h}`;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const span = max - min || 1;
  const pad = 3;
  return data
    .map((v, i) => {
      const x = (i / (data.length - 1)) * w;
      const y = h - pad - ((v - min) / span) * (h - pad * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
}
