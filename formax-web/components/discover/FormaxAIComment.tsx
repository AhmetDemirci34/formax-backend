"use client";

import { motion } from "framer-motion";
import type { MatchDetailDto, ProbabilityItemDto, RecommendationCardDto } from "@/types/api";
import { SparklesIcon } from "./icons";
import { stripEmoji } from "./cardSignals";
import { aiSummaryLines } from "./detailFacts";

// ─────────────────────────────────────────────────────────────────────────────
// FORMAX AI — Discovery (Keşfet) özet kartı.
// Bu kart maç DETAYI değildir: kullanıcı 3 saniyede "bu maça neden bakmalıyım?"
// sorusunun cevabını okur. Uzun Intelligence Report yalnız maç detay ekranındadır.
//
// Yapı:  1) Başlık (FORMAX AI)  2) Radar Özeti (2–3 satır editöryel AI metni)
//        3) AI Beklentisi (en güçlü 3 senaryo — backend probabilities'ten dinamik)
// Radar küresi (RadarDisplay) korunur; AI özeti kartın odak noktasıdır.
// ─────────────────────────────────────────────────────────────────────────────

// Radar Özeti metni — backend AI metni ÖNCE (ileride world/news/social sentezi),
// yoksa mevcut storytelling türevine düşülür. Liste değil, tek anlamlı paragraf.
function radarSummary(card: RecommendationCardDto, detail?: MatchDetailDto): string {
  const ai =
    stripEmoji(card.aiSummary) ||
    stripEmoji(card.aiComment) ||
    stripEmoji(card.aiHeadline);
  if (ai) return ai;

  // Türev: hikâye satırları (model/listeleme kapanışı hariç) → 2 cümlelik özet.
  const lines = aiSummaryLines(card, detail).filter((l) => !l.startsWith("FORMAX Intelligence"));
  return lines.slice(0, 2).join(" ");
}

// AI Beklentisi — en yüksek güvenli ilk 3 senaryo. Sahte veri üretilmez:
// gerçek probabilities yoksa bölüm gizlenir.
function topScenarios(detail?: MatchDetailDto): ProbabilityItemDto[] {
  const probs = detail?.probabilities ?? [];
  return [...probs].sort((a, b) => b.probability - a.probability).slice(0, 3);
}

export function FormaxAIComment({ card, detail }: { card: RecommendationCardDto; detail?: MatchDetailDto }) {
  const summary = radarSummary(card, detail);
  const scenarios = topScenarios(detail);
  const confPct = Math.round((card.confidenceScore ?? 0) * 100);

  return (
    <section className="relative overflow-hidden rounded-[28px] border border-white/[0.08] bg-gradient-to-b from-[#17121F] via-[#0F0D17] to-[#07070C] p-7 shadow-[0_30px_80px_rgba(0,0,0,.65)] backdrop-blur-xl">
      {/* Mor imza ışıması — radar köşesinden yayılır */}
      <div
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(130%_85%_at_100%_0%,rgba(168,85,247,.14),transparent_60%)]"
        aria-hidden
      />

      <div className="relative">
        {/* 1 — Başlık + Radar küresi */}
        <header className="flex items-start justify-between gap-4">
          <div className="flex flex-col gap-1.5">
            <div className="flex items-center gap-2">
              <SparklesIcon size={15} className="text-[#A855F7]" />
              <span className="text-[11px] font-bold uppercase tracking-[0.2em] text-[#C9A6FF]">
                FORMAX AI
              </span>
            </div>
            <span className="flex items-center gap-1.5 text-[11px] font-medium text-text-muted">
              <span className="relative flex h-1.5 w-1.5">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-[#A855F7]/70" />
                <span className="relative inline-flex h-1.5 w-1.5 rounded-full bg-[#A855F7]" />
              </span>
              Canlı veri akışı
            </span>
          </div>

          <RadarDisplay confidence={confPct} />
        </header>

        {/* 2 — Radar Özeti (odak noktası) */}
        {summary && (
          <p className="mt-5 max-w-[92%] text-[15px] font-medium leading-[1.6] text-[#E7E1F2]">
            {summary}
          </p>
        )}

        {/* 3 — AI Beklentisi: en güçlü 3 senaryo (dinamik) */}
        {scenarios.length > 0 && (
          <div className="mt-7">
            <div className="mb-3 flex items-center justify-between">
              <span className="text-[10px] font-bold uppercase tracking-[0.18em] text-text-muted">
                AI Beklentisi
              </span>
              <span className="text-[10px] font-medium text-text-muted/70">En güçlü 3 senaryo</span>
            </div>

            <div className="flex flex-col gap-2.5">
              {scenarios.map((s, i) => (
                <ScenarioRow key={s.market + i} rank={i} market={stripEmoji(s.market)} pct={Math.round(s.probability)} />
              ))}
            </div>

            <p className="mt-4 text-[10px] leading-relaxed text-text-muted/60">
              FORMAX AI özeti · kesin tahmin değildir.
            </p>
          </div>
        )}
      </div>
    </section>
  );
}

