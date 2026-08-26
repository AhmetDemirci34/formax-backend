"use client";

import { motion } from "framer-motion";
import { StarIcon } from "@/components/discover/icons";
import { useFollow } from "@/hooks/useFollow";

/**
 * FORMAX · HeroFollowButton (05)
 * ⭐ Takip Et toggle'ı — MAÇ KARTININ aksiyonu, AI yorumunun değil.
 * Daha önce HeroAICommentCard içinde yaşıyordu; AI anlatısı olmayan maçlarda
 * kart hiç render edilmediği için buton da kayboluyordu. Artık HeroCard'ın
 * footer action alanında, AI yorumundan bağımsız olarak her kartta görünür.
 *
 * Takip durumu GERÇEK backend'den okunur ve yazılır (useFollow → /api/follows).
 * Local/fake state YOK; tasarım (glow, ikon, etiketler) taşındığı hâliyle korunur.
 */
export function HeroFollowButton({ matchId }: { matchId: number }) {
  const { isFollowing: followed, toggle: toggleFollow, isPending } = useFollow(matchId);

  return (
    <motion.button
      type="button"
      onClick={toggleFollow}
      disabled={isPending}
      aria-pressed={followed}
      aria-label={followed ? "Takibi bırak" : "Maçı takip et"}
      whileTap={{ scale: 0.9 }}
      className={`inline-flex shrink-0 items-center gap-1 rounded-full border px-2.5 py-1 text-[11px] font-semibold transition-colors disabled:opacity-60 ${
        followed
          ? "border-neon/40 bg-neon/10 text-neon fx-glow-soft-green"
          : "border-white/10 text-text-secondary hover:text-text-primary"
      }`}
    >
      <span className={followed ? "fx-icon-glow-green" : undefined}>
        <StarIcon size={13} />
      </span>
      {followed ? "Takip Ediliyor" : "Takip Et"}
    </motion.button>
  );
}
