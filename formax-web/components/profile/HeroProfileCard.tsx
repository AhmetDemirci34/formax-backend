"use client";

import { Fragment } from "react";
import Image from "next/image";
import { UserIcon } from "@/components/discover/icons";
import { haptic } from "@/lib/utils/haptics";

export interface HeroStat {
  /** Büyük sayı. */
  value: number | string;
  /** Sayının altındaki küçük gri etiket (ALL CAPS). */
  label: string;
}

interface HeroProfileCardProps {
  fullName: string | null;
  username: string | null;
  avatarUrl: string | null;
  /** Üyelik durumu — avatar halkasını ve "Premium Üyelik" yazısını yönetir. */
  isPremium: boolean;
  /** Kartın alt şeridindeki 3 istatistik. */
  stats: [HeroStat, HeroStat, HeroStat];
  /** Karta dokunulduğunda "Hesabım" detayına gider. */
  onOpenAccount: () => void;
}

/** Backend alanı gelene kadar gösterilen açık yer tutucu (mock veri değil). */
const EMPTY = "—";

/**
 * HeroProfileCard — Profil Merkezi'nin üst kartı (Stitch görseli).
 *
 * Düzen:
 *   üst şerit  → lime halkalı avatar SOLDA · sağında Ad-Soyad + "Premium Üyelik"
 *   alt şerit  → 3 istatistik, aralarında dikey ayırıcı çizgiler
 */
export function HeroProfileCard({
  fullName,
  username,
  avatarUrl,
  isPremium,
  stats,
  onOpenAccount,
}: HeroProfileCardProps) {
  return (
    <button
      type="button"
      onClick={() => {
        haptic();
        onOpenAccount();
      }}
      aria-label="Hesabım"
      className="profile-hero-card w-full rounded-2xl px-5 py-6 text-left transition-transform duration-150 active:scale-[0.99] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-profile-accent/60"
    >
      {/* Üst şerit — avatar + kimlik */}
      <div className="flex items-center gap-4">
        {/* Avatar — Stitch'e göre %10 büyütüldü (64 → 70px); lime halka 3 → 4px. */}
        <span
          className={`flex h-[70px] w-[70px] shrink-0 items-center justify-center rounded-full p-[4px] ${
            isPremium ? "bg-profile-accent" : "bg-profile-border"
          }`}
        >
          <span className="flex h-full w-full items-center justify-center overflow-hidden rounded-full bg-profile-surface">
            {avatarUrl ? (
              <Image
                src={avatarUrl}
                alt=""
                width={70}
                height={70}
                className="h-full w-full object-cover"
              />
            ) : (
              // TODO(backend): avatar alanı yok → fallback ikon. Bkz. useUserProfile.
              <UserIcon size={32} className="text-profile-muted" />
            )}
          </span>
        </span>

        {/* İsim ↔ "Premium Üyelik" boşluğu Stitch ile aynı: 6px. */}
        <span className="flex min-w-0 flex-col gap-1.5">
          {/* TODO(backend): Ad-Soyad alanı yok — GET /api/users/me eklendiğinde bağlanacak. */}
          <span className="truncate text-[20px] font-bold leading-none text-white">
            {fullName ?? EMPTY}
          </span>
          <span
            className={`truncate text-[12px] font-semibold leading-none ${
              isPremium ? "text-profile-accent" : "text-profile-muted"
            }`}
          >
            {isPremium ? "Premium Üyelik" : "Ücretsiz Üyelik"}
          </span>
          {/* TODO(backend): @kullanıcıadı alanı yok — aynı uçla gelecek. */}
          {username ? (
            <span className="truncate text-[12px] leading-none text-profile-muted">
              @{username}
            </span>
          ) : null}
        </span>
      </div>

      {/* Alt şerit — 3 istatistik, aralarında dikey ayırıcı.
          Sayılar 24px/bold, ayırıcılar %12 beyaz (daha görünür), sayı ↔ etiket
          boşluğu 8px; etiketler sayının altında ortalanır. */}
      <div className="mt-6 flex items-stretch">
        {stats.map((stat, i) => (
          <Fragment key={stat.label}>
            {i > 0 ? (
              <span
                className="w-px shrink-0 self-stretch bg-[var(--profile-stat-rule)]"
                aria-hidden
              />
            ) : null}
            <span className="flex flex-1 flex-col items-center gap-2 px-1">
              <span className="text-[24px] font-bold leading-none tracking-[-0.5px] text-profile-accent tabular-nums">
                {stat.value}
              </span>
              <span className="text-center text-[10px] font-semibold uppercase leading-[1.25] tracking-[0.5px] text-profile-muted">
                {stat.label}
              </span>
            </span>
          </Fragment>
        ))}
      </div>
    </button>
  );
}
