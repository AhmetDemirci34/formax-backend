"use client";

// FORMAX · SONUÇLAR sekmesinin LİSTE gövdesi.
//
// KİLİTLİ KAPSAM: kilitli 11 organizasyonun BİTMİŞ maçları, Türkiye takvim günü
// bazında, bugün + önceki 7 gün. Karar backend'de verilir; bu ekran gelen listeyi
// süzmez, sıralamaz ve durum ÜRETMEZ.
//
// Tarih seçici burada DEĞİL, sayfanın sticky başlığındadır (bkz. app/maclar/page.tsx);
// gün durumu useResultDaySelection kancasında tek sahiptedir.

import { ResultCard } from "./ResultCard";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";
import { useMatchResults } from "@/hooks/useMatchResults";

interface Props {
  day: string | null;
  /** Pencerenin tamamında sonuç var mı? Boş durum cümlesini bu belirler. */
  windowHasAnyResult: boolean;
  onOpen: (matchId: number) => void;
}

export function ResultsView({ day, windowHasAnyResult, onOpen }: Props) {
  const resultsQuery = useMatchResults(day);
  const results = resultsQuery.data ?? [];

  return (
    <main className="flex flex-col gap-2.5 px-3.5 pb-[calc(var(--bottom-nav-height)+16px)] pt-2">
      {day === null || resultsQuery.isLoading ? (
        <LoadingState label="Sonuçlar yükleniyor..." />
      ) : resultsQuery.isError ? (
        <ErrorState
          message="Sonuçlar şu an yüklenemiyor. Lütfen tekrar dene."
          onRetry={() => resultsQuery.refetch()}
        />
      ) : results.length === 0 ? (
        // BOŞ DURUM HATA DEĞİLDİR: sakin bir bilgi cümlesi gösterilir.
        <p className="pt-16 text-center text-[13px] leading-relaxed text-text-muted">
          {windowHasAnyResult
            ? "Bu tarihte tamamlanmış maç bulunmuyor."
            : "Son 7 gün içinde tamamlanmış maç bulunmuyor."}
        </p>
      ) : (
        results.map((r) => <ResultCard key={r.matchId} result={r} onOpen={onOpen} />)
      )}
    </main>
  );
}
