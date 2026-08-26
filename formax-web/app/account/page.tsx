"use client";

// ─────────────────────────────────────────────────────────────────────────────
// FORMAX Hesabım — SCREEN_12  (/account)
//
// Profil Merkezi'nin (SCREEN_15 · /profile) ALT ekranı: "Hesabım" satırından
// push ile açılır, header'daki geri butonu Profil'e döner.
// Tek doğruluk kaynağı: Stitch görseli (Developer Handoff yalnızca açıklayıcı).
//
// Telefon çerçevesi ve kaydırma konteyneri layout'tan gelir. Global BottomNav
// bu rotada gizlidir (components/ui/BottomNav.tsx · HIDE_ON) — Stitch'te alt
// navigasyon yoktur.
// ─────────────────────────────────────────────────────────────────────────────

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";

import { useUserProfile } from "@/hooks/useUserProfile";
import { useSubscription } from "@/hooks/useSubscription";
import { useScrolledPast, findScrollParent } from "@/hooks/useScrolledPast";
import pkg from "@/package.json";

import { hankenGrotesk } from "@/components/profile/fonts";
import { SectionGroup } from "@/components/profile/SectionGroup";
import { AccountHeader } from "@/components/account/AccountHeader";
import { AccountIdentity } from "@/components/account/AccountIdentity";
import { InfoRow } from "@/components/account/InfoRow";
import { DangerCard } from "@/components/account/DangerCard";

/** Backend alanı gelene kadar gösterilen açık yer tutucu (mock veri değil). */
const EMPTY = "—";

/**
 * Footer sürüm metni — projenin GERÇEK sürümü.
 * TODO(ürün): Stitch'te "FORMAX App Version 2.4.0 (Build 441)" yazıyor; sürüm
 * package.json'dan okunur, build numarası için kaynak YOK — uydurulmadı.
 */
const VERSION_TEXT = `FORMAX App Version ${pkg.version}`;

export default function AccountPage() {
  const router = useRouter();
  const rootRef = useRef<HTMLDivElement>(null);

  const { fullName, username, avatarUrl, isPremium } = useUserProfile();
  const { data: subscription } = useSubscription();
  const scrolled = useScrolledPast(rootRef, 10);

  // Ekran her açıldığında scroll pozisyonu y: 0.
  useEffect(() => {
    const target = findScrollParent(rootRef.current);
    if (target instanceof Window) window.scrollTo(0, 0);
    else target.scrollTop = 0;
  }, []);

  return (
    <div
      ref={rootRef}
      className={`${hankenGrotesk.variable} relative flex min-h-[100dvh] flex-col bg-profile-surface font-[family-name:var(--font-profile)]`}
    >
      {/* Push (sağdan sola) giriş — Profil Merkezi ile aynı reçete. */}
      <div className="profile-page-enter flex min-h-[100dvh] flex-col">
        <AccountHeader scrolled={scrolled} onBack={() => router.push("/profile")} />

        <main className="profile-content-enter flex-1 pb-[calc(var(--safe-bottom)+32px)]">
          <AccountIdentity
            fullName={fullName}
            username={username}
            avatarUrl={avatarUrl}
            isPremium={isPremium}
          />

          <div className="mt-7 flex flex-col gap-6">
            {/* Başlıklar doğrudan büyük harf: CSS uppercase bazı motorlarda
                Türkçe ı→I / i→İ eşlemesini yapmaz. */}
            <SectionGroup title="KİŞİSEL BİLGİLER" dividerInset={20} dividerStrong>
              <InfoRow
                label="Ad Soyad"
                // TODO(backend): Ad-Soyad alanı YOK. Mevcut kullanıcı uçları
                // yalnızca /api/users/me/subscription|teams|leagues; AuthResponseDto
                // da UserId/Token/IsPremium/AccessLevel döner. Uç eklendiğinde
                // useUserProfile üzerinden bağlanacak.
                value={fullName ?? EMPTY}
                onClick={() => {
                  // TODO(backend/route): Ad-Soyad düzenleme ucu ve ekranı YOK.
                }}
              />
              <InfoRow
                label="Kullanıcı Adı"
                // TODO(backend): @kullanıcıadı alanı YOK.
                value={username ? `@${username}` : EMPTY}
                onClick={() => {
                  // TODO(backend/route): Kullanıcı adı düzenleme ucu ve ekranı YOK.
                }}
              />
              {/* E-posta salt-okunur: Stitch'te chevron yok. */}
              <InfoRow
                label="E-posta"
                // TODO(backend): E-posta alanı giriş yanıtında dönmüyor.
                value={EMPTY}
                readOnly
              />
            </SectionGroup>

            <SectionGroup title="GÜVENLİK" dividerInset={20} dividerStrong>
              <InfoRow
                variant="action"
                label="Şifre"
                // TODO(backend): Şifrenin son değiştirilme tarihi alanı YOK.
                value={`Son değiştirilme: ${EMPTY}`}
                onClick={() => {
                  // TODO(route): Şifre değiştirme ekranı projede YOK — uydurulmadı.
                  // Backend'de yalnızca /api/auth/forgot-password var.
                }}
              />
              <InfoRow
                variant="action"
                label="İki Adımlı Doğrulama"
                // TODO(backend): 2FA durumu alanı YOK.
                value={EMPTY}
                onClick={() => {
                  // TODO(route): İki Adımlı Doğrulama ekranı projede YOK.
                }}
              />
            </SectionGroup>

            <SectionGroup title="ÜYELİK" dividerInset={20} dividerStrong>
              <InfoRow
                variant="action"
                label="FORMAX Premium"
                // GERÇEK veri: GET /api/users/me/subscription → accessLevel.
                value={subscription ? `${subscription.accessLevel} Plan` : EMPTY}
                trailing={
                  /* Lime aksan tavanı #CCFF00; "daha parlak" his yumuşak bir
                     neon parıltıyla verilir (palet değişmez). */
                  <span className="shrink-0 text-[13px] font-bold uppercase leading-none tracking-[0.5px] text-profile-accent [text-shadow:0_0_12px_rgba(204,255,0,.55)]">
                    Yükselt
                  </span>
                }
                onClick={() => {
                  // TODO(route): Premium yükseltme ekranı ve ödeme ucu YOK.
                }}
              />
            </SectionGroup>
          </div>

          <div className="mt-6">
            <DangerCard
              onClick={() => {
                // TODO(backend): Hesap silme ucu YOK (AuthController: register /
                // login / forgot-password). Uç eklenene kadar aksiyon yok;
                // onay akışı da uç tanımlandığında eklenecek.
              }}
            />
          </div>

          <p className="mt-8 px-4 text-center text-[12px] leading-none text-profile-chevron">
            {VERSION_TEXT}
          </p>
        </main>
      </div>
    </div>
  );
}
