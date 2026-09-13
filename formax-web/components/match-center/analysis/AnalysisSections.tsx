"use client";

import type { MatchAnalysisDto } from "@/types/api";
import { DecisionSection, PointList } from "../decision/DecisionBlocks";

/**
 * AI MAÇ ANALİZİ — backend'in arka planda KANITTAN ürettiği ve doğruladığı analiz.
 *
 * VERİ: /api/matches/{id}/detail → analysis (DB'deki hazır kayıt). Bu bileşen:
 *  • metin ÜRETMEZ, sayı HESAPLAMAZ, cümle birleştirmez;
 *  • LLM ÇAĞIRMAZ (sayfa açılışı hiçbir üretim tetiklemez);
 *  • boş bölümü hiç göstermez (placeholder/uydurma yok);
 *  • teknik form satırı (O/G/B/M/AG/YG/AV) GÖSTERMEZ.
 * Bölüm sırası kilitli: Neden izlemeli → Maçın kilidi → Kadro etkisi → Belirsizlik.
 * Olası senaryoların gerekçesi Olası Sonuçlar satırlarında gösterilir (tekrar yok).
 */

export const ANALYSIS_PREPARING_TEXT = "Analiz hazırlanıyor.";
export const ANALYSIS_INSUFFICIENT_TEXT =
  "Bu maç için analiz üretecek kadar doğrulanmış veri henüz yok.";
export const ANALYSIS_UNAVAILABLE_TEXT = "Analiz şu an gösterilemiyor.";

const filled = (arr?: string[] | null) => (arr ?? []).filter((s) => typeof s === "string" && s.trim().length > 0);

/** Gösterilecek içerik var mı? */
export function analysisHasContent(a?: MatchAnalysisDto | null): boolean {
  if (!a || a.status !== "Ready") return false;
  return (
    filled(a.whyWatch).length > 0 ||
    filled(a.keyBattle).length > 0 ||
    filled(a.lineupImpact).length > 0 ||
    !!a.uncertainty?.trim()
  );
}

/** Hazır değilse kullanıcıya gösterilecek tek cümle; hazırsa null. */
export function analysisStatusText(a?: MatchAnalysisDto | null): string | null {
  if (!a || a.status === "Preparing") return ANALYSIS_PREPARING_TEXT;
  if (a.status === "InsufficientData") return ANALYSIS_INSUFFICIENT_TEXT;
  if (a.status === "Ready") return analysisHasContent(a) ? null : ANALYSIS_INSUFFICIENT_TEXT;
  return ANALYSIS_UNAVAILABLE_TEXT;
}

export function AnalysisSections({ analysis }: { analysis?: MatchAnalysisDto | null }) {
  const status = analysisStatusText(analysis);
  if (status) {
    return (
      <div
        className="rounded-2xl border border-goalai-border bg-goalai-surface-bright px-4 py-3 text-[13px] text-white/70"
        data-analysis-state={analysis?.status ?? "Preparing"}
      >
        {status}
      </div>
    );
  }

  const a = analysis!;
  const blocks: React.ReactNode[] = [];
  const why = filled(a.whyWatch).slice(0, 3);
  const key = filled(a.keyBattle);
  const lineup = filled(a.lineupImpact);

  if (why.length > 0)
    blocks.push(
      <DecisionSection key="why" title="Bu Maçı Neden İzlemeli?" index={blocks.length}>
        <PointList items={why} />
      </DecisionSection>
    );
  if (key.length > 0)
    blocks.push(
      <DecisionSection key="key" title="Maçın Kilidi" index={blocks.length}>
        <PointList items={key} />
      </DecisionSection>
    );
  if (lineup.length > 0)
    blocks.push(
      <DecisionSection key="lineup" title="Kadro Etkisi" index={blocks.length}>
        <PointList items={lineup} />
      </DecisionSection>
    );
  if (a.uncertainty?.trim())
    blocks.push(
      <DecisionSection key="uncertainty" title="Belirsizlik" index={blocks.length}>
        <p className="text-[13.5px] leading-[1.6] text-formax-amber/85">{a.uncertainty}</p>
      </DecisionSection>
    );

  return (
    <div className="space-y-3" data-analysis-state="Ready">
      {blocks}
    </div>
  );
}

/** Olası sonuç satırının gerekçesi — yalnız backend market adıyla birebir eşleşen kayıt. */
export function ScenarioReasonLines({
  analysis,
  market,
}: {
  analysis?: MatchAnalysisDto | null;
  market: string;
}) {
  if (!analysis || analysis.status !== "Ready") return null;
  const r = (analysis.scenarios ?? []).find((s) => s.market === market);
  if (!r || (!r.support?.trim() && !r.risk?.trim())) return null;
  return (
    <div className="mt-1.5 space-y-1 text-[12px] leading-snug" data-scenario-reason={market}>
      {r.support?.trim() && (
        <p className="text-white/75">
          <span className="font-semibold text-formax-green/85">Destekleyen veri: </span>
          {r.support}
        </p>
      )}
      {r.risk?.trim() && (
        <p className="text-white/65">
          <span className="font-semibold text-formax-amber/85">Zayıflatan risk: </span>
          {r.risk}
        </p>
      )}
    </div>
  );
}
