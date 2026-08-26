"use client";

import { useMemo } from "react";
import { AnimatePresence, motion } from "framer-motion";
import type { PredictionTab, UiPrediction } from "@/types/predictions";
import { PredictionSection } from "./PredictionSection";
import { PredictionCard } from "./PredictionCard";
import { EmptyState } from "./EmptyState";

/**
 * PredictionList — aktif sekmeye göre bölümleri gruplar ve kartları map'ler.
 * Sekme değişiminde AnimatePresence ile yumuşak yer değiştirme; kartlar stagger
 * ile belirir. Filtrelenen liste boşsa EmptyState.
 */
export function PredictionList({
  predictions,
  activeTab,
}: {
  predictions: UiPrediction[];
  activeTab: PredictionTab;
}) {
  const groups = useMemo(() => {
    const live = predictions.filter((p) => p.status === "live");
    const pending = predictions.filter((p) => p.status === "upcoming" || p.status === "unknown");
    const finished = predictions.filter((p) => p.status === "finished");
    return { live, pending, finished };
  }, [predictions]);

  const sections =
    activeTab === "active"
      ? [{ key: "live", title: "Aktif Tahminlerim", tone: "active" as const, items: groups.live }]
      : activeTab === "pending"
      ? [{ key: "pending", title: "Bekleyen Tahminlerim", tone: "pending" as const, items: groups.pending }]
      : [
          { key: "live", title: "Aktif Tahminlerim", tone: "active" as const, items: groups.live },
          { key: "pending", title: "Bekleyen Tahminlerim", tone: "pending" as const, items: groups.pending },
          { key: "finished", title: "Tamamlanan Tahminlerim", tone: "finished" as const, items: groups.finished },
        ];

  const visible = sections.filter((s) => s.items.length > 0);

  return (
    <AnimatePresence mode="wait">
      <motion.div
        key={activeTab}
        initial={{ opacity: 0, x: 14 }}
        animate={{ opacity: 1, x: 0 }}
        exit={{ opacity: 0, x: -14 }}
        transition={{ duration: 0.22, ease: "easeOut" }}
        className="flex flex-col gap-6"
      >
        {visible.length === 0 ? (
          <EmptyState
            title="Bu kategoride tahmin yok"
            message={
              activeTab === "active"
                ? "Henüz aktif (canlı) tahminin bulunmuyor."
                : activeTab === "pending"
                ? "Bekleyen bir tahminin bulunmuyor."
                : "Henüz bir tahminin bulunmuyor."
            }
          />
        ) : (
          visible.map((s) => (
            <PredictionSection key={s.key} title={s.title} count={s.items.length} tone={s.tone}>
              {s.items.map((p, i) => (
                <motion.div
                  key={p.id}
                  initial={{ opacity: 0, y: 12 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.24, delay: 0.05 * i, ease: "easeOut" }}
                >
                  <PredictionCard p={p} />
                </motion.div>
              ))}
            </PredictionSection>
          ))
        )}
      </motion.div>
    </AnimatePresence>
  );
}
