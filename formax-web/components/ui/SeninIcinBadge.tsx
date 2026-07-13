// Yeşil "SENİN İÇİN" rozeti — yıldız-rozet ikon + metin. Görünürlük kararı parent'ta.
export function SeninIcinBadge() {
  return (
    <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full border border-[#34D27A]/50 bg-[#34D27A]/10">
      <BadgeStarIcon />
      <span className="text-xs font-bold tracking-wide text-[#34D27A]">SENİN İÇİN</span>
    </span>
  );
}

function BadgeStarIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#34D27A" strokeWidth={1.8} strokeLinejoin="round">
      <path d="M12 2l2.6 2.1 3.3-.4 1.1 3.2 2.9 1.7-1.2 3.1 1.2 3.1-2.9 1.7-1.1 3.2-3.3-.4L12 22l-2.6-2.1-3.3.4-1.1-3.2L2.1 15.4l1.2-3.1-1.2-3.1 2.9-1.7 1.1-3.2 3.3.4z" />
      <polyline points="9 12 11.2 14 15 9.5" stroke="#34D27A" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
