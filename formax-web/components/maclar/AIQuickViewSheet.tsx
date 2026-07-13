"use client";

// FORMAX · AI Quick View — PLACEHOLDER bottom sheet.
// Şimdilik yalnızca açılır; tam AI Quick View (Component 03) ayrı sprintte bağlanacak.
// Maç satırına / "AI İncele"ye dokununca açılır.

import Link from "next/link";
import { AnimatePresence, motion } from "framer-motion";
import { TeamCrest } from "@/components/ui/TeamCrest";
import { CloseIcon, TargetIcon, MessageIcon, ChevronRightIcon } from "./icons";
import { confidenceColor, kickoffLabel, type MaclarMatch } from "./matchData";

interface Props {
  match: MaclarMatch | null;
  onClose: () => void;
}

export function AIQuickViewSheet({ match, onClose }: Props) {
  return (
    <AnimatePresence>
      {match && (
        <>
          <motion.div
            className="fixed inset-0 z-[55] bg-black/55"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />
          <motion.div
            className="fixed bottom-0 left-1/2 z-[60] w-full max-w-[430px] -translate-x-1/2 rounded-t-[24px] border border-b-0 border-white/10 bg-bg-deep px-5 pb-8 pt-2.5"
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            exit={{ y: "100%" }}
            transition={{ type: "spring", damping: 32, stiffness: 340 }}
            role="dialog"
            aria-label="AI Quick View"
          >
            <div className="mx-auto mb-4 h-1 w-9 rounded-full bg-[#333941]" />

            <div className="flex items-start justify-between">
              <div>
                <div className="flex items-center gap-2">
                  <TeamCrest name={match.home} logoUrl={match.homeLogoUrl} size={22} />
                  <span className="text-[17px] font-medium text-text-primary">{match.home}</span>
                </div>
                <div className="my-1 ml-1.5 text-[11px] text-text-muted">vs</div>
                <div className="flex items-center gap-2">
                  <TeamCrest name={match.away} logoUrl={match.awayLogoUrl} size={22} />
                  <span className="text-[17px] font-medium text-text-primary">{match.away}</span>
                </div>
                <div className="mt-2 text-[12px] text-text-muted">
                  {match.league.name} · {kickoffLabel(match)}
                </div>
              </div>

              <div className="flex flex-col items-end gap-2">
                <button onClick={onClose} aria-label="Kapat" className="text-text-secondary">
                  <CloseIcon size={20} />
                </button>
                <div
                  className="rounded-xl border px-3 py-1.5 text-center"
                  style={{ borderColor: "rgba(46,230,110,0.38)", background: "rgba(46,230,110,0.10)" }}
                >
                  <div className="text-[10px] text-text-muted">AI Güveni</div>
                  <div className="text-[22px] font-semibold leading-tight" style={{ color: confidenceColor(match.aiConfidence) }}>
                    {match.aiConfidence}
                  </div>
                </div>
              </div>
            </div>

            {/* Ana tahmin */}
            <div
              className="mt-4 flex items-center justify-between rounded-[14px] border px-3.5 py-3"
              style={{ borderColor: "rgba(46,230,110,0.28)", background: "rgba(46,230,110,0.08)" }}
            >
              <span className="flex items-center gap-2 text-[16px] font-medium text-text-primary">
                <span className="text-neon"><TargetIcon size={16} /></span>
                {match.mainPrediction.label}
              </span>
              <span className="text-right">
                <span className="block text-[10px] text-text-muted">AI Güveni</span>
                <span className="text-[16px] font-semibold text-neon">%{match.mainPrediction.confidence}</span>
              </span>
            </div>

            {/* 💬 AI Yorumu — Quick View'ın en önemli alanı: tahminin nedenini doğal dille açıklar */}
            <div className="mt-4 rounded-[14px] border border-white/[0.06] bg-bg-glass px-4 py-3.5">
              <div className="mb-2 flex items-center gap-2">
                <span
                  className="flex h-6 w-6 items-center justify-center rounded-full"
                  style={{ background: "rgba(46,230,110,0.12)", color: "#2EE66E" }}
                >
                  <MessageIcon size={14} />
                </span>
                <span className="text-[13px] font-medium text-text-primary">AI Yorumu</span>
              </div>
              <div className="flex flex-col gap-2">
                {match.aiComment.map((para, i) => (
                  <p
                    key={i}
                    className={`text-[13.5px] leading-relaxed ${
                      i === match.aiComment.length - 1
                        ? "font-medium text-text-primary"
                        : "text-text-secondary"
                    }`}
                  >
                    {para}
                  </p>
                ))}
              </div>
            </div>

            <p className="mt-4 text-[12px] leading-relaxed text-text-muted">
              AI&apos;ın en güçlü senaryoları ve canlı analiz yakında bu görünüme eklenecek.
            </p>

            {/* Aksiyonlar */}
            <div className="mt-4 flex gap-2.5">
              <button
                onClick={onClose}
                className="h-[46px] shrink-0 rounded-[14px] border border-[#2A2F37] px-4 text-[14px] font-medium text-text-primary"
              >
                Kapat
              </button>
              <Link
                href={`/match/${match.id}`}
                className="flex h-[46px] flex-1 items-center justify-center gap-1.5 rounded-[14px] bg-neon text-[14px] font-semibold text-[#06170E]"
              >
                Detaylı İncele
                <ChevronRightIcon size={16} strokeWidth={2.4} />
              </Link>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
