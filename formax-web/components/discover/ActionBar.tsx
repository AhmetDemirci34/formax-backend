"use client";

// Kart altı aksiyon — GEÇ (kırmızı X) · «««« / »»»» · İLGİMİ ÇEKTİ (yeşil kalp).
interface Props {
  onPass: () => void;
  onLike: () => void;
  disabled?: boolean;
}

export function ActionBar({ onPass, onLike, disabled }: Props) {
  return (
    <div className="flex items-center justify-between px-8 pt-5 pb-2">
      {/* GEÇ */}
      <div className="flex flex-col items-center gap-1.5">
        <button
          onClick={onPass}
          disabled={disabled}
          aria-label="Geç"
          className="w-16 h-16 rounded-full border-2 border-[#EF4444] flex items-center justify-center shadow-[0_0_22px_rgba(239,68,68,0.45)] disabled:opacity-40 active:scale-95 transition-transform"
        >
          <XIcon />
        </button>
        <span className="text-xs font-bold tracking-wide text-[#EF4444]">GEÇ</span>
      </div>

      {/* Oklar */}
      <div className="flex items-center gap-3 -mt-5">
        <span className="text-[#EF4444] text-lg font-bold tracking-tighter opacity-80">‹‹‹‹‹</span>
        <span className="w-px h-5 bg-white/15" />
        <span className="text-[#34D27A] text-lg font-bold tracking-tighter opacity-80">›››››</span>
      </div>

      {/* İLGİMİ ÇEKTİ */}
      <div className="flex flex-col items-center gap-1.5">
        <button
          onClick={onLike}
          disabled={disabled}
          aria-label="İlgimi çekti"
          className="w-16 h-16 rounded-full border-2 border-[#34D27A] flex items-center justify-center shadow-[0_0_22px_rgba(52,210,122,0.45)] disabled:opacity-40 active:scale-95 transition-transform"
        >
          <HeartIcon />
        </button>
        <span className="text-xs font-bold tracking-wide text-[#34D27A]">İLGİMİ ÇEKTİ</span>
      </div>
    </div>
  );
}

function XIcon() {
  return (
    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#EF4444" strokeWidth="2.6" strokeLinecap="round">
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  );
}

function HeartIcon() {
  return (
    <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="#34D27A" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 21s-7.5-4.6-10-9.2C.6 8.5 2.3 5 5.7 5c2 0 3.4 1.1 4.3 2.3C10.9 6.1 12.3 5 14.3 5c3.4 0 5.1 3.5 3.7 6.8C19.5 16.4 12 21 12 21z" />
    </svg>
  );
}
