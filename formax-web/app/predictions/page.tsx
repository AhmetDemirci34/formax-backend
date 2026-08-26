"use client";

import { useState } from "react";
import { motion } from "framer-motion";
import { AppShell } from "@/components/layout/AppShell";
import { ErrorState } from "@/components/ui/ErrorState";
import { useUserPredictions } from "@/hooks/useUserPredictions";
import { PredictionHeader } from "@/components/predictions/PredictionHeader";
import { PredictionTabs } from "@/components/predictions/PredictionTabs";
import { PredictionList } from "@/components/predictions/PredictionList";
import { LoadingSkeleton } from "@/components/predictions/LoadingSkeleton";
import { FloatingCTA } from "@/components/predictions/FloatingCTA";
import type { PredictionTab } from "@/types/predictions";

/**
 * Tahminlerim (Predictions) — Radar sekmesinin yerini alan ana ekran.
 * Veri: useUserPredictions (localStorage `formax_predictions` + gerçek /detail).
 * State: activeTab (default 'active'). Global chrome (BottomNav) layout'tan gelir.
 */
export default function PredictionsPage() {
  const [activeTab, setActiveTab] = useState<PredictionTab>("active");
  const { predictions, counts, isLoading, error, refetch } = useUserPredictions();

  return (
    <>
      <AppShell header={<PredictionHeader counts={counts} />}>
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.4, ease: "easeOut" }}
          className="flex flex-col gap-4 pb-20"
        >
          <PredictionTabs active={activeTab} counts={counts} onChange={setActiveTab} />

          {isLoading ? (
            <LoadingSkeleton count={3} />
          ) : error ? (
            <ErrorState message={error} onRetry={refetch} />
          ) : (
            <PredictionList predictions={predictions} activeTab={activeTab} />
          )}
        </motion.div>
      </AppShell>

      <FloatingCTA />
    </>
  );
}
