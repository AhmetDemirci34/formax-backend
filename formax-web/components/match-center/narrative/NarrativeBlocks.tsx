"use client";

import type { RadarNarrativeDto } from "@/types/api";
import { DecisionSection, LabeledLine, PointList } from "../decision/DecisionBlocks";

/**
 * FORMAX — Match Intelligence anlatısı (Gemma) render'ları.
 *
 * TEK KAYNAK: GET /api/matches/{id}/detail → aiNarrative.
 * Keşfet, Maç Detayı ve AI İncele AYNI bu veriyi okur; her yüzey kendi alanını
 * gösterir (radarSummary / matchReport / aiIncele). İkinci bir AI üretilmez.
 *
 * Bu dosya METİN ÜRETMEZ, cümle KURMAZ, sayı HESAPLAMAZ, market ADI TÜRETMEZ.
 * Yalnız backend'in yazdığı cümleleri sıralar. Boş alan = blok tamamen gizlenir
 * (placeholder yok). Blok kabuğu ve madde listesi mevcut Decision sunumundan
 * yeniden kullanılır → görsel dil değişmez.
 */

const filled = (s?: string | null): boolean => typeof s === "string" && s.trim().length > 0;
const cleanList = (arr?: string[] | null): string[] =>
  (arr ?? []).filter((s) => typeof s === "string" && s.trim().length > 0);

/** Anlatıda gösterilecek herhangi bir içerik var mı? */
export function hasNarrativeContent(n?: RadarNarrativeDto | null): boolean {
  if (!n) return false;
  return (
    filled(n.matchReport) ||
    filled(n.whyThisMatch) ||
    filled(n.reasoningSummary) ||
    filled(n.statisticalSummary) ||
    filled(n.newsSummary) ||
    filled(n.evidenceSummary) ||
    filled(n.socialSummary) ||
    cleanList(n.keyInsights).length > 0 ||
    (n.scenarios ?? []).some((s) => filled(s.market))
  );
}

/**
 * Maç Detayı yüzeyi — backend'in bu yüzey için ürettiği alanlar.
 * Sıra backend'in anlatı mantığını izler: maçın hikâyesi → neden izlenir →
 * değerlendirme → sahadaki tablo → gelişmeler → içgörüler → sonuç gerekçeleri.
 */
export function NarrativeBlocks({ narrative }: { narrative: RadarNarrativeDto }) {
  const n = narrative;
  const insights = cleanList(n.keyInsights);
  // Market adı backend'in canonical değeridir; burada YALNIZ gerekçesi olanlar
  // listelenir, ad ve sıra DEĞİŞTİRİLMEZ.
  const scenarios = (n.scenarios ?? []).filter((s) => filled(s.market) && filled(s.reason));

  const blocks: React.ReactNode[] = [];

  if (filled(n.matchReport) || filled(n.whyThisMatch)) {
    blocks.push(
      <DecisionSection key="story" title="Maçın Hikâyesi" index={blocks.length}>
        <div className="space-y-3">
          {filled(n.matchReport) && (
            <p className="text-[14px] leading-[1.62] text-white/85">{n.matchReport}</p>
          )}
          <LabeledLine label="Neden Bu Maç" value={n.whyThisMatch} />
        </div>
      </DecisionSection>
    );
  }

  if (filled(n.reasoningSummary) || filled(n.statisticalSummary)) {
    blocks.push(
      <DecisionSection key="reading" title="FORMAX Değerlendirmesi" index={blocks.length}>
        <div className="space-y-3">
          <LabeledLine label="Genel Okuma" value={n.reasoningSummary} />
          <LabeledLine label="Sahadaki Tablo" value={n.statisticalSummary} />
        </div>
      </DecisionSection>
    );
  }

  if (filled(n.newsSummary) || filled(n.evidenceSummary)) {
    blocks.push(
      <DecisionSection key="news" title="Gelişmeler" index={blocks.length}>
        <div className="space-y-3">
          <LabeledLine label="Son Durum" value={n.newsSummary} />
          <LabeledLine label="Maça Etkisi" value={n.evidenceSummary} />
        </div>
      </DecisionSection>
    );
  }

  if (filled(n.socialSummary)) {
    blocks.push(
      <DecisionSection key="social" title="Kullanıcı İlgisi" index={blocks.length}>
        <p className="text-[14px] leading-[1.62] text-white/85">{n.socialSummary}</p>
      </DecisionSection>
    );
  }

  if (insights.length > 0) {
    blocks.push(
      <DecisionSection key="insights" title="Öne Çıkanlar" index={blocks.length}>
        <PointList items={insights} />
      </DecisionSection>
    );
  }

  if (scenarios.length > 0) {
    blocks.push(
      <DecisionSection key="scenarios" title="Olası Sonuçların Gerekçesi" index={blocks.length}>
        <div className="space-y-3">
          {scenarios.map((s, i) => (
            // MARKET ADI BİREBİR: etiket stilindeki `uppercase` dönüşümü burada
            // KULLANILMAZ. Backend "Kasımpaşa Gol Atar" yazdıysa kullanıcı da tam
            // olarak onu görür — ad, sıra ve yazım backend'in canonical değeridir.
            <div
              key={`${s.market}-${i}`}
              className="border-t border-goalai-border/60 pt-3 first:border-0 first:pt-0"
            >
              <p className="mb-1 text-[12px] font-semibold text-white/70">{s.market}</p>
              <p className="text-[14px] leading-[1.62] text-white/85">{s.reason}</p>
            </div>
          ))}
        </div>
      </DecisionSection>
    );
  }

  if (blocks.length === 0) return null;
  return <div className="space-y-3">{blocks}</div>;
}
