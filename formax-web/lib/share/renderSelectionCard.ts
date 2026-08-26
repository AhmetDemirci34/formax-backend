/**
 * FORMAX · Bugünün Seçkisi — paylaşılabilir GÖRSEL üretimi (PNG).
 *
 * NEDEN CANVAS: HTML modal paylaşılabilir bir görsel DEĞİLDİR; navigator.share yalnız
 * metin/URL taşır. Gerçek görsel paylaşımı için bir raster gerekiyor. html-to-image /
 * html2canvas gibi bir bağımlılık eklemek yerine kart doğrudan Canvas 2D ile çizilir:
 * yeni paket yok, tam kontrol, birebir FORMAX dili.
 *
 * VERİ: yalnız ekrandaki gerçek seçki satırları ve mevcut `comboTotalOdd` sonucu çizilir.
 * Bu dosya hiçbir değer HESAPLAMAZ, ÜRETMEZ; oran/olasılık dönüşümü YAPMAZ.
 */

export interface SelectionCardLine {
  time: string;
  home: string;
  away: string;
  market: string | null;
  probability: number | null;
  odd: number | null;
}

const BG = "#0B0D14";
const CARD = "#12151F";
const NEON = "#CCFF00";
const WHITE = "#FFFFFF";
const MUTED = "rgba(255,255,255,0.45)";
const LINE = "rgba(255,255,255,0.10)";

/** Cihaz bağımsız sabit çözünürlük — paylaşımda net görünsün. */
const W = 1080;
const PAD = 72;
const SCALE = 2;

function roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number) {
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h, r);
  ctx.arcTo(x + w, y + h, x, y + h, r);
  ctx.arcTo(x, y + h, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

/**
 * Seçkiyi PNG Blob olarak çizer. Satır sayısına göre yükseklik hesaplanır
 * (2 maç da 5 maç da doğal görünür).
 */
export async function renderSelectionCard(
  lines: SelectionCardLine[],
  totalOdd: number | null
): Promise<Blob | null> {
  if (typeof document === "undefined" || lines.length === 0) return null;

  const rowH = 190;
  const headerH = 210;
  // Özet satırı çizildikten sonra kart altında dengeli bir pay kalsın (ölçüldü: 200'de
  // altta ~120px boş alan oluşuyordu, kart alt-ağır görünüyordu).
  const footerH = totalOdd != null ? 140 : 110;
  const H = headerH + lines.length * rowH + footerH;

  const canvas = document.createElement("canvas");
  canvas.width = W * SCALE;
  canvas.height = H * SCALE;
  const ctx = canvas.getContext("2d");
  if (!ctx) return null;
  ctx.scale(SCALE, SCALE);

  // Zemin
  ctx.fillStyle = BG;
  ctx.fillRect(0, 0, W, H);

  // Kart gövdesi
  ctx.fillStyle = CARD;
  roundRect(ctx, PAD / 2, PAD / 2, W - PAD, H - PAD, 40);
  ctx.fill();
  ctx.strokeStyle = LINE;
  ctx.lineWidth = 2;
  ctx.stroke();

  // Marka
  ctx.fillStyle = NEON;
  ctx.font = "700 44px system-ui, -apple-system, Segoe UI, sans-serif";
  ctx.textAlign = "center";
  ctx.letterSpacing = "10px";
  ctx.fillText("FORMAX", W / 2, 132);
  ctx.letterSpacing = "0px";

  ctx.fillStyle = MUTED;
  ctx.font = "500 24px system-ui, -apple-system, Segoe UI, sans-serif";
  ctx.fillText("Bugünün Seçkisi", W / 2, 172);

  // Satırlar
  let y = headerH;
  ctx.textAlign = "left";

  for (let i = 0; i < lines.length; i++) {
    const l = lines[i];

    if (i > 0) {
      ctx.strokeStyle = LINE;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(PAD, y - 34);
      ctx.lineTo(W - PAD, y - 34);
      ctx.stroke();
    }

    if (l.time) {
      ctx.fillStyle = MUTED;
      ctx.font = "600 22px system-ui, -apple-system, Segoe UI, sans-serif";
      ctx.fillText(l.time.toLocaleUpperCase("tr-TR"), PAD, y);
    }

    ctx.fillStyle = WHITE;
    ctx.font = "600 34px system-ui, -apple-system, Segoe UI, sans-serif";
    ctx.fillText(`${l.home} — ${l.away}`, PAD, y + 46);

    if (l.market) {
      ctx.fillStyle = "rgba(255,255,255,0.72)";
      ctx.font = "500 27px system-ui, -apple-system, Segoe UI, sans-serif";
      ctx.fillText(l.market, PAD, y + 92);
    }

    // Olasılık (neon) ve oran (beyaz) — sağda, birbirinden ayrı
    ctx.textAlign = "right";
    if (l.probability != null) {
      ctx.fillStyle = NEON;
      ctx.font = "700 34px system-ui, -apple-system, Segoe UI, sans-serif";
      ctx.fillText(`%${l.probability}`, W - PAD - (l.odd != null ? 130 : 0), y + 92);
    }
    if (l.odd != null) {
      ctx.fillStyle = WHITE;
      ctx.font = "700 34px system-ui, -apple-system, Segoe UI, sans-serif";
      ctx.fillText(l.odd.toFixed(2), W - PAD, y + 92);
    }
    ctx.textAlign = "left";

    y += rowH;
  }

  // Özet
  ctx.strokeStyle = LINE;
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(PAD, y - 34);
  ctx.lineTo(W - PAD, y - 34);
  ctx.stroke();

  ctx.fillStyle = MUTED;
  ctx.font = "600 24px system-ui, -apple-system, Segoe UI, sans-serif";
  ctx.fillText(`${lines.length} MAÇ`, PAD, y + 24);

  if (totalOdd != null) {
    ctx.textAlign = "right";
    ctx.fillStyle = MUTED;
    ctx.font = "600 20px system-ui, -apple-system, Segoe UI, sans-serif";
    ctx.fillText("TOPLAM ORAN", W - PAD, y - 4);
    ctx.fillStyle = NEON;
    ctx.font = "700 46px system-ui, -apple-system, Segoe UI, sans-serif";
    ctx.fillText(totalOdd.toFixed(2), W - PAD, y + 44);
    ctx.textAlign = "left";
  }

  return new Promise((resolve) => canvas.toBlob((b) => resolve(b), "image/png"));
}
