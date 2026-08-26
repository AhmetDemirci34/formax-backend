"use client";

import { motion } from "framer-motion";
import type { MatchDecisionDto, MatchReadingDto, ReadingTeamDto } from "@/types/decision";

/**
 * FORMAX — Decision blok render'ları.
 *
 * ⚠️ KİLİTLİ KARAR (16.08): `DecisionBlocks` ARTIK HİÇBİR EKRANDA
 * KULLANILMIYOR. Eski Decision/MatchReadingEngine anlatısı kullanıcıya
 * gösterilmez; üç analiz yüzeyinin (Keşfet / Maç Detayı / AI İncele) tek
 * anlatı kaynağı Match Intelligence'tır (Gemma → /detail → aiNarrative,
 * bkz. ../narrative/NarrativeBlocks). Gerekçe: bu katman maçta yer almayan
 * takımdan söz eden, tekrar eden ve bozuk ekli cümleler üretiyordu.
 *
 * Dosya BİLEREK duruyor: aşağıdaki sunum kabukları (DecisionSection,
 * PointList, LabeledLine) anlatı bloklarınca yeniden kullanılır ve /decision
 * endpoint'i sayısal karar verisi (market/olasılık/güven) için canlıdır.
 *
 * TEK KAYNAK: decision.reading. Bu dosya METİN ÜRETMEZ, cümle KURMAZ,
 * skor HESAPLAMAZ. Yalnız backend'in ürettiği dizileri/cümleleri sıralar.
 * Boş blok TAMAMEN gizlenir — placeholder yoktur.
 */

// ── Yardımcılar: yalnız "dolu mu" kontrolü. İçerik dönüştürülmez. ──────────
const clean = (arr?: string[] | null): string[] =>
  (arr ?? []).filter((s) => typeof s === "string" && s.trim().length > 0);

const hasTeam = (t?: ReadingTeamDto | null): boolean =>
  !!t && clean(t.points).length > 0;

/** Bir bloğun (dizilerin birleşimi) gösterilecek içeriği var mı. */
export function blockHasContent(...groups: (string[] | null | undefined)[]): boolean {
  return groups.some((g) => clean(g).length > 0);
}

// ── Blok kabuğu ────────────────────────────────────────────────────────────
export function DecisionSection({
  title,
  index = 0,
  children,
}: {
  title: string;
  index?: number;
  children: React.ReactNode;
}) {
  return (
    <motion.section
      initial={{ opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.28, delay: Math.min(index, 8) * 0.04, ease: "easeOut" }}
      className="rounded-2xl border border-goalai-border bg-goalai-surface-bright p-4"
    >
      <h3 className="mb-3 text-xs font-semibold uppercase tracking-[0.16em] text-goalai-accent">
        {title}
      </h3>
      {children}
    </motion.section>
  );
}

/** Madde listesi — backend cümleleri olduğu gibi. */
export function PointList({ items }: { items: string[] }) {
  const list = clean(items);
  if (list.length === 0) return null;

  return (
    <ul className="space-y-2.5">
      {list.map((line, i) => (
        <li key={i} className="flex gap-2.5 text-[14px] leading-[1.62] text-white/85">
          <span aria-hidden className="mt-[9px] h-1 w-1 shrink-0 rounded-full bg-goalai-accent/70" />
          <span>{line}</span>
        </li>
      ))}
    </ul>
  );
}

/** Etiketli tek cümle — boşsa hiç render edilmez. */
export function LabeledLine({ label, value }: { label: string; value?: string | null }) {
  if (!value || !value.trim()) return null;
  return (
    <div className="border-t border-goalai-border/60 pt-3 first:border-0 first:pt-0">
      <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.12em] text-white/45">
        {label}
      </p>
      <p className="text-[14px] leading-[1.62] text-white/85">{value}</p>
    </div>
  );
}

/** Takım okuması — takım adı backend'den gelir. */
function TeamRead({ team }: { team: ReadingTeamDto }) {
  return (
    <div className="border-t border-goalai-border/60 pt-3 first:border-0 first:pt-0">
      <p className="mb-2 text-[13px] font-bold text-white">{team.teamName}</p>
      <PointList items={team.points} />
    </div>
  );
}

// ── 9 BLOK ─────────────────────────────────────────────────────────────────
// Sıra kilitli karara göre: Genel → Takım → Oyuncu → Haber → Fikstür →
// Psikoloji → Kritik Faktörler → Hidden Insight → FORMAX Yorumu.

