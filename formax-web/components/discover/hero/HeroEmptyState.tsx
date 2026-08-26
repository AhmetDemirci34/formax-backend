/**
 * FORMAX · HeroEmptyState — gösterilecek (upcoming/live) maç yoksa Hero yerine.
 * İllüstrasyon tamamen kendi çizimimiz olan minimal premium SVG'dir (telifsiz):
 * düdük + ses dalgaları + havada el. Hiçbir internet fotoğrafı / telifli görsel kullanılmaz.
 */
function WhistleIllustration() {
  return (
    <svg
      viewBox="0 0 220 170"
      width="180"
      height="139"
      fill="none"
      aria-hidden
      className="text-text-secondary"
    >
      {/* yumuşak halka */}
      <circle cx="100" cy="92" r="60" stroke="currentColor" strokeOpacity="0.08" strokeWidth="1.5" />

      {/* havada el + kol (hakem) */}
      <g stroke="currentColor" strokeWidth="3.2" strokeLinecap="round" strokeLinejoin="round">
        <path d="M124 70 C136 46 145 38 156 32" />
        <path d="M156 32 l-2 -12 M156 32 l8 -9 M156 32 l12 -2 M156 32 l-10 0" />
      </g>

      {/* düdük gövdesi */}
      <g stroke="currentColor" strokeWidth="3.2" strokeLinejoin="round" strokeLinecap="round">
        <path d="M60 84 h36 a18 18 0 1 1 -18 18 H72 a12 12 0 0 1 -12 -12 z" />
        <circle cx="96" cy="102" r="4.5" fill="currentColor" stroke="none" />
        <path d="M56 88 h4 v10 h-4 a4 4 0 0 1 -4 -4 v-2 a4 4 0 0 1 4 -4 z" />
      </g>

      {/* ses dalgaları (neon aksan) */}
      <g stroke="var(--neon)" strokeWidth="3" strokeLinecap="round">
        <path d="M126 96 a16 16 0 0 1 0 22" opacity="0.75" />
        <path d="M137 89 a27 27 0 0 1 0 36" opacity="0.4" />
      </g>
    </svg>
  );
}

export function HeroEmptyState() {
  return (
    <div className="relative overflow-hidden rounded-[var(--radius-section)] border border-white/[0.08] bg-gradient-to-b from-white/[0.04] to-transparent shadow-[var(--shadow-elevated)]">
      {/* ambient neon zemin */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{ background: "radial-gradient(70% 60% at 50% 30%, rgba(46,230,110,.08), transparent 70%)" }}
      />
      <div className="relative flex flex-col items-center gap-4 px-6 py-14 text-center">
        <WhistleIllustration />
        <div className="space-y-1.5">
          <p className="text-[15px] font-bold text-text-primary">Gösterilecek yeni maç yok.</p>
          <p className="mx-auto max-w-[240px] text-[12px] leading-relaxed text-text-muted">
            Birazdan başa dönülüyor…
          </p>
        </div>
      </div>
    </div>
  );
}
