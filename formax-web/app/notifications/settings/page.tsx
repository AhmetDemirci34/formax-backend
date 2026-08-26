"use client";

// ─────────────────────────────────────────────────────────────────────────────
// FORMAX Bildirim Tercihleri — SCREEN_04  (/notifications/settings)
//
// Profil Merkezi'nin (SCREEN_15 · /profile) ALT ekranı: "Bildirimler" satırından
// push ile açılır, header'daki geri butonu Profil'e döner.
// Bu ekran bildirim GEÇMİŞİ değildir (o /notifications'tadır); yalnızca içerik
// bazlı bildirim TERCİHLERİNİ yönetir. Event tipi (gol/VAR/kırmızı kart) seçimi
// MVP'de YOKTUR — kullanıcı sadece hangi içerikten bildirim alacağını seçer.
//
// Tek doğruluk kaynağı: Stitch görseli (Developer Handoff yalnızca açıklayıcı).
// Alt navigasyon görselde vardır → global BottomNav gizlenmez.
// ─────────────────────────────────────────────────────────────────────────────

import { useEffect, useRef, type ReactNode } from "react";
import { useRouter } from "next/navigation";

import { useFollowedMatches } from "@/hooks/useFollow";
import { useMyTeams } from "@/hooks/useTeams";
import { useMyLeagues } from "@/hooks/useLeagues";
import { useNotificationPreferences } from "@/hooks/useNotificationPreferences";
import { useScrolledPast, findScrollParent } from "@/hooks/useScrolledPast";

import { hankenGrotesk } from "@/components/profile/fonts";
import { SectionGroup } from "@/components/profile/SectionGroup";
import { NotificationSettingsHeader } from "@/components/notification-settings/NotificationSettingsHeader";
import { NotificationItem } from "@/components/notification-settings/NotificationItem";
import {
  FollowedContentEmpty,
  FollowedContentSkeleton,
} from "@/components/notification-settings/FollowedContentStates";
import { InformationCard } from "@/components/notification-settings/InformationCard";
import { QuietHoursCard } from "@/components/notification-settings/QuietHoursCard";
import { MatchGlyphIcon, MegaphoneIcon } from "@/components/notification-settings/icons";
import { ShieldIcon, TrophyIcon, SparklesIcon } from "@/components/discover/icons";


/** Takım/maç adından dairesel görsel için baş harf üretir (gerçek veriden). */
function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("")
    .toLocaleUpperCase("tr");
}

