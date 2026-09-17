"use client";

import { use, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { postSwipe } from "@/lib/api/swipe";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { useLineupAutoRefresh } from "@/hooks/useLineupAutoRefresh";
import type { ActiveView, MatchAction } from "@/components/match-center/aiContext";
import { MatchCenterScreen, type MatchCenterState } from "@/components/match-center/MatchCenterScreen";

interface PageProps {
  params: Promise<{ id: string }>;
}

/**
 * Maç Detay Merkezi (Velocity Pitch) — SPA. Tek `activeView` state'i ile modüler
 * conditional rendering (Teknik Doküman §3). Header + Hero tüm geçişlerde sabit;
 * modül panelleri slide-up ile gelir, 'X' ile dashboard'a döner. Gerçek veri:
 * `GET /api/matches/{id}/detail` (tek istek; görünüm `MatchCenterScreen`).
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

  const state: MatchCenterState = isLoading
    ? { kind: "loading" }
    : isError || !match
      ? { kind: "error" }
      : { kind: "ready", match };

  return (
    <MatchCenterScreen
      state={state}
      activeView={activeView}
      onSelect={(action: MatchAction) => setActiveView(action.view)}
      onCloseView={() => setActiveView("dashboard")}
      // Header geri: alt görünüm açıksa dashboard'a döner, dashboard'daysa route geri.
      onBack={() => (activeView !== "dashboard" ? setActiveView("dashboard") : router.back())}
      onRouteBack={() => router.back()}
      onRetry={() => refetch()}
      onGoMatches={(replace) => (replace ? router.replace("/maclar") : router.push("/maclar"))}
    />
  );
}
