/**
 * FORMAX · RadarVisual (01, new)
 * Radar bloğunun görsel imzası: eş-merkezli neon halkalar + dönen tarama huzmesi
 * + merkezde aktif sinyal sayısı. Token tabanlı (var(--neon)), hardcoded renk yok.
 */
export function RadarVisual({ count }: { count: number }) {
  return (
    <div className="fx-glow-soft-green relative h-[92px] w-[92px] shrink-0 rounded-full">
      {/* eş-merkezli halkalar */}
      <div className="absolute inset-0 rounded-full border border-neon/25" />
      <div className="absolute inset-[16%] rounded-full border border-neon/20" />
      <div className="absolute inset-[34%] rounded-full border border-neon/15" />
      {/* nişangah çizgileri */}
      <div className="absolute left-1/2 top-0 h-full w-px -translate-x-1/2 bg-neon/10" />
      <div className="absolute left-0 top-1/2 h-px w-full -translate-y-1/2 bg-neon/10" />
      {/* dönen tarama huzmesi */}
      <div
        className="absolute inset-0 animate-spin rounded-full"
        style={{
          animationDuration: "4s",
          background:
            "conic-gradient(from 0deg, transparent 0deg, color-mix(in srgb, var(--neon) 26%, transparent) 70deg, transparent 96deg)",
        }}
        aria-hidden
      />
      {/* merkez sayaç */}
      <div className="absolute inset-0 grid place-items-center">
        <div className="flex flex-col items-center leading-none">
          <span className="text-[22px] font-extrabold tabular-nums text-text-primary">{count}</span>
          <span className="mt-1 text-[8px] font-bold uppercase tracking-[0.14em] text-neon">
            Sinyal
          </span>
        </div>
      </div>
    </div>
  );
}
