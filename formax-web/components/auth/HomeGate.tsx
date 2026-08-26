"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/context/AuthContext";
import { AUTH_GATE_ENABLED } from "@/lib/auth/authGate";

/**
 * FORMAX · HomeGate — kök (/) rotası için client-side auth kapısı.
 *
 * ⚠️ ŞU AN PASİF: `AUTH_GATE_ENABLED = false` olduğu sürece kapı hiç devreye girmez,
 * Keşfet doğrudan açılır (UI bitene kadar geçici). Geri açmak için tek nokta:
 * `lib/auth/authGate.ts`.
 *
 * Ürün akışı: Splash → Login → Keşfet. Token localStorage'ta tutulduğundan (server
 * middleware localStorage okuyamaz) guard client-side'dır — mevcut `onboarding/teams`
 * desenIYLE aynı, yeni mimari getirmez.
 *
 *  • auth henüz hydrate olmadıysa  → Splash (kararı bilmiyoruz)
 *  • hydrate + giriş yoksa          → /auth/login (replace: geri tuşu / 'ye dönmesin)
 *  • hydrate + giriş varsa          → children (Keşfet)
 *
 * Recommendation/Discovery/Follow/Notification backend'ine dokunmaz; yalnız açılış
 * yönlendirmesini ürün tasarımına geri getirir.
 */
export function HomeGate({ children }: { children: React.ReactNode }) {
  const { isHydrated, isLoggedIn } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!AUTH_GATE_ENABLED) return;
    if (isHydrated && !isLoggedIn) {
      router.replace("/auth/login");
    }
  }, [isHydrated, isLoggedIn, router]);

  // Kapı pasifken Splash/redirect yok — ekran doğrudan açılır.
  if (!AUTH_GATE_ENABLED) {
    return <>{children}</>;
  }

  if (!isHydrated || !isLoggedIn) {
    return <Splash />;
  }

  return <>{children}</>;
}

/** Minimal markalı splash — hydrate/redirect anında gösterilir (ayrı rota yok). */
function Splash() {
  return (
    <div className="flex h-[100dvh] w-full flex-col items-center justify-center bg-bg-deep">
      <div className="animate-pulse text-center leading-none">
        <span className="text-[32px] font-black tracking-[-0.01em] text-text-primary">
          FORMA<span className="text-neon">X</span>
        </span>
        <p className="mt-2 text-[10px] font-semibold uppercase tracking-[0.13em] text-text-secondary">
          Futbolu Anlayan Zekâ
        </p>
      </div>
    </div>
  );
}