export function DecisionBlocks({ decision }: { decision: MatchDecisionDto }) {
  const r = decision.reading;
  if (!r) return null;

  const blocks: React.ReactNode[] = [];
  const push = (node: React.ReactNode) => blocks.push(node);

  // 1 — Genel AI Analizi
  if (blockHasContent(r.context, r.matchStoryLines, r.synthesis) || r.mainStory || r.subStory) {
    push(
      <DecisionSection key="genel" title="Genel AI Analizi" index={blocks.length}>
        <div className="space-y-3">
          <LabeledLine label="Ana Hikâye" value={r.mainStory} />
          <LabeledLine label="Yan Hikâye" value={r.subStory} />
          <PointList items={[...clean(r.context), ...clean(r.matchStoryLines), ...clean(r.synthesis)]} />
        </div>
      </DecisionSection>
    );
  }

  // 2 — Takım Analizi
  if (hasTeam(r.homeTeam) || hasTeam(r.awayTeam) || blockHasContent(r.teamStory, r.tactical, r.tacticalStory)) {
    push(
      <DecisionSection key="takim" title="Takım Analizi" index={blocks.length}>
        <div className="space-y-3">
          {hasTeam(r.homeTeam) && <TeamRead team={r.homeTeam!} />}
          {hasTeam(r.awayTeam) && <TeamRead team={r.awayTeam!} />}
          <PointList items={[...clean(r.teamStory), ...clean(r.tactical), ...clean(r.tacticalStory)]} />
        </div>
      </DecisionSection>
    );
  }

  // 3 — Oyuncu Analizi
  if (blockHasContent(r.keyPlayers, r.playerStory, r.squad, r.squadStory)) {
    push(
      <DecisionSection key="oyuncu" title="Oyuncu Analizi" index={blocks.length}>
        <PointList
          items={[...clean(r.keyPlayers), ...clean(r.playerStory), ...clean(r.squad), ...clean(r.squadStory)]}
        />
      </DecisionSection>
    );
  }

  // 4 — Haber Etkileri
  if (blockHasContent(r.news, r.newsStory)) {
    push(
      <DecisionSection key="haber" title="Haber Etkileri" index={blocks.length}>
        <PointList items={[...clean(r.news), ...clean(r.newsStory)]} />
      </DecisionSection>
    );
  }

  // 5 — Fikstür Etkileri
  if (blockHasContent(r.fixture, r.timelineStory, r.competitionStory, r.transfers, r.coach)) {
    push(
      <DecisionSection key="fikstur" title="Fikstür Etkileri" index={blocks.length}>
        <PointList
          items={[
            ...clean(r.fixture),
            ...clean(r.timelineStory),
            ...clean(r.competitionStory),
            ...clean(r.transfers),
            ...clean(r.coach),
          ]}
        />
      </DecisionSection>
    );
  }

  // 6 — Psikoloji
  if (blockHasContent(r.psychology, r.psychologyStory)) {
    push(
      <DecisionSection key="psikoloji" title="Psikoloji" index={blocks.length}>
        <PointList items={[...clean(r.psychology), ...clean(r.psychologyStory)]} />
      </DecisionSection>
    );
  }

  // 7 — Kritik Faktörler (kök seviye + reading dönüm noktası)
  if (
    blockHasContent(decision.criticalFactors, decision.decisionDrivers, decision.unknownFactors) ||
    r.turningPoint
  ) {
    push(
      <DecisionSection key="kritik" title="Kritik Faktörler" index={blocks.length}>
        <div className="space-y-3">
          <LabeledLine label="Dönüm Noktası" value={r.turningPoint} />
          <PointList items={[...clean(decision.criticalFactors), ...clean(decision.decisionDrivers)]} />
          {clean(decision.unknownFactors).length > 0 && (
            <div className="border-t border-goalai-border/60 pt-3">
              <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.12em] text-white/45">
                Veri Bulunmayan Alanlar
              </p>
              <PointList items={decision.unknownFactors} />
            </div>
          )}
        </div>
      </DecisionSection>
    );
  }

  // 8 — Hidden Insight
  if (blockHasContent(r.hidden, r.hiddenStory) || r.surprisePotential) {
    push(
      <DecisionSection key="hidden" title="Hidden Insight" index={blocks.length}>
        <div className="space-y-3">
          <PointList items={[...clean(r.hidden), ...clean(r.hiddenStory)]} />
          <LabeledLine label="Sürpriz Potansiyeli" value={r.surprisePotential} />
        </div>
      </DecisionSection>
    );
  }

  // 9 — FORMAX Yorumu
  const v = r.verdict;
  const hasVerdict =
    !!v && !!(v.criticalTopic || v.whyWatch || v.biggestAdvantage || v.biggestRisk || v.whatCouldChange);
  if (hasVerdict || r.formaxView || blockHasContent(r.formaxOpinion) || r.biggestAdvantage || r.biggestRisk) {
    push(
      <DecisionSection key="formax" title="FORMAX Yorumu" index={blocks.length}>
        <div className="space-y-3">
          <LabeledLine label="FORMAX Görüşü" value={r.formaxView} />
          <PointList items={r.formaxOpinion} />
          <LabeledLine label="Kritik Konu" value={v?.criticalTopic} />
          <LabeledLine label="Neden İzlemeli" value={v?.whyWatch} />
          <LabeledLine label="En Büyük Avantaj" value={v?.biggestAdvantage ?? r.biggestAdvantage} />
          <LabeledLine label="En Büyük Risk" value={v?.biggestRisk ?? r.biggestRisk} />
          <LabeledLine label="Neyi Değiştirebilir" value={v?.whatCouldChange} />
        </div>
      </DecisionSection>
    );
  }

  if (blocks.length === 0) return null;

  return <div className="space-y-3">{blocks}</div>;
}

/** Decision paketinde gösterilecek herhangi bir blok var mı. */
export function hasAnyDecisionContent(decision?: MatchDecisionDto | null): boolean {
  if (!decision?.reading) return false;
  const r = decision.reading;
  return (
    blockHasContent(
      r.context, r.matchStoryLines, r.synthesis, r.teamStory, r.tactical, r.tacticalStory,
      r.keyPlayers, r.playerStory, r.squad, r.squadStory, r.news, r.newsStory,
      r.fixture, r.timelineStory, r.competitionStory, r.transfers, r.coach,
      r.psychology, r.psychologyStory, r.hidden, r.hiddenStory, r.formaxOpinion,
      decision.criticalFactors, decision.decisionDrivers, decision.unknownFactors
    ) ||
    hasTeam(r.homeTeam) ||
    hasTeam(r.awayTeam) ||
    !!(r.mainStory || r.subStory || r.turningPoint || r.surprisePotential || r.formaxView) ||
    !!(r.verdict && (r.verdict.criticalTopic || r.verdict.whyWatch))
  );
}
