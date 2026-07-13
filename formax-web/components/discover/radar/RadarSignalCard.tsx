import { Badge } from "@/components/ui/Badge";

export type RadarTone = "neon" | "purple" | "amber" | "red" | "blue" | "muted";

export interface RadarSignal {
  id: string;
  /** Sinyal ailesi: SON DAKİKA · TAKTİK · DİNAMİK · HAVA. */
  category: string;
  /** Severity rengi (tek merkez, Badge tone ile aynı). */
  tone: RadarTone;
  title: string;
  detail: string;
  /** Öncelik etiketi: KRİTİK · YÜKSEK · ORTA · DÜŞÜK. */
  severityLabel: string;
}

const BAR: Record<RadarTone, string> = {
  neon: "bg-neon",
  purple: "bg-signal-purple",
  amber: "bg-signal-amber",
  red: "bg-signal-red",
  blue: "bg-signal-blue",
  muted: "bg-text-muted",
};

/**
 * FORMAX · RadarSignalCard (01, new)
 * Tek radar sinyali: severity aksan çubuğu + kategori rozeti + öncelik + başlık + açıklama.
 * StatPill ile aynı yüzey dili (border-white/[0.06] · bg-white/[0.03] · rounded-2xl).
 */
export function RadarSignalCard({ category, tone, title, detail, severityLabel }: RadarSignal) {
  return (
    <div className="flex items-stretch gap-3 rounded-2xl border border-white/[0.06] bg-white/[0.03] p-3.5">
      <span className={`w-1 shrink-0 self-stretch rounded-full ${BAR[tone]}`} aria-hidden />
      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-2">
          <Badge label={category} tone={tone} />
          <span className="shrink-0 text-[10px] font-bold uppercase tracking-wide text-text-muted">
            {severityLabel}
          </span>
        </div>
        <h3 className="mt-2 text-[13px] font-bold leading-tight text-text-primary">{title}</h3>
        <p className="mt-1 text-[11px] leading-relaxed text-text-secondary">{detail}</p>
      </div>
    </div>
  );
}
