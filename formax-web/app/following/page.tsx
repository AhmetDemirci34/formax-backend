"use client";

import { AppShell } from "@/components/layout/AppShell";
import { FollowHeader } from "@/components/follow/FollowHeader";
import { FollowedMatchesScreen } from "@/components/follow/FollowedMatchesScreen";

/**
 * TAKİP ETTİĞİM MAÇLAR — sadeleştirilmiş tek görünüm (06.09.2026).
 *
 * ÖNCESİ: iki görünümlü bir SPA (Activity Feed + Follow Management) — TÜMÜ/MAÇLAR/
 * TAKIMLAR sekmeleri, "Yeni Gelişmeler" akışı, Takımlar ve Ligler kartları ve
 * istatistik kutuları. Bu ekran, kullanıcının TAKİP ETMEDİĞİ içerikleri de
 * gösterdiği için "takip" adını taşımıyordu.
 *
 * SONRASI: yalnız kullanıcının gerçekten takip ettiği maçlar; iki sade bölüm
 * (YAKLAŞAN / TAMAMLANAN). Takım ve lig takipleri backend'de DURUYOR — başka
 * yüzeyler kullanmaya devam ediyor; bu ekran onları göstermiyor.
 *
 * Yönetim görünümü (ManagementView) ve akış bileşenleri (FeedView, SummaryCards)
 * SİLİNMEDİ: başka rotalardan çağrılabilir durumdalar ve destructive bir kaldırma
 * bu görevin kapsamı değildir.
 */
export default function FollowingPage() {
  return (
    <AppShell header={<FollowHeader />}>
      <FollowedMatchesScreen />
    </AppShell>
  );
}
