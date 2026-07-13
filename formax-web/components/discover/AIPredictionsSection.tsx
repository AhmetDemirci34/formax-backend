"use client";

import { motion } from "framer-motion";
import { GlassCard } from "@/components/ui/GlassCard";
import { SectionHeader } from "@/components/ui/SectionHeader";
import { LiveUpdateBadge } from "@/components/ui/LiveUpdateBadge";
import { PredictionCard } from "@/components/discover/PredictionCard";
import { PrimaryCtaButton } from "@/components/ui/PrimaryCtaButton";
import { SparklesIcon, InfoIcon } from "@/components/discover/icons";
import { MATCHES, type HeroPredictionVM } from "@/components/discover/hero/heroData";

interface Props {
  /** Aktif maçın AI Olası Sonuçları (swipe ile değişir). */
  predictions?: HeroPredictionVM[];
  /** Fade tetiklemek için aktif maç anahtarı. */
  matchKey?: string;
}

export function AIPredictionsSection({
  predictions = MATCHES[0].predictions,
  matchKey = "static",
}: Props) {
  return (
    <GlassCard sectionGlow className="p-5">
      <SectionHeader
        icon={<SparklesIcon size={16} />}
        accent="purple"
        title="AI Olası Sonuçlar"
        titleAfter={<InfoIcon size={13} className="text-text-muted" />}
        right={<LiveUpdateBadge label="Son güncelleme: 2 dk önce" />}
      />
      <motion.div
        key={matchKey}
        initial={{ opacity: 0, y: 6 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.28, ease: "easeOut" }}
        className="mt-1 flex gap-2"
      >
        {predictions.map((p) => (
          <PredictionCard key={p.code} {...p} />
        ))}
      </motion.div>
      <PrimaryCtaButton label="Maçı Keşfet" className="mt-5" />
    </GlassCard>
  );
}
