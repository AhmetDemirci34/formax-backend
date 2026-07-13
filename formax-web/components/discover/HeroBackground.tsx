import Image from "next/image";

// Hero arka planı — GERÇEK stadyum fotoğrafı (production asset). Net görünür kalmalı:
// sadece metin okunabilirliği için üst/alt hafif gradient + ince kenar vignette. Karartma yok.
export function HeroBackground() {
  return (
    <div aria-hidden className="absolute inset-0 overflow-hidden">
      <div className="absolute inset-0 bg-[#05070D]" />

      <Image
        src="/images/hero/stadium-bg.webp"
        alt=""
        fill
        priority
        sizes="100vw"
        className="object-cover scale-[0.95] object-top"
      />

      {/* Üstte hafif mor atmosfer */}
      <div className="absolute inset-x-0 -top-20 h-72 bg-[radial-gradient(ellipse_at_top,rgba(126,87,255,0.28),transparent_72%)]" />

      {/* Okunabilirlik — yalnız üst ve alt; görseli karartmaz */}
      <div className="absolute inset-x-0 top-0 h-28 bg-[linear-gradient(180deg,rgba(5,7,13,0.45),transparent)]" />
      <div className="absolute inset-x-0 bottom-0 h-32 bg-[linear-gradient(0deg,rgba(5,7,13,0.42),transparent)]" />

      {/* İnce kenar vignette (boğmadan derinlik) */}
      <div className="absolute inset-0 shadow-[inset_0_0_60px_rgba(0,0,0,0.25)]" />

      {/* Zemindeki mor sis */}
      <div
      className="
        absolute
        bottom-0
        left-0
        right-0
        h-28
        bg-[radial-gradient(circle_at_center,rgba(176,108,255,0.30),transparent_72%)]
        blur-2xl
        pointer-events-none
      "
    />
    </div>
  );
}
