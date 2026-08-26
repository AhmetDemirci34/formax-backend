"use client";

// ─────────────────────────────────────────────────────────────────────────────
// FORMAX Sessiz Saatler — SCREEN_07  (/notifications/settings/quiet-hours)
//
// Bildirim Tercihleri'nin (SCREEN_04) ALT ekranı: "Sessiz Saatler" satırından
// push ile açılır, header'daki geri butonu SCREEN_04'e döner.
// Kullanıcı bildirimlerin susturulacağı saat aralığını yönetir; her değişiklik
// otomatik kaydedilir (Kaydet butonu yoktur).
//
// Tek doğruluk kaynağı: Stitch görseli. Alt navigasyon görselde vardır → global
// BottomNav gizlenmez.
// ─────────────────────────────────────────────────────────────────────────────

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";

import { useQuietHours } from "@/hooks/useQuietHours";
import { useScrolledPast, findScrollParent } from "@/hooks/useScrolledPast";

import { hankenGrotesk } from "@/components/profile/fonts";
import { QuietHoursHeader } from "@/components/quiet-hours/QuietHoursHeader";
import { QuietHoursSettingsCard } from "@/components/quiet-hours/QuietHoursSettingsCard";
import { QuietHoursInfoCard } from "@/components/quiet-hours/QuietHoursInfoCard";

export default function QuietHoursPage() {
  const router = useRouter();
  const rootRef = useRef<HTMLDivElement>(null);

  const { enabled, start, end, setEnabled, setStart, setEnd } = useQuietHours();
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
      {/* Push (sağdan sola) giriş — Profil modülüyle aynı reçete. */}
      <div className="profile-page-enter flex min-h-[100dvh] flex-col">
        <QuietHoursHeader
          scrolled={scrolled}
          onBack={() => router.push("/notifications/settings")}
        />

        <main className="profile-content-enter flex-1 pb-[calc(var(--bottom-nav-height)+var(--safe-bottom))] pt-4">
          <div className="flex flex-col gap-4">
            <QuietHoursSettingsCard
              enabled={enabled}
              start={start}
              end={end}
              onToggle={setEnabled}
              onStartChange={setStart}
              onEndChange={setEnd}
            />
            <QuietHoursInfoCard />
          </div>
        </main>
      </div>
    </div>
  );
}
