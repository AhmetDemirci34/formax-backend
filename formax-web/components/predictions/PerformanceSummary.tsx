import type { PredictionCounts } from "@/types/predictions";

/**
 * PerformanceSummary — kompakt özet kutusu (header sağ üst).
 * NOT: Backend'de kazanılan/kaybedilen (T/K/B) sonuç verisi YOK; bu yüzden
 * uydurulmadı. Gerçek, türetilebilir sayımlar gösterilir: Aktif / Bekleyen / Toplam.
 */
export function PerformanceSummary({ counts }: { counts: PredictionCounts }) {
  return (
    <div className="flex items-center gap-2.5 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-3 py-1.5">
      <BarsGlyph />
      <div className="flex items-center gap-2.5">
        <Stat value={counts.active} label="Aktif" accent />
        <Divider />
        <Stat value={counts.pending} label="Bekleyen" />
        <Divider />
        <Stat value={counts.total} label="Toplam" />
      </div>
    </div>
  );
}

function Stat({ value, label, accent }: { value: number; label: string; accent?: boolean }) {
  return (
    <div className="flex flex-col items-center leading-none">
      <span className={`text-[15px] font-bold ${accent ? "text-goalai-accent" : "text-text-primary"}`}>{value}</span>
      <span className="mt-0.5 text-[8px] font-semibold uppercase tracking-wide text-text-muted">{label}</span>
    </div>
  );
}

function Divider() {
  return <span className="h-6 w-px bg-white/[0.08]" />;
}

function BarsGlyph() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" className="text-goalai-accent" aria-hidden="true">
      <path d="M5 20V10M12 20V4M19 20v-6" />
    </svg>
  );
}
