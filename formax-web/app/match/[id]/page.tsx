"use client";

import { use, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AnimatePresence } from "framer-motion";
import { postSwipe } from "@/lib/api/swipe";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { archivoNarrow } from "@/components/match-center/fonts";
import type { ActiveView, MatchAction } from "@/components/match-center/aiContext";
import { MatchCenterHeader } from "@/components/match-center/MatchCenterHeader";
import { MatchCenterHero } from "@/components/match-center/MatchCenterHero";
import { MatchCenterBottomNav } from "@/components/match-center/MatchCenterBottomNav";
import { AssistantDashboard } from "@/components/match-center/dashboard/AssistantDashboard";
import { AIAnalysisView } from "@/components/match-center/views/AIAnalysisView";
import { FormStatusView } from "@/components/match-center/views/FormStatusView";
import { LineupView } from "@/components/match-center/views/LineupView";
import { LiveView } from "@/components/match-center/views/LiveView";
import { NewsView } from "@/components/match-center/views/NewsView";
import { HighlightsOverlay } from "@/components/match-center/overlays/HighlightsOverlay";

interface PageProps {
  params: Promise<{ id: string }>;
}

/**
 * Maç Detay Merkezi (Velocity Pitch) — SPA. Tek `activeView` state'i ile modüler
 * conditional rendering (Teknik Doküman §3). Header + Hero tüm geçişlerde sabit;
 * modül panelleri slide-up ile gelir, 'X' ile dashboard'a döner. Gerçek veri:
 * `GET /api/matches/{id}/detail`.
 */
export default function MatchCenterPage({ params }: PageProps) {
  const { id } = use(params);
  const matchId = parseInt(id, 10);
  const router = useRouter();
  const { data: match, isLoading, isError, refetch } = useMatchDetail(matchId);

  const [activeView, setActiveView] = useState<ActiveView>("dashboard");
  const [showHighlights, setShowHighlights] = useState(false);

  // Ekranda geçirilen süre analitiği (mevcut swipe endpoint'i).
  const openTimeRef = useRef(0);
  useEffect(() => {
    openTimeRef.current = Date.now();
    return () => {
      postSwipe({
        matchId,
        action: "detail_return",
        detailDurationMs: Date.now() - openTimeRef.current,
      }).catch(() => {});
    };
  }, [matchId]);

  function handleSelect(action: MatchAction) {
    if (action.slot === "video") {
      setShowHighlights(true);
    } else if (action.view) {
      setActiveView(action.view);
    }
  }

  const goDashboard = () => setActiveView("dashboard");

  // Header geri: overlay/alt görünüm açıksa dashboard'a döner, dashboard'daysa route geri.
  function handleBack() {
    if (showHighlights) {
      setShowHighlights(false);
      return;
    }
    if (activeView !== "dashboard") {
      setActiveView("dashboard");
      return;
    }
    router.back();
  }

  const shell = `relative flex h-[100dvh] flex-col overflow-hidden bg-goalai-surface text-white ${archivoNarrow.variable} ${archivoNarrow.className}`;

  if (isLoading) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={() => router.back()} />
        <LoadingState label="Maç merkezi hazırlanıyor..." />
      </div>
    );
  }

  if (isError || !match) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={() => router.back()} />
        <ErrorState
          message="Maç bilgileri şu an yüklenemiyor. Lütfen tekrar dene."
          onRetry={() => refetch()}
        />
      </div>
    );
  }

  return (
    <div className={shell}>
      {/* Sabit: Header + Hero */}
      <MatchCenterHeader onBack={handleBack} />
      <MatchCenterHero match={match} />

      {/* Dinamik içerik alanı */}
      <div className="relative min-h-0 flex-1 px-4 py-4">
        <AnimatePresence mode="wait">
          {activeView === "dashboard" && (
            <AssistantDashboard key="dashboard" onSelect={handleSelect} />
          )}
          {activeView === "analysis" && (
            <AIAnalysisView key="analysis" match={match} onClose={goDashboard} />
          )}
          {activeView === "stats" && (
            <FormStatusView key="stats" match={match} onClose={goDashboard} />
          )}
          {activeView === "lineup" && (
            <LineupView key="lineup" match={match} onClose={goDashboard} />
          )}
          {activeView === "live" && (
            <LiveView key="live" match={match} onClose={goDashboard} />
          )}
          {activeView === "news" && (
            <NewsView key="news" match={match} onClose={goDashboard} />
          )}
        </AnimatePresence>
      </div>

      {/* Sabit: Bottom Nav */}
      <MatchCenterBottomNav />

      {/* Önemli Anlar paneli (z-40; header/nav z-50 üstte kalır) */}
      <AnimatePresence>
        {showHighlights && (
          <HighlightsOverlay
            key="highlights-overlay"
            match={match}
            onClose={() => setShowHighlights(false)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
