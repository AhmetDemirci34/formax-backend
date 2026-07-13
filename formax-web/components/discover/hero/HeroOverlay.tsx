/**
 * FORMAX · HeroOverlay (05)
 * Arka plan üzerine metin okunabilirliği için alt→üst koyu gradient (token tabanlı).
 */
export function HeroOverlay() {
  return (
    <div
      aria-hidden
      className="absolute inset-0 bg-gradient-to-t from-bg-deep via-bg-deep/30 to-transparent"
    />
  );
}
