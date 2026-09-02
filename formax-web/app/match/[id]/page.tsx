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
import { isUpcomingMatch, isFinishedMatch } from "@/lib/matches/upcomingOnly";
import { FinishedMatchSummary } from "@/components/match-center/views/FinishedMatchSummary";
import { MatchCenterHero } from "@/components/match-center/MatchCenterHero";
import { MatchCenterBottomNav } from "@/components/match-center/MatchCenterBottomNav";
import { AssistantDashboard } from "@/components/match-center/dashboard/AssistantDashboard";
import { AIAnalysisView } from "@/components/match-center/views/AIAnalysisView";
import { FormStatusView } from "@/components/match-center/views/FormStatusView";
import { LineupView } from "@/components/match-center/views/LineupView";
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

  // ── BİTMİŞ MAÇ: MAÇ ÖZETİ ────────────────────────────────────────────────
  // Aynı rota durum-duyarlıdır. Maç bittiğinde kullanıcı boş bir ekranla
  // karşılaşmaz; kesinleşmiş sonuç ve kaynaktaki olaylar gösterilir. Bu ekran
  // yalnız DB'den okunur — sağlayıcıya istek ÜRETMEZ ve canlı akış İÇERMEZ.
  if (isFinishedMatch(match.status)) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={() => router.back()} title="Maç Özeti" />
        <FinishedMatchSummary match={match} />
      </div>
    );
  }

  // KİLİTLİ ÜRÜN KARARI: FORMAX canlı maç GÖSTERMEZ. Henüz bitmemiş ama başlamış
  // (canlı) bir maç doğrudan URL ile açılırsa canlı skor/dakika/olay GÖSTERİLMEZ;
  // mevcut güvenli davranış korunur.
  if (!isUpcomingMatch({ status: match.status, startTime: match.matchDate })) {
    return (
      <div className={shell}>
        <MatchCenterHeader onBack={() => router.push("/maclar")} />
        <div className="flex flex-1 flex-col items-center justify-center gap-4 px-8 text-center">
          <p className="text-[14px] leading-relaxed text-white/70">
            Bu maç oynanıyor. FORMAX canlı yayın ve canlı skor göstermez; maç
            bittiğinde özet burada yayımlanır.
          </p>
          <button
            type="button"
            onClick={() => router.replace("/maclar")}
            className="rounded-[12px] border border-goalai-accent/30 bg-goalai-accent/10 px-4 py-2 text-[13px] font-bold text-goalai-accent"
          >
            Maçlara dön
          </button>
        </div>
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
