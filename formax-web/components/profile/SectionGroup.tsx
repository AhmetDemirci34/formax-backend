import { Children, Fragment, type ReactNode } from "react";

interface SectionGroupProps {
  /** Grup başlığı — Semibold, gri, ALL CAPS. */
  title: string;
  /**
   * Divider'ın soldan içeri girme miktarı (px). Varsayılan 52 = 16px padding +
   * 20px ikon + 16px boşluk, yani başlık metniyle hizalı (Profil Merkezi).
   * Sol ikonu olmayan listelerde (Hesabım) 16 verilir.
   */
  dividerInset?: number;
  /**
   * Ayırıcıyı daha görünür tona alır (%5 → %10 beyaz). Hesabım ekranında
   * Stitch'teki divider görünürlüğüne yaklaşmak için kullanılır.
   */
  dividerStrong?: boolean;
  /**
   * Başlık tonu. `muted` (varsayılan) gri — Profil Merkezi / Hesabım.
   * `accent` lime — Bildirim Tercihleri ekranı.
   */
  titleTone?: "muted" | "accent";
  /** Başlık ile kart arasına giren açıklama metni (opsiyonel). */
  description?: string;
  /** Kart köşe yarıçapı (px). Varsayılan 16. */
  radius?: number;
  children: ReactNode;
}

/**
 * SectionGroup — "HIZLI ERİŞİM" / "GENEL" grupları (Stitch görseli).
 *
 *   • Panel ekran kenarından 16px içeride, köşeleri 16px yuvarlatılmış kart
 *     (`overflow-hidden` → ilk satır üstten, son satır alttan yuvarlanır).
 *   • Başlık panelin DIŞINDA, koyu sayfa zemininde, panelin sol kenarından
 *     biraz içeride.
 *   • Divider satırlar ARASINDA; soldan 52px (ikon sütunundan sonra, başlıkla
 *     hizalı), sağda panel kenarına kadar. Son satırdan sonra çizilmez.
 */
export function SectionGroup({
  title,
  dividerInset = 52,
  dividerStrong = false,
  titleTone = "muted",
  description,
  radius = 16,
  children,
}: SectionGroupProps) {
  const items = Children.toArray(children);
  const dividerColor = dividerStrong
    ? "var(--profile-divider-strong)"
    : "var(--profile-divider)";

  return (
    <section>
      <h2
        className={`px-6 text-[11px] font-semibold uppercase leading-none tracking-[0.6px] ${
          description ? "pb-2.5" : "pb-2"
        } ${titleTone === "accent" ? "text-profile-accent" : "text-profile-muted"}`}
      >
        {title}
      </h2>

      {description ? (
        <p className="px-6 pb-3 text-[12px] leading-[1.45] text-profile-muted">{description}</p>
      ) : null}

      <div className="mx-4 overflow-hidden bg-profile-container" style={{ borderRadius: radius }}>
        {items.map((item, index) => (
          <Fragment key={index}>
            {item}
            {index < items.length - 1 ? (
              <div
                className="h-px"
                style={{ marginLeft: dividerInset, background: dividerColor }}
                role="presentation"
              />
            ) : null}
          </Fragment>
        ))}
      </div>
    </section>
  );
}
