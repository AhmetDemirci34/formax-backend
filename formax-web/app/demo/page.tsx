"use client";

// ─────────────────────────────────────────────────────────────────────────────
// /demo — FORMAX Screen Preview
// Renders all screen states with mock data (no API needed)
// ─────────────────────────────────────────────────────────────────────────────

import { useState, useRef, useCallback } from "react";
import { SwipeCardStack } from "@/components/feed/SwipeCardStack";
import { MatchCard } from "@/components/feed/MatchCard";
import { MatchHeader } from "@/components/match/MatchHeader";
import { AiBlock } from "@/components/match/AiBlock";
import { SapmaBlock } from "@/components/match/SapmaBlock";
import { InsightBlock, ProbabilitiesBlock, MarketIntelligenceBlock } from "@/components/match/InsightBlock";
import { TacticalMatchupBlock } from "@/components/match/TacticalMatchup";
import { FormSection } from "@/components/match/FormSection";
import { H2HSection } from "@/components/match/H2HSection";
import { LineupSection, PlayerStatusSection } from "@/components/match/LineupSection";
import { StandingsSection } from "@/components/match/StandingsSection";
import { LiveStatsPanel } from "@/components/live/LiveStatsPanel";
import { LiveTimeline } from "@/components/live/LiveTimeline";
import { MomentumChart } from "@/components/live/MomentumChart";
import {
  MOCK_FEED,
  MOCK_MATCH_SCHEDULED,
  MOCK_MATCH_LIVE,
  MOCK_MATCH_FINISHED,
} from "@/lib/mockData";

// ── Section wrapper ────────────────────────────────────────────────────────────

function Section({
  id,
  label,
  badge,
  badgeColor = "bg-accent",
  children,
}: {
  id: string;
  label: string;
  badge: string;
  badgeColor?: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="mb-16">
      <div className="flex items-center gap-3 mb-5">
        <span className={`text-xs font-bold px-2.5 py-1 rounded-full text-white ${badgeColor}`}>
          {badge}
        </span>
        <h2 className="text-lg font-bold text-text-primary">{label}</h2>
      </div>
      {children}
    </section>
  );
}

// ── Phone frame ───────────────────────────────────────────────────────────────

function PhoneFrame({ children }: { children: React.ReactNode }) {
  return (
    <div className="mx-auto max-w-sm w-full bg-bg-base rounded-[2rem] border-2 border-border shadow-2xl overflow-hidden">
      {/* Notch bar */}
      <div className="h-7 bg-bg-card flex items-center justify-center">
        <div className="w-20 h-1.5 rounded-full bg-bg-elevated" />
      </div>
      <div className="overflow-y-auto max-h-[700px]">
        {children}
      </div>
      {/* Home bar */}
      <div className="h-6 bg-bg-card flex items-center justify-center">
        <div className="w-24 h-1 rounded-full bg-bg-elevated" />
      </div>
    </div>
  );
}

// ── Sticky nav ────────────────────────────────────────────────────────────────

const NAV_ITEMS = [
  { id: "swipe-feed",  label: "Swipe Feed"  },
  { id: "home-feed",   label: "Kart Liste"  },
  { id: "scheduled",   label: "Preview"     },
  { id: "live",        label: "Canlı"       },
  { id: "finished",    label: "Bitti"       },
];

function StickyNav() {
  return (
    <nav className="sticky top-0 z-50 bg-bg-base/95 backdrop-blur border-b border-border-dim">
      <div className="max-w-4xl mx-auto px-6 py-3 flex items-center gap-4 overflow-x-auto">
        <span className="text-accent font-black text-sm tracking-tight shrink-0">FORMAX</span>
        <span className="text-border h-4 border-r border-border-dim" />
        <span className="text-text-muted text-xs shrink-0">Screen Preview</span>
        <div className="flex gap-1 ml-auto">
          {NAV_ITEMS.map((item) => (
            <a
              key={item.id}
              href={`#${item.id}`}
              className="text-xs font-medium text-text-muted hover:text-text-primary px-3 py-1.5 rounded-lg hover:bg-bg-elevated transition-colors shrink-0"
            >
              {item.label}
            </a>
          ))}
        </div>
      </div>
    </nav>
  );
}

