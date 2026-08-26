"use client";

import { useMemo } from "react";
import { useQuery, useQueries } from "@tanstack/react-query";
import { getMatchDetail } from "@/lib/api/matches";
import { predictionRepository } from "@/lib/predictions";
import type { UiPrediction, UiPredictionStatus, PredictionCounts } from "@/types/predictions";

/**
 * useUserPredictions — "Tahminlerim" veri katmanı (Clean Architecture).
 *
 * Kaynak SOYUTLANMIŞTIR: hook localStorage bilmez, yalnızca `predictionRepository`
 * üzerinden genel `Prediction` modelini çeker. Kaynak bugün localStorage, yarın
 * backend olabilir — bu hook ve UI değişmez.
 *
 * Her tahmin gerçek `GET /api/matches/{id}/detail` ile canlı durum/dakika/skor/lig/
 * logo için zenginleştirilir (kaynak snapshot'ı fallback'tir). Mock/fake yok.
 */
function toUiStatus(status?: string): UiPredictionStatus {
  if (!status) return "unknown";
  if (status === "Live") return "live";
  if (["Finished", "FullTime", "AfterExtraTime", "AfterPenalties"].includes(status)) return "finished";
  return "upcoming";
}

export function useUserPredictions() {
  // 1) Kaynak-agnostik tahmin listesi (Repository → DataSource).
  const {
    data: source = [],
    isLoading: loadingList,
    isError: listError,
    refetch: refetchList,
  } = useQuery({
    queryKey: ["predictions"],
    queryFn: () => predictionRepository.getAll(),
    staleTime: 10_000,
  });

  // 2) Her tahmin için gerçek maç detayı ile zenginleştirme.
  const details = useQueries({
    queries: source.map((p) => ({
      queryKey: ["match", p.matchId],
      queryFn: () => getMatchDetail(p.matchId),
      staleTime: 30_000,
    })),
  });

  const detailsSignal = details.map((d) => d.dataUpdatedAt).join(",");

  const predictions = useMemo<UiPrediction[]>(
    () =>
      source.map((p, i) => {
        const d = details[i]?.data;
        const stats = d?.live?.stats;
        return {
          id: p.predictionId,
          matchId: p.matchId,
          home: { name: d?.homeTeam?.name || p.homeTeam?.name || "", logoUrl: d?.homeTeam?.logoUrl ?? p.homeTeam?.logoUrl },
          away: { name: d?.awayTeam?.name || p.awayTeam?.name || "", logoUrl: d?.awayTeam?.logoUrl ?? p.awayTeam?.logoUrl },
          league: d?.league || p.league || "",
          market: p.market,
          selection: p.selection,
          odds: p.odds,
          status: toUiStatus(d?.status),
          minute: stats?.minute ?? null,
          score: stats ? `${stats.homeScore}-${stats.awayScore}` : null,
          kickoff: d?.matchDate ?? null,
          createdAt: p.createdAt,
        };
      }),
    // `details`/`source` referansları renderda değişebilir; detailsSignal veri değişimini temsil eder.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [source, detailsSignal]
  );

  const counts = useMemo<PredictionCounts>(() => {
    let active = 0;
    let pending = 0;
    for (const p of predictions) {
      if (p.status === "live") active += 1;
      else if (p.status === "upcoming" || p.status === "unknown") pending += 1;
    }
    return { active, pending, total: predictions.length };
  }, [predictions]);

  const isLoading = loadingList || (source.length > 0 && details.some((d) => d.isLoading));
  const error =
    listError || (source.length > 0 && details.length > 0 && details.every((d) => d.isError))
      ? "Tahminler şu an yüklenemiyor."
      : null;

  const refetch = () => refetchList();

  return { predictions, counts, isLoading, error, refetch };
}
