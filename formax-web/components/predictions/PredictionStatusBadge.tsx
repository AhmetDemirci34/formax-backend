import type { UiPredictionStatus } from "@/types/predictions";

/**
 * PredictionStatusBadge — "Devam Ediyor" / "Başlamadı" / "Bitti" rozeti.
 * Canlı: lime nabız noktası; bekleyen: nötr; bitmiş: soluk.
 */
export function PredictionStatusBadge({ status }: { status: UiPredictionStatus }) {
  if (status === "live") {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-goalai-accent/15 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-goalai-accent">
        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-goalai-accent" />
        Devam Ediyor
      </span>
    );
  }
  if (status === "finished") {
    return (
      <span className="inline-flex items-center rounded-full bg-white/10 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-text-muted">
        Bitti
      </span>
    );
  }
  return (
    <span className="inline-flex items-center rounded-full bg-white/[0.06] px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-text-secondary">
      Başlamadı
    </span>
  );
}