// Podyum (altın/gümüş/bronz) — emoji yerine premium SVG-uyumlu sıralama rozeti.
const RANK_STYLE = [
  { ring: "#F5C04E", from: "#FFD982", to: "#E0A126", text: "#3A2A06" }, // 1 — altın
  { ring: "#CBD2DE", from: "#EAEEF6", to: "#AEB6C6", text: "#2A2F38" }, // 2 — gümüş
  { ring: "#D89460", from: "#F0B488", to: "#B9743F", text: "#36210F" }, // 3 — bronz
] as const;

function ScenarioRow({ rank, market, pct }: { rank: number; market: string; pct: number }) {
  const r = RANK_STYLE[Math.min(rank, 2)];
  const isTop = rank === 0;

  return (
    <div className="flex items-center gap-3 rounded-2xl border border-white/[0.05] bg-white/[0.02] px-3 py-2.5">
      {/* Sıralama rozeti */}
      <span
        className="grid h-7 w-7 shrink-0 place-items-center rounded-full text-[12px] font-extrabold tabular-nums shadow-[inset_0_1px_1px_rgba(255,255,255,.45)]"
        style={{
          background: `linear-gradient(150deg, ${r.from}, ${r.to})`,
          color: r.text,
          boxShadow: `0 0 0 1px ${r.ring}66, 0 2px 8px ${r.ring}33`,
        }}
      >
        {rank + 1}
      </span>

      {/* Senaryo adı + güven barı */}
      <div className="min-w-0 flex-1">
        <div className="flex items-baseline justify-between gap-2">
          <span className="truncate text-[13.5px] font-semibold text-white">{market}</span>
          <span
            className="shrink-0 text-[13.5px] font-extrabold tabular-nums"
            style={{ color: isTop ? "#C9A6FF" : "#A89CC2" }}
          >
            %{pct}
          </span>
        </div>
        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-white/[0.06]">
          <motion.div
            className="h-full rounded-full"
            style={{
              background: isTop
                ? "linear-gradient(90deg,#7C5CFF,#A855F7,#C9A6FF)"
                : "linear-gradient(90deg,#6E62A0,#8E7FC0)",
            }}
            initial={{ width: 0 }}
            animate={{ width: `${Math.max(4, Math.min(100, pct))}%` }}
            transition={{ duration: 0.9, ease: "easeOut", delay: 0.1 + rank * 0.12 }}
          />
        </div>
      </div>
    </div>
  );
}

// ── Radar Intelligence Core ──────────────────────────────────────────────────
// Mor cam küre içinde ÇALIŞAN radar motoru: katmanlı halkalar + dönen tarama (sweep)
// + merkez hedef + yanıp sönen veri noktaları. Yazı/sayı/ikon YOK. Durum AI Güven'e
// göre renk/yoğunlukla anlaşılır (zayıf=mavi-mor, orta=turuncu, güçlü=yeşil).
const RADAR_DOTS = [
  { cx: 70, cy: 34 }, { cx: 33, cy: 40 }, { cx: 30, cy: 64 }, { cx: 64, cy: 71 },
  { cx: 50, cy: 23 }, { cx: 77, cy: 56 }, { cx: 40, cy: 77 },
];

function radarState(confidence: number) {
  // Daha sakin/premium: zayıf glow, yavaş sweep, soluk halkalar (cam ardında keşfedilir).
  if (confidence >= 70) return { color: "#34D27A", glow: 0.5, sweepDur: 4, dots: 5, dotDur: 2.8, ring: 0.2 };
  if (confidence >= 40) return { color: "#F5A623", glow: 0.4, sweepDur: 6, dots: 4, dotDur: 3.2, ring: 0.16 };
  return { color: "#6E8BFF", glow: 0.32, sweepDur: 8, dots: 3, dotDur: 3.8, ring: 0.13 };
}
const a2 = (alpha: number) => Math.round(Math.min(1, alpha) * 255).toString(16).padStart(2, "0");

