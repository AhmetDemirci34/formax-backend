"use client";

import { use } from "react";
import { useRouter } from "next/navigation";
import { useMatchDecision } from "@/hooks/useMatchDecision";
import { useMatchDetail } from "@/hooks/useMatchDetail";
import { NarrativeBlocks, hasNarrativeContent } from "@/components/match-center/narrative/NarrativeBlocks";
import { MatchCenterHeader } from "@/components/match-center/MatchCenterHeader";
import { archivoNarrow } from "@/components/match-center/fonts";
import { readAiConfidence } from "@/components/match-center/aiContext";
import { LoadingState } from "@/components/ui/LoadingState";
import { ErrorState } from "@/components/ui/ErrorState";

interface PageProps {
  params: Promise<{ id: string }>;
}

/**
 * AI İncele — /match/[id]/ai
 *
 * AYRI AI DEĞİLDİR. Yeni analiz ÜRETMEZ, yeni endpoint ÇAĞIRMAZ.
 * ANLATININ TEK KAYNAĞI: Match Intelligence (Gemma) → ["match", matchId] →
 * /detail → aiNarrative. Maç Detay ile aynı cache olduğu için oradan
 * gelindiğinde İKİNCİ İSTEK ATILMAZ.
 *
 * Bu yüzey backend'in KENDİ "AI İncele" metnini (aiNarrative.aiIncele)
 * gösterir — Keşfet ve Maç Detayı metinlerinin kopyası değildir, backend
 * üçünü ayrı üretir. Frontend metni özetlemez, değiştirmez, yeniden yazmaz.
 *
 * KİLİTLİ KARAR (16.08): Eski Decision/MatchReadingEngine ANLATISI burada
 * gösterilmez. /decision çağrısı yalnız AI Güven endeksi için yapılır; o
 * endpoint ve backend kodu yerinde durur.
 */
export default function MatchAiReadPage({ params }: PageProps) {
  const { id } = use(params);
  const matchId = parseInt(id, 10);
  const router = useRouter();

  const { data: match, isLoading, isError, refetch } = useMatchDetail(matchId);
  // Decision'dan YALNIZ güven endeksi okunur — anlatı okunmaz.
  const { data: decision } = useMatchDecision(matchId);

  const confidence = readAiConfidence(decision?.confidence);

  const narrative = match?.aiNarrative ?? null;
  const aiIncele = narrative?.aiIncele?.trim() ?? "";
  const hasNarrative = hasNarrativeContent(narrative);

  const goBack = () => router.push(`/match/${matchId}`);

  return (
    <div className={`${archivoNarrow.variable} flex min-h-dvh flex-col bg-goalai-bg font-goalai`}>
      <div className="shrink-0 px-4 pt-3">
        <MatchCenterHeader onBack={goBack} />
      </div>

      <main className="min-h-0 flex-1 overflow-y-auto px-4 pb-10 pt-2">
        {isLoading && <LoadingState label="AI analizi yükleniyor..." />}

        {isError && (
          <ErrorState message="AI analizi şu an yüklenemedi." onRetry={() => refetch()} />
        )}

        {!isLoading && !isError && match && (
          <article className="mx-auto w-full max-w-[680px]">
            {/* Başlık — takım adları backend'den (canonical ev sahibi/deplasman) */}
            <header className="mb-5">
              <p className="mb-1.5 text-[11px] font-bold uppercase tracking-[0.2em] text-goalai-accent">
                AI İncele
              </p>
              <h1 className="text-[26px] font-bold leading-tight text-white">
                {match.homeTeam.name} — {match.awayTeam.name}
              </h1>
              {confidence && (
                <p className="mt-2 text-[13px] text-white/55">
                  AI Güveni{" "}
                  <span className="font-semibold tabular-nums text-white/85">
                    {confidence.score}
                  </span>{" "}
                  · {confidence.level}
                </p>
              )}
              {decision?.confidence?.basis && (
                <p className="mt-1.5 text-[12.5px] leading-[1.65] text-white/45">
                  {decision.confidence.basis}
                </p>
              )}
            </header>

            {/* Uzun okuma yerleşimi — yalnız Match Intelligence anlatısı */}
            {hasNarrative && narrative ? (
              <div className="[&_h3]:text-[13px] [&_li]:text-[15px] [&_li]:leading-[1.75] [&_p]:text-[15px] [&_p]:leading-[1.75]">
                {/* Backend'in bu yüzey için ürettiği metin — birebir gösterilir. */}
                {aiIncele && (
                  <p className="mb-4 text-[16px] leading-[1.8] text-white/85">{aiIncele}</p>
                )}
                <NarrativeBlocks narrative={narrative} />
              </div>
            ) : (
              <p className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[14px] text-white/55">
                Bu maç için AI analizi henüz üretilmedi.
              </p>
            )}
          </article>
        )}
      </main>
    </div>
  );
}
