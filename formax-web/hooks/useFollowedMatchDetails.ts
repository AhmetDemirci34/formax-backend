"use client";

// FORMAX · Takip edilen maçların TAM kaydı (Takip sayfası için).
//
// TEK KAYNAK: giriş yapılmışsa backend `/api/follows/me`; anonimken takip edilen id'ler
// (localStorage) mevcut maç listesiyle eşleştirilir. Yeni uç UYDURULMADI; anonim yolda
// gösterilen her alan yine backend'in `/api/matches` yanıtından gelir.

import { useMemo } from "react";
import { useAuth } from "@/context/AuthContext";
import { useFollowedIds, useFollowedMatches } from "@/hooks/useFollow";
import { isUpcomingMatch, onlyUpcoming } from "@/lib/matches/upcomingOnly";
import { useMatchList } from "@/hooks/useMatchList";
import type { FollowedMatchDto } from "@/types/api";

export function useFollowedMatchDetails(): {
  matches: FollowedMatchDto[];
  isLoading: boolean;
  /** Takip edilen ama listede karşılığı bulunamayan maç sayısı (ör. pencere dışı). */
  unresolved: number;
} {
  const { isLoggedIn } = useAuth();
  const { data: followedIds = [] } = useFollowedIds();
  const remote = useFollowedMatches();
  const list = useMatchList();

  return useMemo(() => {
    if (isLoggedIn) {
      // ÜRÜN KARARI: aktif Takip listesi yalnız BAŞLAMAMIŞ maçları gösterir. Takip kaydı
      // silinmez (arşiv/geçmiş korunur), yalnız başlamış maç aktif listede görünmez.
      return {
        matches: onlyUpcoming(remote.data ?? []),
        isLoading: remote.isLoading,
        unresolved: 0,
      };
    }

    const byId = new Map((list.data ?? []).map((m) => [m.matchId, m]));
    const matches: FollowedMatchDto[] = [];
    let unresolved = 0;

    for (const id of followedIds) {
      const m = byId.get(id);
      if (!m) {
        unresolved++;
        continue;
      }
      // Başlamış/bitmiş maç aktif takip listesinde GÖSTERİLMEZ; takip kaydı silinmez.
      if (!isUpcomingMatch(m)) continue;
      matches.push({
        matchId: m.matchId,
        homeTeam: m.homeTeam,
        awayTeam: m.awayTeam,
        homeTeamLogoUrl: m.homeTeamLogoUrl,
        awayTeamLogoUrl: m.awayTeamLogoUrl,
        league: m.league,
        startTime: m.startTime,
        status: m.status,
        minute: m.minute,
        score: m.score ?? undefined,
      });
    }

    matches.sort(
      (a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime()
    );
    return { matches, isLoading: list.isLoading, unresolved };
  }, [isLoggedIn, remote.data, remote.isLoading, list.data, list.isLoading, followedIds]);
}
