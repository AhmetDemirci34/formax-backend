import { GlassCard } from "@/components/ui/GlassCard";
import { StatPill } from "@/components/ui/StatPill";
import { LiveUpdateBadge } from "@/components/ui/LiveUpdateBadge";
import {
  SparklesIcon,
  ActivityIcon,
  UsersIcon,
  ShieldIcon,
  ChevronRightIcon,
} from "@/components/discover/icons";

interface AICommentSectionProps {
  comment?: string;
  updatedLabel?: string;
}

// PNG-2 referans içeriği (statik; iş mantığı/API yok — ileride backend besler).
const DEFAULT_COMMENT =
  "Bugünkü maçlar arasında veri olarak en yüksek potansiyele sahip karşılaşma bu. " +
  "Tempo, pres ve geçiş oyunları ön planda. Yüksek tempolu, bol pozisyonlu bir 90 dakika bekleniyor.";

/**
 * FORMAX · AICommentSection (07)
 * Hero'nun hemen altında AI'ın ilk yorumu. Hero ile aynı glass/glow/border/spacing —
 * yeni tasarım dili yok, Hero'nun devamı gibi. Design Token; hardcoded renk yok.
 */
export function AICommentSection({
  comment = DEFAULT_COMMENT,
  updatedLabel = "Son güncelleme: 2 dk önce",
}: AICommentSectionProps) {
  return (
    <GlassCard sectionGlow className="p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <RobotAvatar />
          <div className="flex items-center gap-1.5">
            <SparklesIcon size={14} className="shrink-0 text-neon" />
            <span className="whitespace-nowrap text-[13px] font-bold uppercase tracking-[0.04em] text-text-primary">
              FORMAX AI Yorumu
            </span>
          </div>
        </div>
        <LiveUpdateBadge label={updatedLabel} />
      </div>

      <p className="mt-3.5 text-[13px] leading-[1.65] text-text-secondary">{comment}</p>

      <div className="mt-4 grid grid-cols-4 gap-2">
        <StatPill icon={<ActivityIcon size={15} />} value="624" label="Haber analiz edildi" />
        <StatPill icon={<UsersIcon size={15} />} value="18" label="Kaynak doğrulandı" />
        <StatPill icon={<ShieldIcon size={15} />} value="%82" label="Veri güvenilirliği" />
        <StatPill icon={<SparklesIcon size={15} />} value="3" label="AI Modeli onayladı" />
      </div>

      <button
        type="button"
        className="mt-4 inline-flex items-center gap-0.5 text-[12px] font-semibold text-neon transition-opacity hover:opacity-80"
      >
        Detaylı analizi gör
        <ChevronRightIcon size={14} />
      </button>
    </GlassCard>
  );
}

/** Robot avatar placeholder — neon parıltılı gözler (Design Token). */
function RobotAvatar() {
  return (
    <span className="fx-glow-soft-green grid h-10 w-10 shrink-0 place-items-center rounded-full bg-bg-glass ring-1 ring-neon/25">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden>
        <rect x="5" y="7" width="14" height="11" rx="3.5" stroke="currentColor" strokeWidth="1.6" className="text-text-secondary" />
        <path d="M12 4v2.5M9 18v2M15 18v2" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" className="text-text-secondary" />
        <circle cx="9.5" cy="12.5" r="1.6" className="fill-neon fx-icon-glow-green" />
        <circle cx="14.5" cy="12.5" r="1.6" className="fill-neon fx-icon-glow-green" />
      </svg>
    </span>
  );
}
