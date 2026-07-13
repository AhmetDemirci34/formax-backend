import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { LiveUpdateBadge } from "@/components/ui/LiveUpdateBadge";
import { TargetIcon } from "@/components/discover/icons";
import { RadarVisual } from "./RadarVisual";
import { RadarSignalCard, type RadarSignal } from "./RadarSignalCard";

// Referans dili (Man City vs Liverpool bağlamı, Hero/Predictions ile tutarlı) — statik.
const SIGNALS: RadarSignal[] = [
  {
    id: "son-dakika",
    category: "Son Dakika",
    tone: "red",
    severityLabel: "Kritik",
    title: "Rodri kadroda yok",
    detail: "Man City orta sahada eksik; Liverpool'un pres baskısı öne çıkabilir.",
  },
  {
    id: "taktik",
    category: "Taktik",
    tone: "amber",
    severityLabel: "Yüksek",
    title: "Yüksek savunma hattı",
    detail: "Liverpool'un yüksek hattı, Haaland'ın derinlik koşularına açık kapı bırakıyor.",
  },
  {
    id: "dinamik",
    category: "Dinamik",
    tone: "blue",
    severityLabel: "Orta",
    title: "Tempo yükseliyor",
    detail: "Son 15 dakikada iki takım da pozisyon üretiminde belirgin artışta.",
  },
  {
    id: "hava",
    category: "Hava",
    tone: "neon",
    severityLabel: "Düşük",
    title: "Hafif yağış bekleniyor",
    detail: "Etihad'da zemin hızlanabilir; hatalı pas ve dinamik atak riski artar.",
  },
];

/**
 * FORMAX · Radar'ın Öne Çıkardıkları (01, new)
 * AI Önerilerim akışında AI Yorumu'ndan sonra gelen radar bloğu.
 * Section kalıbı AIPredictionsSection ile birebir: GlassCard sectionGlow p-5 + SectionHeader.
 */
export function RadarSection() {
  const top = SIGNALS[0];

  return (
    <GlassCard sectionGlow className="p-5">
      <SectionHeader
        icon={<TargetIcon size={16} />}
        accent="neon"
        title="Radar'ın Öne Çıkardıkları"
        subtitle="AI'nın bu maçta yakaladığı sinyaller"
        right={<LiveUpdateBadge label="Canlı tarama" />}
      />

      <div className="mt-1 flex items-center gap-4">
        <RadarVisual count={SIGNALS.length} />
        <div className="min-w-0">
          <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-text-muted">
            En yüksek öncelik
          </p>
          <p className="mt-1 text-[15px] font-extrabold leading-tight text-text-primary">
            {top.title}
          </p>
          <p className="mt-1 text-[11px] leading-relaxed text-text-secondary">{top.detail}</p>
        </div>
      </div>

      <div className="mt-4 flex flex-col gap-2.5">
        {SIGNALS.map((s) => (
          <RadarSignalCard key={s.id} {...s} />
        ))}
      </div>
    </GlassCard>
  );
}