// ── Match detail inner (reusable) ─────────────────────────────────────────────

function MatchDetailInner({ match }: { match: typeof MOCK_MATCH_SCHEDULED }) {
  const status = match.status ?? "Scheduled";
  const isLive = status === "Live";
  const isFinished = status === "Finished";

  return (
    <div className="bg-bg-base px-4 py-4 space-y-3">
      <MatchHeader match={match} />

      {/* LIVE: stats + momentum + timeline */}
      {isLive && match.live?.stats && (
        <>
          <LiveStatsPanel
            stats={match.live.stats}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
          <MomentumChart
            momentum={match.live.momentum ?? []}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
          <LiveTimeline events={match.live.timeline ?? []} />
        </>
      )}

      {/* FINISHED: final timeline */}
      {isFinished && match.live?.timeline && match.live.timeline.length > 0 && (
        <LiveTimeline events={match.live.timeline} title="Maç Olayları" />
      )}

      {/* AI block */}
      {match.ai && <AiBlock ai={match.ai} />}

      {/* Sapma / Piyasa Sinyali */}
      <SapmaBlock sapma={match.sapma} />

      {/* PREVIEW: Intelligence */}
      {!isLive && (
        <>
          <InsightBlock insight={match.insight} />
          {match.probabilities?.length > 0 && (
            <ProbabilitiesBlock items={match.probabilities} />
          )}
          <MarketIntelligenceBlock market={match.marketIntelligence} />
          <TacticalMatchupBlock
            data={match.tacticalMatchup}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
        </>
      )}

      {/* LIVE: market still relevant */}
      {isLive && (
        <MarketIntelligenceBlock market={match.marketIntelligence} />
      )}

      {/* Form + H2H */}
      <FormSection
        homeTeamName={match.homeTeam.name}
        awayTeamName={match.awayTeam.name}
        homeLastMatches={match.homeTeamLastMatches ?? []}
        awayLastMatches={match.awayTeamLastMatches ?? []}
      />
      <H2HSection
        h2h={match.h2h}
        homeTeamName={match.homeTeam.name}
        awayTeamName={match.awayTeam.name}
      />

      {/* Lineup + injuries (pre/during match) */}
      {!isFinished && (
        <>
          <LineupSection
            lineup={match.lineup}
            homeTeamName={match.homeTeam.name}
            awayTeamName={match.awayTeam.name}
          />
          {(match.playerStatus?.injuries?.length > 0 ||
            match.playerStatus?.suspensions?.length > 0 ||
            match.playerStatus?.doubtful?.length > 0) && (
            <PlayerStatusSection playerStatus={match.playerStatus} />
          )}
        </>
      )}

      {/* Standings */}
      {match.standing && <StandingsSection standing={match.standing} />}

      {/* Responsibility note */}
      {match.userProtection && (
        <div className="rounded-xl border border-border-dim bg-bg-card/50 p-3">
          <p className="text-xs text-text-muted text-center leading-relaxed">
            {match.userProtection.responsibilityNote}
          </p>
        </div>
      )}
    </div>
  );
}

// ── Swipe Feed Demo (mock, no backend) ───────────────────────────────────────

// Extend mock feed with repeated cards so there are enough to swipe through
const DEMO_CARDS = [...MOCK_FEED, ...MOCK_FEED, ...MOCK_FEED, ...MOCK_FEED, ...MOCK_FEED];

