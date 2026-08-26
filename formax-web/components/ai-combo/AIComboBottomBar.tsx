"use client";

/**
 * FORMAX · AI Kombin Analizi — sabit alt aksiyon barı.
 * "Tahminlerime Ekle" gerçek yerel kalıcılık (predictionRepository/localStorage)
 * yazar; "Paylaş" native share sheet'i tetikler. Sahte başarı yok.
 */
export function AIComboBottomBar({
  saved,
  onAdd,
  onShare,
}: {
  saved: boolean;
  onAdd: () => void;
  onShare: () => void;
}) {
  return (
    <div className="fixed bottom-0 left-1/2 z-[60] flex w-full max-w-[var(--app-max-width,430px)] -translate-x-1/2 items-center gap-3 border-t border-white/10 bg-bg-deep/95 px-4 pb-[max(var(--safe-bottom,16px),16px)] pt-3 backdrop-blur-xl">
      <button
        type="button"
        onClick={onAdd}
        aria-pressed={saved}
        className="flex h-[52px] flex-1 items-center justify-center gap-2 rounded-2xl border border-white/10 text-[14px] font-bold uppercase tracking-wide text-white transition-transform active:scale-[0.98]"
      >
        {saved ? <CheckGlyph /> : <BookmarkGlyph />}
        {saved ? "Eklendi" : "Tahminlerime Ekle"}
      </button>

      <button
        type="button"
        onClick={onShare}
        className="flex h-[52px] flex-1 items-center justify-center gap-2 rounded-2xl bg-neon text-[14px] font-bold uppercase tracking-wide text-black transition-transform active:scale-[0.98]"
      >
        <ShareGlyph />
        Paylaş
      </button>
    </div>
  );
}

function BookmarkGlyph() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z" />
    </svg>
  );
}
function CheckGlyph() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={3} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M5 12l5 5L19 6" />
    </svg>
  );
}
function ShareGlyph() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" />
      <path d="M8.6 13.5l6.8 4M15.4 6.5l-6.8 4" />
    </svg>
  );
}
