import { SparklesIcon, ChevronRightIcon } from "@/components/discover/icons";

interface Props {
  text: string;
  onDetail?: () => void;
}

/**
 * FORMAX · HeroAICommentCard (05)
 * PNG hero içindeki cam yorum kartı: ✨ FORMAX AI YORUMU + metin + "Detaylı analizi gör →".
 */
export function HeroAICommentCard({ text, onDetail }: Props) {
  return (
    <div className="rounded-2xl border border-neon/15 bg-bg-deep/55 p-4 backdrop-blur-md">
      <div className="mb-2.5 flex items-center gap-2">
        <SparklesIcon size={14} className="shrink-0 text-neon" />
        <span className="text-[10px] font-bold uppercase tracking-[0.08em] text-neon">
          FORMAX AI Yorumu
        </span>
      </div>
      <p className="text-[12.5px] leading-[1.6] text-text-secondary">{text}</p>
      <button
        type="button"
        onClick={onDetail}
        className="mt-3 inline-flex items-center gap-0.5 text-[11px] font-semibold text-neon transition-opacity hover:opacity-80"
      >
        Detaylı analizi gör
        <ChevronRightIcon size={13} />
      </button>
    </div>
  );
}
