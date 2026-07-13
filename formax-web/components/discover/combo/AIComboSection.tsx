import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { PrimaryCtaButton } from "@/components/ui/PrimaryCtaButton";
import { FlameIcon, ShieldIcon } from "@/components/discover/icons";
import { ComboLegItem } from "./ComboLegItem";
import { ComboTotalCard } from "./ComboTotalCard";
import { COMBO_DEMO, type ComboVM } from "./comboData";

/** Başlık sağındaki AI güven kalkanı (referans: shield + "AI GÜVENİ" + %). */
function ConfidenceShield({ value }: { value: string }) {
  return (
    <div className="flex items-center gap-2 rounded-xl border border-neon/20 bg-neon/[0.06] px-3 py-1.5">
      <ShieldIcon size={15} className="text-neon" />
      <div className="flex flex-col leading-none">
        <span className="text-[7.5px] font-bold uppercase tracking-wide text-text-muted">AI Güveni</span>
        <span className="text-[13px] font-extrabold leading-none tabular-nums text-neon">{value}</span>
      </div>
    </div>
  );
}

/**
 * FORMAX · Günün AI Kombini (09, new)
 * Referans Home bloğu: başlık + AI güven kalkanı → yatay kombin ayakları ("+" ile) + özet kart → CTA.
 * Section kalıbı Hero/AI Olası Sonuçlar ile aynı: GlassCard sectionGlow p-5 + SectionHeader.
 */
export function AIComboSection({ combo = COMBO_DEMO }: { combo?: ComboVM }) {
  return (
    <GlassCard sectionGlow className="p-4">
      <SectionHeader
        icon={<FlameIcon size={16} />}
        accent="amber"
        title="Günün AI Kombini"
        subtitle="FORMAX AI'nın bugünün en güçlü kombini"
        right={
          <div className="flex flex-col items-end gap-1">
            <ConfidenceShield value={combo.confidence} />
            {/* Maç sayısı — kombindeki ayak sayısından otomatik */}
            <span className="text-[9px] font-bold uppercase tracking-wide text-text-muted">
              {combo.legs.length} Maç
            </span>
          </div>
        }
      />

      {/* Kartlar sabit genişlikte; ekrana sığdırılmaz — yatay scroll. TOPLAM ORAN her zaman son. */}
      <div className="-mx-4 mt-1 flex items-stretch gap-2 overflow-x-auto px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {combo.legs.map((leg) => (
          <div key={leg.id} className="flex items-center gap-2">
            <ComboLegItem {...leg} />
            {/* Her maçtan sonra "+" — kartların dikey ortasında; son maçtan sonra TOPLAM ORAN'a bağlanır */}
            <span className="text-[15px] font-bold leading-none text-text-muted">+</span>
          </div>
        ))}
        <ComboTotalCard totalOdds={combo.totalOdds} stars={combo.stars} trend={combo.trend} />
      </div>

      <PrimaryCtaButton label="Kombini İncele" variant="ghost" className="mt-3.5" />
    </GlassCard>
  );
}
