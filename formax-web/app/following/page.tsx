"use client";

import { useState } from "react";
import { AnimatePresence } from "framer-motion";
import { AppShell } from "@/components/layout/AppShell";
import { FollowHeader } from "@/components/follow/FollowHeader";
import { FeedView } from "@/components/follow/FeedView";
import { ManagementView } from "@/components/follow/ManagementView";
import type { ViewMode, ManageFilter } from "@/components/follow/types";

/**
 * Takip (Follow) — tek sayfa, iki görünüm (SPA): Activity Feed + Follow Management.
 * `viewMode` state'i ile yönetilir; yeni route yok. Management görünümü tam ekran
 * overlay olarak açılır ve Bottom Navigation'ı örter (Teknik Doküman §3).
 * Gerçek kaynaklar: /api/notifications/me, /api/users/me/teams, /api/follows/me.
 */
export default function FollowingPage() {
  const [viewMode, setViewMode] = useState<ViewMode>("feed");
  const [manageFilter, setManageFilter] = useState<ManageFilter>("all");

  const openManagement = (f: ManageFilter) => {
    setManageFilter(f);
    setViewMode("management");
  };

  return (
    <>
      <AppShell header={<FollowHeader />}>
        <FeedView onOpenManagement={openManagement} />
      </AppShell>

      <AnimatePresence>
        {viewMode === "management" && (
          <ManagementView initialFilter={manageFilter} onBack={() => setViewMode("feed")} />
        )}
      </AnimatePresence>
    </>
  );
}