export default function NotificationSettingsPage() {
  const router = useRouter();
  const rootRef = useRef<HTMLDivElement>(null);

  // GERÇEK takip verisi — mevcut uçlar, yeni servis uydurulmadı.
  const { data: matches = [], isLoading: matchesLoading } = useFollowedMatches();
  const { data: teams = [], isLoading: teamsLoading } = useMyTeams();
  const { data: leagues = [], isLoading: leaguesLoading } = useMyLeagues();

  const followedLoading = matchesLoading || teamsLoading || leaguesLoading;
  const followedCount = matches.length + teams.length + leagues.length;

  const { isEnabled, setEnabled } = useNotificationPreferences();

  /** Takip edilen içerik satırları — maç → takım → lig sırasıyla. */
  const followedRows: ReactNode[] = [
    ...matches.map((m) => {
      const key = `match:${m.matchId}`;
      return (
        <NotificationItem
          key={key}
          leading={
            <span className="text-[10px] font-bold text-profile-muted">
              {initials(m.homeTeam)}
            </span>
          }
          title={`${m.homeTeam} - ${m.awayTeam}`}
          category="Maç"
          categoryIcon={<MatchGlyphIcon size={11} />}
          checked={isEnabled(key)}
          onChange={(v) => setEnabled(key, v)}
        />
      );
    }),
    ...teams.map((t) => {
      const key = `team:${t.id}`;
      return (
        <NotificationItem
          key={key}
          leading={
            <span className="text-[10px] font-bold text-profile-muted">{initials(t.name)}</span>
          }
          title={t.name}
          category="Takım"
          categoryIcon={<ShieldIcon size={11} />}
          checked={isEnabled(key)}
          onChange={(v) => setEnabled(key, v)}
        />
      );
    }),
    ...leagues.map((l) => {
      const key = `league:${l.id}`;
      return (
        <NotificationItem
          key={key}
          leading={<TrophyIcon size={16} className="text-profile-muted" />}
          title={l.name}
          category="Lig"
          categoryIcon={<TrophyIcon size={11} />}
          checked={isEnabled(key)}
          onChange={(v) => setEnabled(key, v)}
        />
      );
    }),
  ];

  /**
   * Takip listesinin durumu: yükleniyorsa shimmer, hiç takip edilen içerik
   * yoksa boş mesaj. Dolu ise `null` döner ve satırların kendisi kullanılır.
   */
  const followedState: ReactNode | null = followedLoading ? (
    <FollowedContentSkeleton key="followed-loading" />
  ) : followedCount === 0 ? (
    <FollowedContentEmpty key="followed-empty" />
  ) : null;

  /**
   * FORMAX içerikleri — kullanıcının TAKİP ETTİĞİ içerikler değil, FORMAX'ın
   * global bildirim kategorileridir. Bu yüzden takip listesi boş olsa da
   * (ya da yüklenirken de) HER ZAMAN görünürler; boş durum yalnızca maç /
   * takım / lig listesini kapsar.
   */
  const formaxRows: ReactNode[] = [
    <NotificationItem
      key="formax:ai-combo"
      leading={<SparklesIcon size={16} className="text-profile-accent" />}
      title="Günün AI Kombini"
      category="FORMAX"
      categoryIcon={<SparklesIcon size={11} />}
      checked={isEnabled("formax:ai-combo")}
      onChange={(v) => setEnabled("formax:ai-combo", v)}
    />,
    <NotificationItem
      key="formax:system"
      leading={<MegaphoneIcon size={16} className="text-profile-accent" />}
      title="Sistem Duyuruları"
      category="FORMAX"
      categoryIcon={<MegaphoneIcon size={11} />}
      checked={isEnabled("formax:system")}
      onChange={(v) => setEnabled("formax:system", v)}
    />,
  ];

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
        <NotificationSettingsHeader scrolled={scrolled} onBack={() => router.push("/profile")} />

        <main className="profile-content-enter flex-1 pb-[calc(var(--bottom-nav-height)+var(--safe-bottom))] pt-5">
          <div className="flex flex-col gap-6">
            <SectionGroup
              title="TAKİP ETTİKLERİM"
              titleTone="accent"
              description="Takip ettiğin maç, takım, lig ve diğer içerikler için bildirim almak istediğini seç."
              dividerInset={16}
              radius={12}
            >
              {/* Takip listesi: shimmer / boş mesaj / satırlar.
                  FORMAX kategorileri her durumda en altta kalır.
                  Çocuklar DİZİ olarak verilir — SectionGroup `Children.toArray`
                  ile dizileri düzleştirip aralarına divider koyar; Fragment tek
                  çocuk sayılacağı için kullanılmaz. */}
              {[...(followedState ? [followedState] : followedRows), ...formaxRows]}
            </SectionGroup>

            <InformationCard />

            <section>
              <h2 className="px-6 pb-2 text-[11px] font-semibold uppercase leading-none tracking-[0.6px] text-profile-accent">
                SESSİZ SAATLER
              </h2>
              <QuietHoursCard
                // TODO(backend): Sessiz saat aralığı alanı YOK. NotificationsController
                // yalnızca me / unread-count / read / read-all sunuyor; tercih tablosu
                // ve DTO'su tanımlı değil. `range` bilinçli olarak VERİLMEZ — yer
                // tutucu gösterilmez. Uç geldiğinde tek yapılacak: range={...}.
                range={undefined}
                // SCREEN_07 — Sessiz Saatler detay ekranı (push).
                onClick={() => router.push("/notifications/settings/quiet-hours")}
              />
            </section>
          </div>
        </main>
      </div>
    </div>
  );
}
