"use client";

// FORMAX · Takım arması — TEK ortak component (Maçlar, Takip, Maç Detayı hepsi bunu kullanır).
//
// KAYNAK: yalnız backend'in gönderdiği `logoUrl` (Teams.LogoUrl → MatchListItemDto).
// Statik takım-logo eşlemesi YOK, ada göre internetten arama YOK, uydurma URL YOK.
//
// BOŞ BEYAZ DAİRE YOK (11.09.2026, ölçüldü: maç özeti ekranında Fenerbahçe ve Lyon
// armaları yüklenene dek bembeyaz boş daire görünüyordu). Artık:
//   1) Monogram (takım kısaltması) HER ZAMAN altta çizilir — tasarımın koyu yüzey rengiyle.
//   2) Görsel YALNIZ gerçekten yüklendiğinde üste belirir (opacity), beyaz zemini de o an gelir.
//   3) Görsel yüklenemezse (404/CORS/ağ) monogram kalır; logoUrl hiç yoksa yalnız monogram.

import { useEffect, useRef, useState } from "react";

interface Props {
  name: string;
  logoUrl?: string | null;
  size?: number;
}

type CrestState = "loading" | "loaded" | "broken";

/** Takım kısaltması — Türkçe harfler korunur, en fazla 3 harf. */
export function crestInitials(name: string): string {
  return name.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "").slice(0, 3).toLocaleUpperCase("tr-TR") || "—";
}

export function TeamCrest({ name, logoUrl, size = 88 }: Props) {
  const [state, setState] = useState<CrestState>(logoUrl ? "loading" : "broken");
  const imgRef = useRef<HTMLImageElement | null>(null);

  // Aynı slot farklı takıma yeniden kullanıldığında eski durum taşınmasın. Görsel,
  // hidrasyondan ÖNCE yüklenmişse onLoad kaçmış olabilir: tamamlanmışlığı doğrudan oku.
  useEffect(() => {
    if (!logoUrl) {
      setState("broken");
      return;
    }
    const img = imgRef.current;
    if (img && img.complete) setState(img.naturalWidth > 0 ? "loaded" : "broken");
    else setState("loading");
  }, [logoUrl]);

  const loaded = state === "loaded";

  return (
    <div
      className={`relative shrink-0 overflow-hidden rounded-full shadow-[0_4px_16px_rgba(0,0,0,0.45)] ${
        loaded ? "bg-white/95" : "border border-white/15 bg-goalai-surface-bright"
      }`}
      style={{ width: size, height: size }}
      role="img"
      aria-label={name}
      data-crest={state}
    >
      {/* Monogram — yüklenirken ve hata durumunda görünen GERÇEK yedek. */}
      {!loaded && (
        <span
          className="absolute inset-0 flex items-center justify-center font-black text-white/90"
          style={{ fontSize: Math.max(8, size * 0.3) }}
          aria-hidden
        >
          {crestInitials(name)}
        </span>
      )}

      {logoUrl && state !== "broken" && (
        // eslint-disable-next-line @next/next/no-img-element
        <img
          ref={imgRef}
          src={logoUrl}
          alt=""
          width={size}
          height={size}
          onLoad={() => setState("loaded")}
          onError={() => setState("broken")}
          className="absolute inset-0 h-full w-full object-contain transition-opacity duration-200"
          style={{ opacity: loaded ? 1 : 0 }}
        />
      )}
    </div>
  );
}