function RadarDisplay({ confidence }: { confidence: number }) {
  const st = radarState(confidence);
  const c = st.color;

  return (
    <motion.div
      className="relative h-[92px] w-[92px] shrink-0"
      aria-hidden
      animate={{
        y:[0,-4,0],
        rotate:[0,0.4,0,-0.4,0]
      }}
      transition={{ duration: 3.6, repeat: Infinity, ease: "easeInOut" }}
    >
      {/* Durum parıltısı (renk = sinyal gücü) */}
      <motion.div
        className="absolute -inset-4 rounded-full"
        style={{ background: `radial-gradient(circle, ${c}${a2(st.glow)}, transparent 70%)` }}
        animate={{ opacity: [0.45, 0.9, 0.45], scale: [0.95, 1.06, 0.95] }}
        transition={{ duration: 3.6, repeat: Infinity, ease: "easeInOut" }}
      />

      {/* Glass Radar */}
        <div
          className="absolute inset-0 rounded-full"
          style={{
            background:
              "radial-gradient(circle at 30% 28%, rgba(255,255,255,.18), rgba(7,18,28,.92) 58%, rgba(2,8,14,1) 100%)",
            border: "1px solid rgba(80,255,255,.18)",
            boxShadow:
              "0 0 40px rgba(0,255,255,.16), 0 0 65px rgba(0,255,255,.06), inset 0 0 24px rgba(0,255,255,.12)"
          }}
        />

      {/* RADAR — camın derininde, ikinci planda (zamanla keşfedilir) */}
      <div className="absolute inset-[6%] overflow-hidden rounded-full opacity-100">
        {/* Dönen tarama — dar (~18°) sektör, hafif glow, yavaş */}
        <motion.div
          className="absolute inset-0 blur-[2px]"
          style={{
              transformOrigin: "50% 50%",
              background: `
                conic-gradient(
                  from 0deg,
                  transparent 0deg,                  
                  ${c}55 336deg,
                  ${c}CC 344deg,
                  ${c}CC 350deg,
                  ${c}FF 355deg,
                  transparent 360deg
                )
              `
            }}

            
          animate={{ rotate: 360 }}
          transition={{ duration: st.sweepDur, repeat: Infinity, ease: "linear" }}
        />

        <svg viewBox="0 0 100 100" className="absolute inset-0 h-full w-full">
          {/* İnce cam çizgisi gibi halkalar + eksen */}
          <g stroke={c} fill="none" opacity="0.28">
            {[6,10,14,18,22,26,30,34,38,42,46].map(r=>(
              <circle
                key={r}
                cx="50"
                cy="50"
                r={r}
                strokeWidth="0.22"
              />
            ))}
                                                
          </g>
            
            <g stroke={c} opacity="0.18" strokeWidth="0.22">
                <line x1="50" y1="5" x2="50" y2="95"/>
                <line x1="5" y1="50" x2="95" y2="50"/>

                <line x1="18" y1="18" x2="82" y2="82"/>
                <line x1="82" y1="18" x2="18" y2="82"/>

                <line x1="28" y1="8" x2="72" y2="92"/>
                <line x1="8" y1="28" x2="92" y2="72"/>

                <line x1="72" y1="8" x2="28" y2="92"/>
                <line x1="92" y1="28" x2="8" y2="72"/>
              </g>
            

          {/* Merkez hedef — soluk, gömülü */}
          <g stroke={c} fill="none" strokeWidth="0.7" opacity={0.45}>
            <circle cx="50" cy="50" r="3" />
            <circle cx="50" cy="50" r="7" opacity="0.45" />
            <line x1="50" y1="42" x2="50" y2="45" />
            <line x1="50" y1="55" x2="50" y2="58" />
            <line x1="42" y1="50" x2="45" y2="50" />
            <line x1="55" y1="50" x2="58" y2="50" />
          </g>
          <circle cx="50" cy="50" r="1.1" fill={c} opacity={0.6} />

          {/* Küçük, hafif yanıp sönen veri noktaları */}
          {RADAR_DOTS.slice(0, st.dots).map((d, i) => (
            <motion.circle
              key={i}
              cx={d.cx}
              cy={d.cy}
              r="1.3"
              fill={c}
              animate={{
                opacity:[0.15,1,0.15],
                scale:[1,1.8,1]
                  }}
              transition={{
                duration:2.2,
                repeat:Infinity,
                ease:"easeInOut",
                delay:i*0.35
            }}
            />
          ))}
          
          <circle
            cx="50"
            cy="50"
            r="47"
            fill="none"
            stroke={c}
            strokeWidth="0.6"
            opacity="0.35"
          />

        </svg>
      </div>

      {/* CAM — radar'ın üstünde: yansıma + derinlik (radar camın ardına gömülür) */}
      <div className="pointer-events-none absolute inset-0 rounded-full bg-[radial-gradient(circle_at_32%_24%,rgba(255,255,255,0.30),transparent_40%)]" />
      <div className="pointer-events-none absolute inset-0 rounded-full bg-[linear-gradient(135deg,rgba(255,255,255,0.12),transparent_42%)]" />
      <div className="pointer-events-none absolute inset-0 rounded-full shadow-[inset_0_-12px_26px_rgba(14,5,38,0.7),inset_0_6px_16px_rgba(255,255,255,0.10)]" />
      {/* Üst specular parlama */}
      <div
          className="pointer-events-none absolute left-[28%] top-[18%] h-2 w-3 rounded-full bg-white/35 blur-[3px]"
      />
    </motion.div>
  );
}