function SwipeFeedDemo() {
  const [index, setIndex] = useState(0);
  const [swipeLog, setSwipeLog] = useState<{ dir: string; title: string }[]>([]);
  const startTimeRef = useRef(Date.now());

  const activeCard = DEMO_CARDS[index] ?? null;
  const nextCard = DEMO_CARDS[index + 1] ?? null;

  const advance = useCallback((direction: "left" | "right") => {
    const card = DEMO_CARDS[index];
    if (!card) return;
    setSwipeLog((prev) => [
      { dir: direction === "left" ? "←" : "→", title: card.storyHeadline ?? card.teamA },
      ...prev.slice(0, 9),
    ]);
    setIndex((i) => i + 1);
    startTimeRef.current = Date.now();
  }, [index]);

  const isEmpty = index >= DEMO_CARDS.length;

  return (
    <div className="flex flex-col lg:flex-row items-start gap-8">
      {/* Phone frame with swipe feed */}
      <div className="mx-auto max-w-sm w-full bg-bg-base rounded-[2rem] border-2 border-border shadow-2xl overflow-hidden shrink-0">
        <div className="h-7 bg-bg-card flex items-center justify-center">
          <div className="w-20 h-1.5 rounded-full bg-bg-elevated" />
        </div>
        <div className="h-[680px] overflow-hidden px-4 py-5 flex flex-col">
          {isEmpty ? (
            <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center">
              <p className="text-3xl">🏆</p>
              <p className="text-sm font-medium text-text-secondary">Bugünlük bu kadar.</p>
              <button
                onClick={() => { setIndex(0); setSwipeLog([]); }}
                className="text-xs text-accent hover:underline mt-2"
              >
                Baştan başla →
              </button>
            </div>
          ) : activeCard ? (
            <SwipeCardStack
              activeCard={activeCard}
              leftPeek={null}
              rightPeek={nextCard}
              onSwipe={advance}
              onDetailOpen={() => {}}
              renderPeek={(card) => <MatchCard card={card} />}
            >
              {(card) => <MatchCard card={card} />}
            </SwipeCardStack>
          ) : null}
        </div>
        <div className="h-6 bg-bg-card flex items-center justify-center">
          <div className="w-24 h-1 rounded-full bg-bg-elevated" />
        </div>
      </div>

      {/* Swipe log panel */}
      <div className="flex-1 space-y-3 min-w-0">
        <div className="bg-bg-card rounded-xl border border-border p-4">
          <p className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2">
            Durum — {index}/{DEMO_CARDS.length} kart
          </p>
          <div className="w-full bg-bg-elevated rounded-full h-1.5 mb-3">
            <div
              className="bg-accent h-1.5 rounded-full transition-all"
              style={{ width: `${(index / DEMO_CARDS.length) * 100}%` }}
            />
          </div>
          <p className="text-xs text-text-muted">
            Sürükleyerek swipe et. Her swipe sonraki kartı getirir.
          </p>
        </div>
        {swipeLog.length > 0 && (
          <div className="bg-bg-card rounded-xl border border-border p-4 space-y-1.5">
            <p className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2">Son Swipe'lar</p>
            {swipeLog.map((log, i) => (
              <div key={i} className="flex items-center gap-2 text-xs text-text-secondary">
                <span className={log.dir === "←" ? "text-formax-red" : "text-formax-green"}>{log.dir}</span>
                <span className="truncate">{log.title}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function DemoPage() {
  return (
    <div className="min-h-screen bg-bg-base">
      <StickyNav />

      <main className="max-w-4xl mx-auto px-6 py-10">

        {/* ── 0. SWIPE FEED ────────────────────────────────────────────────── */}
        <Section
          id="swipe-feed"
          label="Swipe Feed — Tek Kart Deneyimi"
          badge="UI.9"
          badgeColor="bg-formax-green"
        >
          <SwipeFeedDemo />
        </Section>

        {/* ── 1. HOME FEED ─────────────────────────────────────────────────── */}
        <Section
          id="home-feed"
          label="Kart Listesi (eski format)"
          badge="Referans"
          badgeColor="bg-accent"
        >
          <div className="grid gap-3 sm:grid-cols-2">
            {MOCK_FEED.map((card) => (
              <MatchCard key={card.matchId} card={card} />
            ))}
          </div>
        </Section>

        {/* ── 2. MATCH DETAIL — SCHEDULED ──────────────────────────────────── */}
        <Section
          id="scheduled"
          label="Maç Detay — Maç Öncesi"
          badge="Preview"
          badgeColor="bg-formax-green"
        >
          <div className="flex items-start gap-6">
            <PhoneFrame>
              <MatchDetailInner match={MOCK_MATCH_SCHEDULED} />
            </PhoneFrame>
            <div className="hidden lg:block flex-1 pt-2 space-y-3">
              <div className="bg-bg-card rounded-xl border border-border p-4 space-y-2">
                <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">Ne gösteriyor?</p>
                <ul className="text-sm text-text-secondary space-y-1.5">
                  <li className="flex gap-2"><span className="text-accent">●</span> MatchHeader — skor / tarih / saat / devre</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> AI Block (Extended) — analiz özeti</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Sapma Block — piyasa sinyali metrikleri</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Insight + Olasılık Analizi</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Taktik Karşılaştırma</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Son Form (5 maç) + H2H</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Kadro + Sakatlıklar</li>
                  <li className="flex gap-2"><span className="text-accent">●</span> Puan Tablosu dilimi</li>
                </ul>
              </div>
            </div>
          </div>
        </Section>

        {/* ── 3. MATCH DETAIL — LIVE ───────────────────────────────────────── */}
        <Section
          id="live"
          label="Maç Detay — Canlı (63')"
          badge="CANLI"
          badgeColor="bg-formax-red"
        >
          <div className="flex items-start gap-6">
            <PhoneFrame>
              <MatchDetailInner match={MOCK_MATCH_LIVE} />
            </PhoneFrame>
            <div className="hidden lg:block flex-1 pt-2 space-y-3">
              <div className="bg-bg-card rounded-xl border border-border p-4 space-y-2">
                <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">Canlı state neler gösterir?</p>
                <ul className="text-sm text-text-secondary space-y-1.5">
                  <li className="flex gap-2"><span className="text-formax-red">●</span> MatchHeader — pulse animasyonu, canlı dakika</li>
                  <li className="flex gap-2"><span className="text-formax-red">●</span> LiveStatsPanel — possession, shots, corners, xG</li>
                  <li className="flex gap-2"><span className="text-formax-red">●</span> MomentumChart — basınç grafiği (12 snapshot)</li>
                  <li className="flex gap-2"><span className="text-formax-red">●</span> LiveTimeline — maç olayları kronoloji</li>
                  <li className="flex gap-2"><span className="text-formax-red">●</span> AI Block (Short) — kısıtlı canlı analiz</li>
                  <li className="flex gap-2"><span className="text-formax-red">●</span> Sapma + Piyasa (canlı güncellenmiş)</li>
                  <li className="flex gap-2"><span className="text-text-muted">○</span> Insight / Olasılık gizli (Live state)</li>
                </ul>
              </div>
            </div>
          </div>
        </Section>

        {/* ── 4. MATCH DETAIL — FINISHED ───────────────────────────────────── */}
        <Section
          id="finished"
          label="Maç Detay — Bitti (2-1)"
          badge="Bitti"
          badgeColor="bg-text-muted"
        >
          <div className="flex items-start gap-6">
            <PhoneFrame>
              <MatchDetailInner match={MOCK_MATCH_FINISHED} />
            </PhoneFrame>
            <div className="hidden lg:block flex-1 pt-2 space-y-3">
              <div className="bg-bg-card rounded-xl border border-border p-4 space-y-2">
                <p className="text-xs font-semibold text-text-muted uppercase tracking-wider">Finished state neler gösterir?</p>
                <ul className="text-sm text-text-secondary space-y-1.5">
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> MatchHeader — BİTTİ badge, final skor</li>
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> LiveTimeline — tüm maç olayları (gol, kart, değişiklik)</li>
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> AI Block (Extended) — maç sonu değerlendirme</li>
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> Sapma doğrulama — sinyal tuttu mu?</li>
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> Piyasa İstihbaratı — post-match özet</li>
                  <li className="flex gap-2"><span className="text-text-secondary">●</span> Form + H2H geçmişi</li>
                  <li className="flex gap-2"><span className="text-text-muted">○</span> Kadro/Sakatlık gizli (Finished state)</li>
                </ul>
              </div>
            </div>
          </div>
        </Section>

      </main>
    </div>
  );
}
