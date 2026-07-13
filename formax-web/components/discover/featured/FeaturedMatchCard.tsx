import { Badge } from "@/components/ui/Badge";
import { TeamLogoPair } from "@/components/ui/TeamLogoPair";
import type { FeaturedMatchVM } from "./featuredData";

/**
 * FORMAX · FeaturedMatchCard (10, new)
 * "Sana Özel Maçlar" öne çıkan maç kartı: ÖNE ÇIKAN rozeti + saat + armalar + isimler
 * + ilgi etiketleri + ilgi skoru barı. Combo/AI Olası kartlarıyla aynı yüzey dili.
 */
export function FeaturedMatchCard({ time, home, away, tags, interestScore }: FeaturedMatchVM) {
  return (
    <div className="flex w-[150px] shrink-0 flex-col items-center gap-2.5 rounded-2xl border border-white/[0.06] bg-white/[0.03] p-3 text-center">
      <div className="self-start">
        <Badge label="Öne Çıkan" tone="neon" />
      </div>

      <span className="text-[10px] font-medium text-text-muted">{time}</span>

      <TeamLogoPair home={home} away={away} size={28} showVs />

      <div className="flex w-full items-start justify-center gap-2 leading-tight">
        <span className="flex-1 truncate text-[11px] font-bold text-text-primary">{home.name}</span>
        <span className="flex-1 truncate text-[11px] font-bold text-text-primary">{away.name}</span>
      </div>

      <div className="flex flex-wrap items-center justify-center gap-1">
        {tags.map((t) => (
          <Badge key={t.label} label={t.label} tone={t.tone} />
        ))}
      </div>

      <div className="mt-auto w-full pt-1">
        <div className="mb-1 flex items-center justify-between">
          <span className="text-[9px] font-semibold uppercase tracking-wide text-text-muted">
            İlgi Skorun
          </span>
          <span className="text-[11px] font-extrabold tabular-nums text-signal-purple">
            %{interestScore}
          </span>
        </div>
        <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/[0.08]">
          <div
            className="h-full rounded-full bg-signal-purple"
            style={{ width: `${Math.min(100, Math.max(0, interestScore))}%` }}
          />
        </div>
      </div>
    </div>
  );
}
