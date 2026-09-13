"use client";

import { use, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AnimatePresence } from "framer-motion";
import { postSwipe } from "@/lib/api/swipe";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { useLineupAutoRefresh } from "@/hooks/useLineupAutoRefresh";
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
import { LineupPanel } from "@/components/match-center/lineup/LineupPanel";
import { NewsView } from "@/components/match-center/views/NewsView";

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
  // Kadro yokken (T−90…kickoff+10) açık ekran 30 sn'de bir backend'i (DB) yeniden okur;
  // kadro gelince durur. Sağlayıcıya istek atmaz.
  useLineupAutoRefresh(match, refetch);

  const [activeView, setActiveView] = useState<ActiveView>("dashboard");

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
    setActiveView(action.view);
  }

  const goDashboard = () => setActiveView("dashboard");

  // Header geri: alt görünüm açıksa dashboard'a döner, dashboard'daysa route geri.
  function handleBack() {
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
        <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-y-auto px-4 pb-28 pt-4">
          <div className="flex flex-col items-center gap-3 px-4 text-center">
            {/* "Bu maç oynanıyor" DENMEZ: sonuç alımı gecikmişse maç çoktan bitmiş
                olabilir. Saatten canlı durum ÜRETİLMEZ; yalnız doğru olan söylenir. */}
            <p className="text-[14px] leading-relaxed text-white/85">
              Bu maçın başlama saati geçti. FORMAX canlı yayın ve canlı skor göstermez;
              sonuç kesinleştiğinde maç özeti burada yayımlanır.
            </p>
            <button
              type="button"
              onClick={() => router.replace("/maclar")}
              className="rounded-[12px] border border-goalai-accent/30 bg-goalai-accent/10 px-4 py-2 text-[13px] font-bold text-goalai-accent"
            >
              Maçlara dön
            </button>
          </div>

          {/* KADRO — maç başlamış olsa bile DB'deki doğrulanmış kadro kaybolmaz; yoksa
              "doğrulanmış kadro bulunamadı" açıkça yazılır. Sağlayıcıya istek YOK. */}
          <section className="w-full rounded-2xl border border-goalai-border bg-goalai-surface-bright/40 p-3">
            <h2 className="mb-2 text-[12px] font-bold uppercase tracking-wide text-white/85">Kadrolar</h2>
            <LineupPanel match={match} />
          </section>
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
    </div>
  );
}
