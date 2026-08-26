"use client";

import { SparklesIcon, ChevronRightIcon } from "@/components/discover/icons";

interface Props {
  /** Decision paketinden SEÇİLMİŞ teaser satırları (maks. 3). Frontend üretmez. */
  lines: string[];
  onDetail?: () => void;
}

/**
 * FORMAX · HeroAICommentCard (05)
 * PNG hero içindeki cam yorum kartı: ✨ FORMAX AI YORUMU + metin + "Tüm Analizi Gör →".
 * ⭐ Takip Et burada DEĞİLDİR: takip AI yorumunun değil maç kartının aksiyonudur,
 * bu yüzden HeroCard'ın footer alanına taşındı (bkz. HeroFollowButton). Böylece
 * AI anlatısı olmayan maçlarda da takip butonu görünür.
 */
export function HeroAICommentCard({ lines, onDetail }: Props) {
  return (
    <div className="rounded-2xl border border-neon/15 bg-bg-deep/55 p-4 backdrop-blur-md">
      <div className="mb-2.5 flex items-center gap-2">
        <SparklesIcon size={14} className="shrink-0 text-neon" />
        <span className="text-[10px] font-bold uppercase tracking-[0.08em] text-neon">
          FORMAX AI Yorumu
        </span>
      </div>
      {/* Teaser — en fazla 3 satır. Tüm analiz burada verilmez. */}
      <div className="space-y-1.5">
        {lines.map((line, i) => (
          <p key={i} className="text-[12.5px] leading-[1.6] text-text-secondary">
            {line}
          </p>
        ))}
      </div>

      <div className="mt-3 flex items-center gap-2">
        <button
          type="button"
          onClick={onDetail}
          className="inline-flex items-center gap-0.5 text-[11px] font-semibold text-neon transition-opacity hover:opacity-80"
        >
          Tüm Analizi Gör
          <ChevronRightIcon size={13} />
        </button>
      </div>
    </div>
  );
}
