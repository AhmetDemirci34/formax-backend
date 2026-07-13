/**
 * FORMAX · StadiumBackground (05) — sinematik atmosfer yığını.
 *
 * Katman sırası (arkadan öne): Stadium Image → Dark Overlay → Team Color Lighting
 * → Fog → Light Beams → Ambient Glow → Player Spotlights.
 * Atmosfer gradient taklidi DEĞİL: temel katman gerçek stadyum fotoğrafı; üstteki
 * ışık/sis/huzme katmanları `screen` blend ile eklenerek gerçek ışık gibi davranır.
 * Takım tonları: sol mavi (home) / sağ kırmızı (away).
 */
export function StadiumBackground() {
  return (
    <div aria-hidden className="absolute inset-0 overflow-hidden bg-black">
      {/* 1 · Stadium Image — sinematik temel */}
      <div
        className="absolute inset-0 bg-cover bg-[center_top] bg-no-repeat"
        style={{ backgroundImage: "url(/images/hero/stadium-bg.webp)", opacity: 0.62 }}
      />

      {/* 2 · Dark Overlay — mood + okunabilirlik + köşe vignette (üst yumuşak) */}
      <div className="absolute inset-0 bg-black/38" />
      <div
        className="absolute inset-0"
        style={{
          background:
            "radial-gradient(140% 108% at 50% 18%, transparent 50%, rgba(0,0,0,.70) 100%)",
        }}
      />
      {/* Alt okunabilirlik zemini (UI için) */}
      <div
        className="absolute inset-x-0 bottom-0 h-1/2"
        style={{ background: "linear-gradient(to top, rgba(7,7,12,.72), transparent 92%)" }}
      />

      {/* 3 · Team Color Lighting — sol mavi / sağ kırmızı yanal yıkama */}
      <div
        className="absolute inset-0 mix-blend-screen"
        style={{ background: "radial-gradient(78% 92% at -6% 58%, rgba(77,166,255,.40), transparent 55%)" }}
      />
      <div
        className="absolute inset-0 mix-blend-screen"
        style={{ background: "radial-gradient(78% 92% at 106% 58%, rgba(245,69,77,.38), transparent 55%)" }}
      />

      {/* 4 · Fog — alttan yükselen sinematik sis */}
      <div
        className="absolute inset-x-0 bottom-0 h-1/2 mix-blend-screen blur-2xl"
        style={{ background: "radial-gradient(120% 82% at 50% 122%, rgba(180,205,255,.15), transparent 70%)" }}
      />
      <div
        className="absolute inset-x-0 bottom-[6%] h-1/3 mix-blend-screen blur-3xl"
        style={{ background: "linear-gradient(to top, rgba(255,255,255,.07), transparent 80%)" }}
      />
      {/* Sahne çizgisi sisi — yatay yumuşak bant */}
      <div
        className="absolute inset-x-0 top-[58%] h-24 mix-blend-screen blur-2xl"
        style={{ background: "radial-gradient(80% 100% at 50% 50%, rgba(190,210,255,.12), transparent 72%)" }}
      />

      {/* 5 · Light Beams — üstten çapraz projektör huzmeleri + köşe projektör parlaması */}
      <div
        className="absolute -top-1/4 left-[6%] h-[155%] w-[34%] mix-blend-screen blur-2xl"
        style={{
          transform: "rotate(15deg)",
          background: "linear-gradient(to bottom, rgba(205,222,255,.24), transparent 60%)",
        }}
      />
      <div
        className="absolute -top-1/4 right-[6%] h-[155%] w-[34%] mix-blend-screen blur-2xl"
        style={{
          transform: "rotate(-15deg)",
          background: "linear-gradient(to bottom, rgba(255,208,208,.22), transparent 60%)",
        }}
      />
      {/* Köşe projektör parlaması (gerçek floodlight vurgusu) */}
      <div
        className="absolute -left-[6%] -top-[6%] h-40 w-40 mix-blend-screen blur-2xl"
        style={{ background: "radial-gradient(circle, rgba(210,224,255,.30), transparent 66%)" }}
      />
      <div
        className="absolute -right-[6%] -top-[6%] h-40 w-40 mix-blend-screen blur-2xl"
        style={{ background: "radial-gradient(circle, rgba(255,214,214,.26), transparent 66%)" }}
      />

      {/* 6 · Ambient Glow — imza yeşil taban parıltısı */}
      <div
        className="absolute inset-x-0 bottom-0 h-2/5 mix-blend-screen"
        style={{ background: "radial-gradient(70% 60% at 50% 126%, rgba(46,230,110,.24), transparent 62%)" }}
      />

      {/* 7 · Player Spotlights — oyuncuların bastığı zemin ışık havuzları */}
      <div
        className="absolute bottom-0 left-0 h-[58%] w-[56%] mix-blend-screen blur-xl"
        style={{ background: "radial-gradient(58% 55% at 32% 100%, rgba(77,166,255,.34), transparent 60%)" }}
      />
      <div
        className="absolute bottom-0 right-0 h-[58%] w-[56%] mix-blend-screen blur-xl"
        style={{ background: "radial-gradient(58% 55% at 68% 100%, rgba(245,69,77,.32), transparent 60%)" }}
      />

      {/* Sahne ışık hatları — ince detay */}
      <div className="absolute inset-x-0 top-6 h-px bg-white/5" />
      <div className="absolute inset-x-0 top-10 h-px bg-white/[0.03]" />
    </div>
  );
}
