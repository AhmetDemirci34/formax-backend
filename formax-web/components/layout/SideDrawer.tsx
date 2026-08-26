"use client";

import { useEffect, type ComponentType } from "react";
import { useRouter } from "next/navigation";
import { AnimatePresence, motion, type PanInfo } from "framer-motion";
import { useChrome } from "@/context/ChromeContext";
import {
  UserIcon,
  CrownIcon,
  ShieldIcon,
  HeartIcon,
  ClipboardListIcon,
  TrophyIcon,
  NewspaperIcon,
  GlobeIcon,
  BellIcon,
  SettingsIcon,
  LifeBuoyIcon,
  SparklesIcon,
} from "@/components/discover/icons";

type IconType = ComponentType<{ size?: number; className?: string }>;

interface NavItem {
  key: string;
  label: string;
  Icon: IconType;
  /** Var olan bir rotaya git (yoksa yalnızca drawer kapanır — UI hazır). */
  href?: string;
  /** Özel aksiyon: dil seçiciyi aç. */
  action?: "lang";
  badge?: string;
}

/**
 * Drawer navigasyonu — tek kaynak.
 * Rotası olan öğeler yönlendirir; olmayanlar UI-hazır placeholder (drawer kapanır).
 */
const NAV_ITEMS: NavItem[] = [
  { key: "profile", label: "Profil", Icon: UserIcon, href: "/profile" },
  { key: "premium", label: "Premium", Icon: CrownIcon, badge: "PRO" },
  { key: "teams", label: "Takımlarım", Icon: ShieldIcon },
  { key: "following", label: "Takip Ettiklerim", Icon: HeartIcon, href: "/following" },
  { key: "predictions", label: "Tahminlerim", Icon: ClipboardListIcon, href: "/predictions" },
  { key: "leagues", label: "Ligler", Icon: TrophyIcon },
  { key: "news", label: "Haberler", Icon: NewspaperIcon },
  { key: "lang", label: "Dil", Icon: GlobeIcon, action: "lang" },
  { key: "notifications", label: "Bildirimler", Icon: BellIcon, href: "/notifications" },
  { key: "settings", label: "Ayarlar", Icon: SettingsIcon },
  { key: "support", label: "Destek", Icon: LifeBuoyIcon },
];

/**
 * FORMAX · SideDrawer
 * Soldan gelen premium drawer. İçerik push efekti AppChrome'da; burada overlay + panel.
 * Kapanma: overlay tap · aşağı/sola sürükleme · Escape · geri tuşu (history state).
 * Animasyon: transform tabanlı (60 FPS), easeInOut.
 */
export function SideDrawer() {
  const router = useRouter();
  const { drawerOpen, closeDrawer, openLang } = useChrome();

  // Escape + Android/tarayıcı geri tuşu ile kapanma (routing'i bozmadan).
  useEffect(() => {
    if (!drawerOpen) return;

    window.history.pushState({ fxDrawer: true }, "");
    const onPop = () => closeDrawer();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") closeDrawer();
    };
    window.addEventListener("popstate", onPop);
    window.addEventListener("keydown", onKey);

    return () => {
      window.removeEventListener("popstate", onPop);
      window.removeEventListener("keydown", onKey);
      // UI ile kapandıysa eklediğimiz history kaydını geri al.
      if (window.history.state?.fxDrawer) window.history.back();
    };
  }, [drawerOpen, closeDrawer]);

  const onDragEnd = (_e: unknown, info: PanInfo) => {
    if (info.offset.x < -70 || info.velocity.x < -500) closeDrawer();
  };

  const handleItem = (item: NavItem) => {
    if (item.action === "lang") {
      closeDrawer();
      // Drawer kapanış animasyonu bitince dil sheet'ini aç.
      setTimeout(openLang, 260);
      return;
    }
    if (item.href) {
      router.push(item.href);
    }
    closeDrawer();
  };

  return (
    <AnimatePresence>
      {drawerOpen && (
        <div className="absolute inset-0 z-[70]">
          {/* Overlay — dokununca kapanır */}
          <motion.button
            type="button"
            aria-label="Menüyü kapat"
            onClick={closeDrawer}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.32, ease: "easeInOut" }}
            className="absolute inset-0 bg-black/45"
          />

          {/* Panel */}
          <motion.aside
            aria-label="Menü"
            drag="x"
            dragConstraints={{ left: 0, right: 0 }}
            dragElastic={{ left: 0.35, right: 0 }}
            onDragEnd={onDragEnd}
            initial={{ x: "-100%" }}
            animate={{ x: 0 }}
            exit={{ x: "-100%" }}
            transition={{ duration: 0.42, ease: [0.4, 0, 0.2, 1] }}
            className="absolute inset-y-0 left-0 flex w-[80%] max-w-[330px] flex-col border-r border-white/10 bg-bg-glass shadow-[24px_0_60px_rgba(0,0,0,.55)] will-change-transform"
          >
            {/* Üst — marka + profil özeti */}
            <div className="flex items-center gap-3 px-5 pb-4 pt-[calc(var(--safe-top)+22px)]">
              <span className="fx-glow-soft-green grid h-11 w-11 place-items-center rounded-full bg-bg-deep ring-2 ring-neon/60">
                <UserIcon size={20} className="text-neon" />
              </span>
              <div className="leading-tight">
                <p className="text-[15px] font-bold text-text-primary">Misafir</p>
                <p className="text-[11px] font-medium text-text-muted">
                  Giriş yap / Kayıt ol
                </p>
              </div>
            </div>

            <div className="mx-5 h-px bg-white/5" />

            {/* Menü öğeleri */}
            <nav className="flex-1 overflow-y-auto px-3 py-3">
              <ul className="flex flex-col gap-0.5">
                {NAV_ITEMS.map((item) => {
                  const { Icon } = item;
                  return (
                    <li key={item.key}>
                      <button
                        type="button"
                        onClick={() => handleItem(item)}
                        className="flex w-full items-center gap-3.5 rounded-2xl px-3 py-3 text-left text-text-secondary transition-colors hover:bg-white/5 hover:text-text-primary active:scale-[0.99]"
                      >
                        <span className="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-white/[0.04] text-text-secondary">
                          <Icon size={19} />
                        </span>
                        <span className="flex-1 text-[14.5px] font-medium">
                          {item.label}
                        </span>
                        {item.badge ? (
                          <span className="rounded-full bg-neon/15 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wide text-neon">
                            {item.badge}
                          </span>
                        ) : null}
                      </button>
                    </li>
                  );
                })}
              </ul>
            </nav>

            {/* Alt — marka imzası */}
            <div className="mt-auto flex items-center gap-2.5 border-t border-white/5 px-5 py-4 pb-[calc(var(--safe-bottom)+16px)]">
              <SparklesIcon size={16} className="text-neon" />
              <div className="leading-tight">
                <p className="text-[13px] font-bold text-text-primary">FORMAX AI</p>
                <p className="text-[10.5px] font-medium text-text-muted">Version 1.0</p>
              </div>
            </div>
          </motion.aside>
        </div>
      )}
    </AnimatePresence>
  );
}
