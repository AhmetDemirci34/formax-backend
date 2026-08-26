"use client";

import { useEffect, useState } from "react";
import { renderSelectionCard } from "@/lib/share/renderSelectionCard";

/**
 * FORMAX · Bugünün Seçkisi — paylaşım paneli (bottom sheet).
 *
 * ÖNİZLEME GERÇEK VERİDİR: satırlar ekrandaki seçki ayaklarından birebir gelir
 * (tarih/saat, takımlar, market, olasılık, oran). Bu bileşen hiçbir değer HESAPLAMAZ,
 * ÜRETMEZ, biçim dışında dönüştürmez; toplam oran dışarıdan hazır gelir
 * (mevcut `comboTotalOdd` ürün mantığı — ayak oranlarının çarpımı).
 *
 * Paylaşım hedefleri: Web Share API (varsa) + platformların KENDİ genel paylaşım
 * bağlantıları + panoya kopyala. Sahte/entegrasyonu olmayan hedef eklenmez.
 */
export interface ShareLine {
  matchId: number;
  /** Gerçek tarih/saat metni (feed'den). */
  time: string;
  home: string;
  away: string;
  market: string | null;
  probability: number | null;
  odd: number | null;
}

export function ShareSheet({
  open,
  onClose,
  lines,
  totalOdd,
}: {
  open: boolean;
  onClose: () => void;
  lines: ShareLine[];
  /** Mevcut ürün mantığından gelen toplam oran; ayaklardan biri oransızsa null. */
  totalOdd: number | null;
}) {
  const [copied, setCopied] = useState(false);
  const [canNativeShare, setCanNativeShare] = useState(false);
  const [busy, setBusy] = useState(false);
  const [imgNote, setImgNote] = useState<string | null>(null);

  // navigator.share yalnız tarayıcıda ve güvenli bağlamda vardır → render sonrası ölç.
  useEffect(() => {
    setCanNativeShare(typeof navigator !== "undefined" && typeof navigator.share === "function");
  }, []);

  useEffect(() => {
    if (!open) setCopied(false);
  }, [open]);

  if (!open) return null;

  // Paylaşılacak METİN de aynı gerçek satırlardan kurulur (ikinci bir kaynak yok).
  const text = [
    "FORMAX · Bugünün Seçkisi",
    "",
    ...lines.map((l) =>
      [
        l.time,
        `${l.home} — ${l.away}`,
        [l.market, l.probability != null ? `%${l.probability}` : null, l.odd != null ? l.odd.toFixed(2) : null]
          .filter(Boolean)
          .join(" · "),
      ]
        .filter(Boolean)
        .join("\n")
    ),
    "",
    `${lines.length} maç`,
    totalOdd != null ? `Toplam oran ${totalOdd.toFixed(2)}` : "",
  ]
    .filter((s) => s !== "")
    .join("\n");

  const url = typeof window !== "undefined" ? window.location.href : "";
  const enc = encodeURIComponent;

  // Bu hedeflerin HEPSİ metin/URL taşır — hiçbiri web'den dosya (görsel) kabul etmez.
  // Instagram'ın web paylaşım ucu YOKTUR, o yüzden listede de yoktur (sahte buton eklenmez).
  // Görsel paylaşmak için "Görsel Olarak Paylaş" (native share files / indirme) kullanılır.
  const targets: { label: string; href: string }[] = [
    { label: "WhatsApp", href: `https://wa.me/?text=${enc(`${text}\n${url}`)}` },
    { label: "Telegram", href: `https://t.me/share/url?url=${enc(url)}&text=${enc(text)}` },
    { label: "X", href: `https://twitter.com/intent/tweet?text=${enc(text)}&url=${enc(url)}` },
    { label: "Facebook", href: `https://www.facebook.com/sharer/sharer.php?u=${enc(url)}` },
    {
      label: "E-posta",
      href: `mailto:?subject=${enc("FORMAX — Bugünün Seçkisi")}&body=${enc(`${text}\n\n${url}`)}`,
    },
  ];

  const copy = async () => {
    const payload = `${text}\n${url}`;
    try {
      await navigator.clipboard.writeText(payload);
      setCopied(true);
      return;
    } catch {
      // Clipboard API yoksa/izin verilmediyse: gizli textarea + execCommand yedeği.
      try {
        const ta = document.createElement("textarea");
        ta.value = payload;
        ta.style.position = "fixed";
        ta.style.opacity = "0";
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand("copy");
        document.body.removeChild(ta);
        setCopied(ok);
      } catch {
        setCopied(false);
      }
    }
  };

  /**
   * GÖRSEL PAYLAŞIM — önce gerçek PNG üretilir (Canvas), sonra dosya olarak paylaşılır.
   * Tarayıcı dosya paylaşımını desteklemiyorsa (navigator.canShare({files}) false) sessizce
   * metne düşülür ve kullanıcıya bunun neden olduğu YAZILI olarak bildirilir — sahte başarı yok.
   */
  const shareImage = async () => {
    setBusy(true);
    setImgNote(null);
    try {
      const blob = await renderSelectionCard(lines, totalOdd);
      if (!blob) {
        setImgNote("Görsel bu cihazda oluşturulamadı.");
        return;
      }
      const file = new File([blob], "formax-secki.png", { type: "image/png" });

      if (navigator.canShare?.({ files: [file] }) && navigator.share) {
        await navigator.share({ files: [file], title: "FORMAX · Bugünün Seçkisi", text });
        return;
      }

      // Dosya paylaşımı yoksa: görseli indir (gerçek PNG kullanıcıda kalır).
      const href = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = href;
      a.download = "formax-secki.png";
      a.click();
      URL.revokeObjectURL(href);
      setImgNote("Bu tarayıcı görsel paylaşımını desteklemiyor; görsel indirildi.");
    } catch {
      setImgNote("Görsel paylaşımı tamamlanamadı.");
    } finally {
      setBusy(false);
    }
  };

  const nativeShare = () => {
    navigator.share?.({ title: "FORMAX · Bugünün Seçkisi", text, url }).catch(() => {});
  };

  return (
    <div className="fixed inset-0 z-[80] flex items-end justify-center" role="dialog" aria-modal="true" aria-label="Paylaş">
      <button
        type="button"
        aria-label="Kapat"
        onClick={onClose}
        className="absolute inset-0 bg-black/70 backdrop-blur-sm"
      />

      {/* Kompakt bottom sheet — ekranı tamamen kaplamaz; önizleme kendi içinde kayar. */}
      <div className="relative flex max-h-[72dvh] w-full max-w-[var(--app-max-width,430px)] flex-col overflow-hidden rounded-t-[24px] border-t border-white/10 bg-bg-deep">
        <div className="flex items-center justify-between border-b border-white/[0.07] px-5 py-4">
          <h2 className="text-[13px] font-bold uppercase tracking-[0.14em] text-white">Paylaş</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Kapat"
            className="flex h-8 w-8 items-center justify-center rounded-full text-white/70 transition-colors hover:bg-white/5 active:scale-95"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" aria-hidden="true">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          {/* ÖNİZLEME KARTI — değerler ekrandaki gerçek seçkiden */}
          <div className="rounded-[20px] border border-white/10 bg-white/[0.04] p-4">
            <p className="text-center text-[12px] font-bold uppercase tracking-[0.22em] text-neon">Formax</p>

            <div className="mt-3 space-y-3">
              {lines.map((l, i) => (
                <div
                  key={l.matchId}
                  className={i > 0 ? "border-t border-white/[0.07] pt-3" : undefined}
                >
                  {l.time && (
                    <p className="text-[10px] font-semibold uppercase tracking-wide text-white/45">{l.time}</p>
                  )}
                  <p className="mt-1 text-[13.5px] font-semibold text-white">
                    {l.home} — {l.away}
                  </p>
                  {l.market && (
                    <p className="mt-1.5 text-[12.5px] text-white/70">{l.market}</p>
                  )}
                  {(l.probability != null || l.odd != null) && (
                    <p className="mt-0.5 text-[13px] font-bold tabular-nums text-neon">
                      {l.probability != null ? `%${l.probability}` : ""}
                      {l.probability != null && l.odd != null ? " · " : ""}
                      {l.odd != null ? <span className="text-white">{l.odd.toFixed(2)}</span> : null}
                    </p>
                  )}
                </div>
              ))}
            </div>

            <div className="mt-4 flex items-end justify-between border-t border-white/[0.07] pt-3.5">
              <span className="text-[11px] font-bold uppercase tracking-wide text-white/55">
                {lines.length} Maç
              </span>
              {/* Toplam oran yalnız mevcut ürün mantığı bir değer verdiyse gösterilir. */}
              {totalOdd != null && (
                <span className="text-right">
                  <span className="block text-[9.5px] font-semibold uppercase tracking-wide text-white/45">
                    Toplam Oran
                  </span>
                  <span className="block text-[18px] font-bold leading-none tabular-nums text-neon">
                    {totalOdd.toFixed(2)}
                  </span>
                </span>
              )}
            </div>
          </div>

          {/* Hedefler */}
          <div className="mt-4 grid grid-cols-2 gap-2">
            {/* GERÇEK GÖRSEL: yukarıdaki önizlemenin aynısı PNG olarak çizilir. */}
            <button
              type="button"
              onClick={() => void shareImage()}
              disabled={busy}
              className="col-span-2 flex h-12 items-center justify-center rounded-2xl bg-neon text-[13px] font-bold uppercase tracking-wide text-black transition-transform active:scale-[0.98] disabled:opacity-60"
            >
              {busy ? "Görsel hazırlanıyor…" : "Görsel Olarak Paylaş"}
            </button>

            {imgNote && (
              <p className="col-span-2 -mt-1 text-center text-[11px] leading-snug text-white/50">
                {imgNote}
              </p>
            )}

            {canNativeShare && (
              <button
                type="button"
                onClick={nativeShare}
                className="col-span-2 flex h-12 items-center justify-center rounded-2xl border border-white/10 text-[13px] font-semibold text-white transition-colors hover:bg-white/5 active:scale-[0.98]"
              >
                Metin Olarak Paylaş
              </button>
            )}

            {targets.map((t) => (
              <a
                key={t.label}
                href={t.href}
                target="_blank"
                rel="noopener noreferrer"
                className="flex h-12 items-center justify-center rounded-2xl border border-white/10 text-[13px] font-semibold text-white transition-colors hover:bg-white/5 active:scale-[0.98]"
              >
                {t.label}
              </a>
            ))}

            <button
              type="button"
              onClick={() => void copy()}
              className="col-span-2 flex h-12 items-center justify-center rounded-2xl border border-white/10 text-[13px] font-semibold text-white transition-colors hover:bg-white/5 active:scale-[0.98]"
            >
              {copied ? "Kopyalandı" : "Bağlantıyı Kopyala"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
