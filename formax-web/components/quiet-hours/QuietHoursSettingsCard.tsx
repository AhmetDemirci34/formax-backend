"use client";

import { NotificationToggle } from "@/components/notification-settings/NotificationToggle";
import { MoonIcon } from "@/components/notification-settings/icons";
import { TimeRow } from "./TimeRow";

interface QuietHoursSettingsCardProps {
  enabled: boolean;
  start: string;
  end: string;
  onToggle: (next: boolean) => void;
  onStartChange: (next: string) => void;
  onEndChange: (next: string) => void;
}

/** Satırlar arası ayırıcı — ikon sütunundan sonra (52px) içeride. */
function Divider() {
  return <div className="ml-[52px] h-px bg-[var(--profile-divider)]" role="presentation" />;
}

/**
 * QuietHoursSettingsCard — üç satırlı ayar kartı (Stitch görseli):
 *   1) Sessiz Saatleri Aç  → ay ikonu + başlık + toggle (72px)
 *   2) Başlangıç Saati      → saat ikonu + başlık + değer + chevron (64px)
 *   3) Bitiş Saati          → aynı (64px)
 *
 * Kart: 16px yanlarda içeride, radius 16, bg #1A1B1F, köşesiz satırlar
 * (`overflow-hidden`). Son satırdan sonra ayırıcı yoktur.
 */
export function QuietHoursSettingsCard({
  enabled,
  start,
  end,
  onToggle,
  onStartChange,
  onEndChange,
}: QuietHoursSettingsCardProps) {
  return (
    <div className="mx-4 overflow-hidden rounded-2xl bg-profile-container">
      {/* 1) Sessiz Saatleri Aç */}
      <div className="flex h-[72px] items-center gap-3 px-4">
        <span className="flex h-6 w-6 shrink-0 items-center justify-center text-profile-muted">
          <MoonIcon size={20} />
        </span>
        <span className="flex-1 text-[16px] font-medium leading-none text-white">
          Sessiz Saatleri Aç
        </span>
        <NotificationToggle checked={enabled} onChange={onToggle} label="Sessiz Saatleri Aç" />
      </div>

      <Divider />

      {/* 2) Başlangıç Saati */}
      <TimeRow title="Başlangıç Saati" value={start} onChange={onStartChange} />

      <Divider />

      {/* 3) Bitiş Saati */}
      <TimeRow title="Bitiş Saati" value={end} onChange={onEndChange} />
    </div>
  );
}
