"use client";

// FORMAX · Takım arması — TEK ortak component (Maçlar, Takip, Maç Detayı hepsi bunu kullanır).
//
// KAYNAK: yalnız backend'in gönderdiği `logoUrl` (Teams.LogoUrl → MatchListItemDto).
// Statik takım-logo eşlemesi YOK, ada göre internetten arama YOK, uydurma URL YOK.
//
// FALLBACK ZİNCİRİ:
//   1) logoUrl varsa görsel (oran korunur: object-contain, kare kutu)
//   2) görsel yüklenemezse (404/CORS/ağ) → takım kısaltması (monogram)
//   3) logoUrl hiç yoksa → doğrudan monogram

import { useEffect, useState } from "react";

interface Props {
  name: string;
  logoUrl?: string | null;
  size?: number;
}

export function TeamCrest({ name, logoUrl, size = 88 }: Props) {
  const [broken, setBroken] = useState(false);

  // Aynı slot farklı takıma yeniden kullanıldığında hatalı "kırık" durumu taşınmasın.
  useEffect(() => {
    setBroken(false);
  }, [logoUrl]);

  if (logoUrl && !broken) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={logoUrl}
        alt={name}
        width={size}
        height={size}
        loading="lazy"
        onError={() => setBroken(true)}
        className="shrink-0 rounded-full bg-white/95 object-contain shadow-[0_4px_16px_rgba(0,0,0,0.45)]"
        style={{ width: size, height: size }}
      />
    );
  }

  const initials =
    name.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "").slice(0, 3).toUpperCase() || "—";

  return (
    <div
      className="flex shrink-0 items-center justify-center rounded-full bg-white/95 shadow-[0_4px_16px_rgba(0,0,0,0.45)]"
      style={{ width: size, height: size }}
      aria-label={name}
      role="img"
    >
      <span className="font-black text-[#0a0e16]" style={{ fontSize: size * 0.28 }}>
        {initials}
      </span>
    </div>
  );
}
