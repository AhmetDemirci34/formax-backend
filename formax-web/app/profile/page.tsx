"use client";

// ─────────────────────────────────────────────────────────────────────────────
// FORMAX Profil Merkezi — SCREEN_15  (/profile)
//
// Tek doğruluk kaynağı: Stitch tasarımı + UI Specification + Developer Handoff
// + Interaction & Lifecycle Specification.
//
// Telefon çerçevesi, kaydırma konteyneri ve global BottomNav `app/layout.tsx` →
// LayoutWrapper → AppChrome tarafından sağlanır; burada TEKRARLANMAZ.
// ─────────────────────────────────────────────────────────────────────────────

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { useAuth } from "@/context/AuthContext";
import { useChrome } from "@/context/ChromeContext";
import { useUserProfile } from "@/hooks/useUserProfile";
import { useOnlineStatus } from "@/hooks/useOnlineStatus";
import { useScrolledPast, findScrollParent } from "@/hooks/useScrolledPast";
import { useUserPredictions } from "@/hooks/useUserPredictions";
import { useFollowedIds } from "@/hooks/useFollow";
import { useMyTeams } from "@/hooks/useTeams";
import { LANGUAGES } from "@/components/layout/LanguageSheet";
import pkg from "@/package.json";

import { hankenGrotesk } from "@/components/profile/fonts";
import { ProfileHeader } from "@/components/profile/ProfileHeader";
import { HeroProfileCard, type HeroStat } from "@/components/profile/HeroProfileCard";
import { SectionGroup } from "@/components/profile/SectionGroup";
import { NavListItem } from "@/components/profile/NavListItem";
import { LogoutCard } from "@/components/profile/LogoutCard";
import { ProfileSkeleton } from "@/components/profile/ProfileSkeleton";
import { LogoutAlert } from "@/components/profile/LogoutAlert";
import { ProfileBottomNav } from "@/components/profile/ProfileBottomNav";

import {
  UserIcon,
  BellIcon,
  HeartIcon,
  ClipboardListIcon,
  StarIcon,
  ShieldIcon,
  LifeBuoyIcon,
  InfoIcon,
} from "@/components/discover/icons";
import { LanguageIcon, ThemeIcon } from "@/components/profile/icons";

/** Liste satırı ikon ölçüsü — Stitch görseli. */
const ICON = 20;

/**
 * "Hakkında" satırında gösterilen sürüm — projenin GERÇEK sürümü.
 * TODO(ürün): Stitch görselinde "Sürüm 2.4.3" yazıyor; bu tasarım yer tutucusu
 * uydurulmadı, package.json'daki gerçek sürüm bağlandı.
 */
const APP_VERSION: string = pkg.version;

