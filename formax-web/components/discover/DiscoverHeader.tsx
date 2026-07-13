"use client";

import type { ReactNode } from "react";
import { MenuIcon, SearchIcon, BellIcon } from "./icons";

interface Props {
  onMenu?: () => void;
  onSearch?: () => void;
  onNotifications?: () => void;
  onProfile?: () => void;
  /** Bildirim noktası (PNG'de aktif). */
  hasUnread?: boolean;
}

/**
 * FORMAX · Header (03)
 * PNG referansı: hamburger · marka + "FUTBOLU ANLAYAN ZEKÂ" · search / bell(dot) / profil.
 * Tüm renkler Design Token; ölçüler Layout System.
 */
export function DiscoverHeader({
  onMenu,
  onSearch,
  onNotifications,
  onProfile,
  hasUnread = true,
}: Props) {
  return (
    <header className="flex h-[var(--header-height)] items-center justify-between border-b border-white/5 bg-bg-deep/70 px-4">
      <div className="flex items-center gap-2.5">
        <IconButton label="Menü" onClick={onMenu}>
          <MenuIcon size={22} />
        </IconButton>
        <BrandLogo />
      </div>

      <div className="flex items-center gap-1">
        <IconButton label="Ara" onClick={onSearch}>
          <SearchIcon size={21} />
        </IconButton>
        <IconButton label="Bildirimler" onClick={onNotifications}>
          <span className="relative">
            <BellIcon size={21} />
            {hasUnread ? (
              <span className="absolute -right-0.5 -top-0.5 h-2 w-2 rounded-full bg-neon ring-2 ring-bg-deep" />
            ) : null}
          </span>
        </IconButton>
        <ProfileAvatar onClick={onProfile} />
      </div>
    </header>
  );
}

function BrandLogo() {
  return (
    <div className="leading-none">
      <span className="text-[25px] font-black tracking-[-0.01em] text-text-primary">
        FORMA<span className="text-neon">X</span>
      </span>
      <p className="mt-1 text-[10px] font-semibold uppercase tracking-[0.13em] text-text-secondary">
        Futbolu Anlayan Zekâ
      </p>
    </div>
  );
}

function ProfileAvatar({ onClick }: { onClick?: () => void }) {
  return (
    <button
      type="button"
      aria-label="Profil"
      onClick={onClick}
      className="fx-glow-soft-green ml-1 grid h-9 w-9 place-items-center rounded-full bg-bg-glass ring-[3px] ring-neon/70 transition-transform active:scale-95"
    >
      <span className="flex gap-[3px]">
        <span className="h-1 w-1 rounded-full bg-neon" />
        <span className="h-1 w-1 rounded-full bg-neon" />
        <span className="h-1 w-1 rounded-full bg-neon" />
      </span>
    </button>
  );
}

function IconButton({
  children,
  label,
  onClick,
}: {
  children: ReactNode;
  label: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      className="grid h-11 w-11 place-items-center rounded-full text-text-secondary transition-colors hover:bg-white/5 hover:text-text-primary active:scale-95"
    >
      {children}
    </button>
  );
}
