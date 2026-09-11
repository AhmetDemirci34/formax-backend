"use client";

// FORMAX · Maç takip ikonu — maç satırının EN SOLU.
//
// TIKLAMA AYRIMI: bu düğme kendi olayını YUTAR (stopPropagation + preventDefault),
// bu yüzden takip etmek Maç Detay ekranını AÇMAZ. Satırın kendisi ve "AI İncele"
// düğmesi detayı açmaya devam eder.
//
// Dokunma alanı 40×40 px (mobil erişilebilirlik); ikon takım armasıyla karışmasın diye
// armalardan ayrı sütunda ve farklı biçimde (yıldız) durur.

import { useFollow } from "@/hooks/useFollow";

interface Props {
  matchId: number;
}

export function FollowStar({ matchId }: Props) {
  const { isFollowing, toggle, isPending } = useFollow(matchId);

  return (
    <button
      type="button"
      onClick={(e) => {
        e.stopPropagation();
        e.preventDefault();
        toggle();
      }}
      disabled={isPending}
      aria-pressed={isFollowing}
      aria-label={isFollowing ? "Maçı takipten çıkar" : "Maçı takip et"}
      title={isFollowing ? "Maçı takipten çıkar" : "Maçı takip et"}
      className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] transition-colors disabled:opacity-50 ${
        isFollowing ? "text-neon hover:bg-neon/10" : "text-[#4A5060] hover:bg-white/[0.06] hover:text-[#8A93A6]"
      }`}
    >
      <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
        <path
          d="M12 3.5l2.6 5.28 5.83.85-4.22 4.11.996 5.8L12 16.82l-5.21 2.74.996-5.8L3.57 9.63l5.83-.85L12 3.5z"
          fill={isFollowing ? "currentColor" : "none"}
          stroke="currentColor"
          strokeWidth="1.7"
          strokeLinejoin="round"
        />
      </svg>
    </button>
  );
}
