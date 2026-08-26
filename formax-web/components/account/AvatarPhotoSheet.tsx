"use client";

import { useCallback, useRef, useState } from "react";
import { AnimatePresence, motion } from "framer-motion";
import { haptic } from "@/lib/utils/haptics";

/** Kırpma alanının ekrandaki ölçüsü (px) ve dışa aktarılan kare boyutu. */
const VIEWPORT = 260;
const EXPORT_SIZE = 512;

interface AvatarPhotoSheetProps {
  open: boolean;
  onClose: () => void;
  /** Kırpılmış 1:1 görsel onaylandığında çağrılır (yükleme burada başlar). */
  onConfirm: (blob: Blob) => Promise<void>;
}

type Step = "options" | "crop";

/**
 * AvatarPhotoSheet — "Profil Fotoğrafını Güncelle" bottom sheet'i.
 *
 * Akış: Fotoğraf Çek / Galeriden Seç / İptal → 1:1 kırpma (yuvarlak önizleme)
 * → onay → yükleme. Yükleme sırasında sheet kilitlenir; hata olursa mevcut
 * avatar korunur ve kısa hata mesajı gösterilir.
 *
 * Web'de "Fotoğraf Çek" `capture` özniteliğiyle cihaz kamerasını, "Galeriden
 * Seç" standart dosya seçiciyi açar.
 * TODO(native): Uygulama native kabuğa taşındığında bunlar Android sistem Photo
 * Picker'ı ve iOS Photo Library / Camera ile değiştirilecek.
 */
