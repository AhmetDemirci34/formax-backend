import { UserIcon } from "@/components/discover/icons";
import type { PredictionCounts } from "@/types/predictions";
import { PerformanceSummary } from "./PerformanceSummary";

/**
 * PredictionHeader — marka (F) + "TAHMİNLERİM" başlığı + kompakt özet + profil.
 * (AppShell'in sticky üst bölgesine verilir; global chrome tarafından sarılır.)
 */
export function PredictionHeader({ counts }: { counts: PredictionCounts }) {
  return (
    <div className="px-4 pb-2 pt-3">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-goalai-accent text-[18px] font-black italic text-[#0a0e16]">
            F
          </span>
          <div className="leading-tight">
            <h1 className="text-[24px] font-bold uppercase tracking-tight text-text-primary">Tahminlerim</h1>
            <p className="text-[11px] font-medium text-text-muted">Seçimlerini yap, takip et, sonucu gör</p>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          <PerformanceSummary counts={counts} />
          <span className="flex h-9 w-9 items-center justify-center rounded-full bg-goalai-surface-bright ring-1 ring-white/10">
            <UserIcon size={18} className="text-text-secondary" />
          </span>
        </div>
      </div>
    </div>
  );
}