export default function ProfilePage() {
  const router = useRouter();
  const rootRef = useRef<HTMLDivElement>(null);

  const { logout } = useAuth();
  const { openLang, language } = useChrome();
  const activeLanguage = LANGUAGES.find((l) => l.code === language);
  const { fullName, username, avatarUrl, isPremium, isLoading } = useUserProfile();

  // Hero kartındaki 3 istatistik — hepsi MEVCUT veri kaynaklarından beslenir.
  // TODO(tasarım): Stitch görselinde istatistik etiketleri okunamıyor (metinler
  // render olmamış). Projede gerçek karşılığı olan üç sayaç seçildi; doğru
  // etiketler netleşince yalnızca bu dizi değişecek.
  const { counts } = useUserPredictions();
  const { data: followedIds = [] } = useFollowedIds();
  const { data: teams = [] } = useMyTeams();
  const stats: [HeroStat, HeroStat, HeroStat] = [
    { value: counts.total, label: "Tahmin" },
    { value: followedIds.length, label: "Takip" },
    { value: teams.length, label: "Takım" },
  ];

  const isOnline = useOnlineStatus();
  const scrolled = useScrolledPast(rootRef, 10);
  const [logoutOpen, setLogoutOpen] = useState(false);

  // Lifecycle §1: ekran her açıldığında scroll pozisyonu y: 0.
  useEffect(() => {
    const target = findScrollParent(rootRef.current);
    if (target instanceof Window) window.scrollTo(0, 0);
    else target.scrollTop = 0;
  }, []);

  function handleLogoutConfirm() {
    // TODO(backend): Interaction Spec §9 `POST /api/v1/auth/logout` ucu FORMAX
    // backend'inde YOK (mevcut uçlar: /api/auth/register, /login, /forgot-password).
    // Uç eklendiğinde burada çağrılmalı; şimdilik oturum yalnızca istemcide kapatılır.
    setLogoutOpen(false);
    logout();
    router.replace("/auth/login"); // Flow E: Onay Alert → Login Screen
  }

  return (
    <div
      ref={rootRef}
      className={`${hankenGrotesk.variable} relative flex min-h-[100dvh] flex-col bg-profile-surface font-[family-name:var(--font-profile)]`}
    >
      {/* Lifecycle §1 / Animation §5 — Page Slide: 300ms, translateX %100 → 0.
          CSS keyframe kullanılır; gerekçe globals.css `.profile-page-enter`. */}
      <div className="profile-page-enter flex min-h-[100dvh] flex-col">
        <ProfileHeader
          scrolled={scrolled}
          onEdit={() => {
            // Düzenleme, Hesabım (SCREEN_12) ekranı üzerinden yapılır.
            router.push("/account");
          }}
        />

        {/* State §6 — isOffline: header altında "Bağlantı Yok" barı */}
        {!isOnline ? (
          <div
            role="status"
            className="bg-formax-red/15 px-5 py-2 text-center text-[12px] font-semibold text-formax-red"
          >
            Bağlantı Yok
          </div>
        ) : null}

        {/* Lifecycle §1 — içerik, header sabitlendikten 50ms sonra fade-in.
            Yatay boşluğu kartların kendisi (mx-4) taşır. */}
        <main
          className={`profile-content-enter flex-1 pb-[calc(var(--bottom-nav-height)+var(--safe-bottom))] ${
            isOnline ? "" : "pointer-events-none"
          }`}
        >
          {isLoading ? (
            <ProfileSkeleton />
          ) : (
            <>
              {/* Hero kartı — section kartlarıyla aynı 16px yan boşluk. */}
              <div className="px-4 pb-6 pt-4">
                <HeroProfileCard
                  fullName={fullName}
                  username={username}
                  avatarUrl={avatarUrl}
                  isPremium={isPremium}
                  stats={stats}
                  onOpenAccount={() => {
                    // Hero kartı → Hesabım (SCREEN_12).
                    router.push("/account");
                  }}
                />
              </div>

              <div className="flex flex-col gap-6">
                {/* Başlıklar doğrudan büyük harf: Türkçe ı→I / i→İ eşlemesi
                    CSS uppercase'e bırakılmaz (bkz. ProfileHeader notu).
                    Satır alt açıklamaları Stitch görselinden birebir alınmıştır. */}
                <SectionGroup title="HIZLI ERİŞİM">
                  <NavListItem
                    icon={<UserIcon size={ICON} />}
                    title="Hesabım"
                    subtitle="Profil bilgilerini düzenle"
                    // SCREEN_12 — Hesabım alt ekranı (push).
                    onClick={() => router.push("/account")}
                  />
                  <NavListItem
                    icon={<BellIcon size={ICON} />}
                    title="Bildirimler"
                    subtitle="Gol, ilk 11, Haber ve AI bildirimleri"
                    // SCREEN_04 — Bildirim TERCİHLERİ (bildirim geçmişi değil;
                    // geçmiş /notifications rotasında kalır).
                    onClick={() => router.push("/notifications/settings")}
                  />
                  <NavListItem
                    icon={<HeartIcon size={ICON} />}
                    title="Takip Ettiklerim"
                    subtitle="Takımlar, Ligler ve Maçlar"
                    onClick={() => router.push("/following")}
                  />
                  <NavListItem
                    icon={<ClipboardListIcon size={ICON} />}
                    title="Tahminlerim"
                    subtitle="Tahmin geçmişini görüntüle"
                    onClick={() => router.push("/predictions")}
                  />
                  <NavListItem
                    icon={<StarIcon size={ICON} fill="currentColor" strokeWidth={0} />}
                    title="Premium"
                    subtitle="Premium özelliklerini keşfet"
                    isHighlighted
                    onClick={() => {
                      // TODO(route): Premium/abonelik ekranı rotası projede YOK.
                      // Abonelik verisi var (GET /api/users/me/subscription),
                      // yönetim ekranı ve ödeme ucu tanımlı değil — uydurulmadı.
                    }}
                  />
                </SectionGroup>

                <SectionGroup title="GENEL">
                  <NavListItem
                    icon={<LanguageIcon size={ICON} />}
                    title="Dil"
                    // Alt metin seçili dilden gelir (ChromeContext, tek kaynak).
                    subtitle={activeLanguage?.native}
                    // Modal → mevcut LanguageSheet.
                    onClick={openLang}
                  />
                  <NavListItem
                    icon={<ThemeIcon size={ICON} />}
                    title="Tema"
                    subtitle="Karanlık mod aktif"
                    onClick={() => {
                      // TODO(route/state): Tema ekranı ve tema state'i projede YOK.
                      // Uygulama şu an yalnızca karanlık temada çalışıyor.
                    }}
                  />
                  <NavListItem
                    icon={<ShieldIcon size={ICON} />}
                    title="Gizlilik"
                    subtitle="Veri ve güvenlik tercihleri"
                    onClick={() => {
                      // TODO(route): Gizlilik ekranı rotası projede YOK.
                    }}
                  />
                  <NavListItem
                    icon={<LifeBuoyIcon size={ICON} />}
                    title="Yardım"
                    subtitle="Destek merkezi ve SSS"
                    onClick={() => {
                      // TODO(route/içerik): Yardım ekranı ya da destek adresi YOK.
                    }}
                  />
                  <NavListItem
                    icon={<InfoIcon size={ICON} />}
                    title="Hakkında"
                    subtitle={`Sürüm ${APP_VERSION}`}
                    onClick={() => {
                      // TODO(route): Hakkında ekranı rotası projede YOK.
                    }}
                  />
                </SectionGroup>
              </div>

              {/* Çıkış Yap — section'lardan ayrı, en altta duran kendi kartı. */}
              <div className="mt-6">
                <LogoutCard onClick={() => setLogoutOpen(true)} />
              </div>
            </>
          )}
        </main>
      </div>

      {/* Görseldeki 4 sekmeli sabit navigasyon. Global 5 sekmeli BottomNav bu
          rotada gizlidir (components/ui/BottomNav.tsx · HIDE_ON). */}
      <ProfileBottomNav />

      <LogoutAlert
        open={logoutOpen}
        onCancel={() => setLogoutOpen(false)}
        onConfirm={handleLogoutConfirm}
      />
    </div>
  );
}