export function AvatarPhotoSheet({ open, onClose, onConfirm }: AvatarPhotoSheetProps) {
  const cameraInputRef = useRef<HTMLInputElement>(null);
  const galleryInputRef = useRef<HTMLInputElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);
  const dragStart = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const [step, setStep] = useState<Step>("options");
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [natural, setNatural] = useState<{ w: number; h: number } | null>(null);
  const [zoom, setZoom] = useState(1);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  /**
   * Kapanışta her şeyi başlangıç durumuna al ve object URL'i serbest bırak.
   * Bilinçli olarak effect DEĞİL: durum sıfırlama kullanıcı eylemine bağlıdır,
   * effect içinde setState zincirleme render'a yol açar.
   */
  const close = useCallback(() => {
    if (imageUrl) URL.revokeObjectURL(imageUrl);
    setImageUrl(null);
    setStep("options");
    setNatural(null);
    setZoom(1);
    setOffset({ x: 0, y: 0 });
    setBusy(false);
    setError(null);
    onClose();
  }, [imageUrl, onClose]);

  const pickFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ""; // aynı dosya tekrar seçilebilsin
    if (!file) return;
    setError(null);
    setImageUrl(URL.createObjectURL(file));
    setZoom(1);
    setOffset({ x: 0, y: 0 });
    setStep("crop");
  };

  /** Görselin kırpma alanını daima kaplaması için taban ölçek. */
  const baseScale = natural ? Math.max(VIEWPORT / natural.w, VIEWPORT / natural.h) : 1;
  const scale = baseScale * zoom;
  const drawW = natural ? natural.w * scale : 0;
  const drawH = natural ? natural.h * scale : 0;

  /** Kenarlarda boşluk kalmaması için kaydırmayı verilen zoom'a göre sınırla. */
  const clampAt = useCallback(
    (o: { x: number; y: number }, z: number) => {
      const s = baseScale * z;
      const w = natural ? natural.w * s : 0;
      const h = natural ? natural.h * s : 0;
      const maxX = Math.max(0, (w - VIEWPORT) / 2);
      const maxY = Math.max(0, (h - VIEWPORT) / 2);
      return {
        x: Math.min(maxX, Math.max(-maxX, o.x)),
        y: Math.min(maxY, Math.max(-maxY, o.y)),
      };
    },
    [baseScale, natural]
  );

  const onPointerDown = (e: React.PointerEvent) => {
    if (busy) return;
    (e.target as HTMLElement).setPointerCapture(e.pointerId);
    dragStart.current = { x: e.clientX, y: e.clientY, ox: offset.x, oy: offset.y };
  };
  const onPointerMove = (e: React.PointerEvent) => {
    const s = dragStart.current;
    if (!s) return;
    setOffset(clampAt({ x: s.ox + (e.clientX - s.x), y: s.oy + (e.clientY - s.y) }, zoom));
  };
  const onPointerUp = () => {
    dragStart.current = null;
  };

  /** Kırpma alanını 1:1 kareye çizip Blob üretir (ekrandaki yerleşimin aynısı). */
  const crop = async (): Promise<Blob> => {
    const img = imgRef.current;
    if (!img || !natural) throw new Error("NO_IMAGE");
    const k = EXPORT_SIZE / VIEWPORT;
    const canvas = document.createElement("canvas");
    canvas.width = EXPORT_SIZE;
    canvas.height = EXPORT_SIZE;
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("NO_CANVAS");
    const dx = (VIEWPORT - drawW) / 2 + offset.x;
    const dy = (VIEWPORT - drawH) / 2 + offset.y;
    ctx.drawImage(img, dx * k, dy * k, drawW * k, drawH * k);
    return new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(
        (b) => (b ? resolve(b) : reject(new Error("NO_BLOB"))),
        "image/jpeg",
        0.9
      );
    });
  };

  const handleConfirm = async () => {
    haptic();
    setBusy(true);
    setError(null);
    try {
      await onConfirm(await crop());
      close();
    } catch {
      // Hata: mevcut avatar korunur, kısa mesaj gösterilir.
      setError("Fotoğraf yüklenemedi. Lütfen daha sonra tekrar deneyin.");
      setBusy(false);
    }
  };

  return (
    <AnimatePresence>
      {open && (
        <div className="fixed inset-0 z-[85]">
          <motion.button
            type="button"
            aria-label="Kapat"
            onClick={busy ? undefined : close}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.22, ease: "easeInOut" }}
            className="absolute inset-0 bg-black/60 backdrop-blur-[2px]"
          />

          <motion.div
            role="dialog"
            aria-modal="true"
            aria-label="Profil Fotoğrafını Güncelle"
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            exit={{ y: "100%" }}
            transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
            className="absolute inset-x-0 bottom-0 rounded-t-[26px] bg-profile-container pb-[max(var(--safe-bottom),16px)]"
          >
            <div className="flex justify-center pt-3">
              <span className="h-1 w-10 rounded-full bg-white/20" />
            </div>

            <h2 className="px-5 pb-1 pt-4 text-center text-[15px] font-bold text-white">
              Profil Fotoğrafını Güncelle
            </h2>

            {step === "options" ? (
              <div className="px-4 pt-3">
                <SheetAction label="Fotoğraf Çek" onClick={() => cameraInputRef.current?.click()} />
                <div className="my-1 h-px bg-[var(--profile-divider-strong)]" />
                <SheetAction
                  label="Galeriden Seç"
                  onClick={() => galleryInputRef.current?.click()}
                />
                <div className="my-1 h-px bg-[var(--profile-divider-strong)]" />
                <SheetAction label="İptal" muted onClick={close} />
              </div>
            ) : (
              <div className="px-4 pt-3">
                {/* 1:1 kırpma alanı — yuvarlak avatar önizlemesi maskeyle gösterilir */}
                <div
                  className="relative mx-auto touch-none overflow-hidden rounded-2xl bg-black"
                  style={{ width: VIEWPORT, height: VIEWPORT }}
                  onPointerDown={onPointerDown}
                  onPointerMove={onPointerMove}
                  onPointerUp={onPointerUp}
                  onPointerCancel={onPointerUp}
                >
                  {imageUrl ? (
                    /* eslint-disable-next-line @next/next/no-img-element */
                    <img
                      ref={imgRef}
                      src={imageUrl}
                      alt=""
                      draggable={false}
                      onLoad={(e) =>
                        setNatural({
                          w: e.currentTarget.naturalWidth,
                          h: e.currentTarget.naturalHeight,
                        })
                      }
                      className="absolute max-w-none select-none"
                      style={{
                        width: drawW || undefined,
                        height: drawH || undefined,
                        left: (VIEWPORT - drawW) / 2 + offset.x,
                        top: (VIEWPORT - drawH) / 2 + offset.y,
                      }}
                    />
                  ) : null}

                  {/* Yuvarlak maske — kırpmanın avatarda nasıl görüneceğini gösterir */}
                  <div
                    className="pointer-events-none absolute inset-0"
                    style={{
                      background: `radial-gradient(circle at 50% 50%, transparent 0 ${
                        VIEWPORT / 2 - 1
                      }px, rgba(0,0,0,.55) ${VIEWPORT / 2}px)`,
                    }}
                  />
                  <div
                    className="pointer-events-none absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 rounded-full ring-2 ring-profile-accent/70"
                    style={{ width: VIEWPORT, height: VIEWPORT }}
                  />
                </div>

                <label className="mt-4 flex items-center gap-3 px-1">
                  <span className="text-[11px] font-semibold uppercase tracking-[0.5px] text-profile-muted">
                    Yakınlaştır
                  </span>
                  <input
                    type="range"
                    min={1}
                    max={3}
                    step={0.01}
                    value={zoom}
                    disabled={busy}
                    onChange={(e) => {
                      const z = Number(e.target.value);
                      setZoom(z);
                      setOffset((o) => clampAt(o, z));
                    }}
                    className="h-1 flex-1 accent-[var(--profile-accent)]"
                  />
                </label>

                {error ? (
                  <p role="alert" className="mt-3 text-center text-[12px] text-profile-danger">
                    {error}
                  </p>
                ) : null}

                <div className="mt-4 flex gap-3">
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setStep("options")}
                    className="h-12 flex-1 rounded-xl bg-white/[0.06] text-[15px] font-semibold text-white transition-colors active:bg-white/10 disabled:opacity-40"
                  >
                    Geri
                  </button>
                  <button
                    type="button"
                    disabled={busy || !natural}
                    onClick={handleConfirm}
                    className="h-12 flex-1 rounded-xl bg-profile-accent text-[15px] font-bold text-black transition-opacity active:opacity-80 disabled:opacity-40"
                  >
                    {busy ? "Yükleniyor…" : "Onayla"}
                  </button>
                </div>
              </div>
            )}

            {/* Gizli dosya girişleri — "Fotoğraf Çek" cihaz kamerasını açar. */}
            <input
              ref={cameraInputRef}
              type="file"
              accept="image/*"
              capture="user"
              onChange={pickFile}
              className="hidden"
            />
            <input
              ref={galleryInputRef}
              type="file"
              accept="image/*"
              onChange={pickFile}
              className="hidden"
            />
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

function SheetAction({
  label,
  muted = false,
  onClick,
}: {
  label: string;
  muted?: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      onClick={() => {
        haptic();
        onClick();
      }}
      className={`h-14 w-full rounded-xl text-[16px] font-medium transition-colors active:bg-white/10 ${
        muted ? "text-profile-muted" : "text-white"
      }`}
    >
      {label}
    </button>
  );
}
