// Discover — tek, monokrom SVG ikon seti. currentColor + ince stroke (premium, emoji yok).
// Tek kaynak: tüm Discover ikonları buradan gelir, böylece çizgi dili tutarlı kalır.
import type { SVGProps } from "react";

type IconProps = SVGProps<SVGSVGElement> & { size?: number };

function Base({ size = 16, children, ...rest }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.7}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      {...rest}
    >
      {children}
    </svg>
  );
}

export function SparklesIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M12 3l1.6 4.6L18 9.2l-4.4 1.6L12 15l-1.6-4.2L6 9.2l4.4-1.6z" />
      <path d="M18 14l.7 1.9L20.6 16.6l-1.9.7L18 19l-.7-1.7L15.4 16.6l1.9-.7z" />
    </Base>
  );
}

export function CalendarIcon(p: IconProps) {
  return (
    <Base {...p}>
      <rect x="3.5" y="5" width="17" height="15" rx="2.5" />
      <path d="M3.5 9.5h17M8 3.5v3M16 3.5v3" />
    </Base>
  );
}

export function ArrowRightIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M5 12h14M13 6l6 6-6 6" />
    </Base>
  );
}

export function InfoIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 11v5M12 8v.5" />
    </Base>
  );
}

export function TicketIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 8a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2 2 2 0 0 0 0 4 2 2 0 0 1-2 2H6a2 2 0 0 1-2-2 2 2 0 0 0 0-4z" />
      <path d="M14 6v2M14 11v2M14 16v0" />
    </Base>
  );
}

export function FilterIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 7h16M7 12h10M10 17h4" />
    </Base>
  );
}

export function MenuIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </Base>
  );
}

export function SearchIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="11" cy="11" r="7" />
      <path d="M20 20l-3.5-3.5" />
    </Base>
  );
}

export function BellIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M6 9a6 6 0 0 1 12 0c0 4 1.5 5.5 2 6H4c.5-.5 2-2 2-6z" />
      <path d="M10 19a2 2 0 0 0 4 0" />
    </Base>
  );
}

export function StarIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M12 3.5l2.6 5.3 5.8.8-4.2 4.1 1 5.8L12 17.8 6.8 19.5l1-5.8L3.6 9.6l5.8-.8z" />
    </Base>
  );
}

export function FlameIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M12 3c1 2.5.3 4-1 5.5C9.5 10.2 8 11.5 8 14a4 4 0 1 0 8 0c0-1.6-.6-2.7-1.3-3.7.8.3 1.3.9 1.3.9-.2-3-2-5.2-4-9z" />
    </Base>
  );
}

export function HeartIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M12 20s-7-4.3-9.2-8.5C1.4 8.8 2.7 5.6 5.8 5.6c1.9 0 3.2 1.1 4.2 2.4 1-1.3 2.3-2.4 4.2-2.4 3.1 0 4.4 3.2 3 5.9C19 15.7 12 20 12 20z" />
    </Base>
  );
}

export function UsersIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M3.5 19a5.5 5.5 0 0 1 11 0" />
      <path d="M16 5.5a3 3 0 0 1 0 5.8M17 19a5.5 5.5 0 0 0-2.3-4.5" />
    </Base>
  );
}

export function ActivityIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M3 12h3l2.5-6 4 13 2.5-7H21" />
    </Base>
  );
}

export function TrophyIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M7 4h10v4a5 5 0 0 1-10 0z" />
      <path d="M7 6H4.5a2.5 2.5 0 0 0 2.5 3M17 6h2.5a2.5 2.5 0 0 1-2.5 3" />
      <path d="M12 13v3M9 20h6M10 20v-1.5a2 2 0 0 1 4 0V20" />
    </Base>
  );
}

export function ShieldIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M12 3.2l7 2.4v5c0 4.4-3 7.6-7 9.2-4-1.6-7-4.8-7-9.2v-5z" />
      <path d="M9.2 12.2l2 2 3.6-4" />
    </Base>
  );
}

export function TargetIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="8.5" />
      <circle cx="12" cy="12" r="4.5" />
      <circle cx="12" cy="12" r="1" fill="currentColor" stroke="none" />
    </Base>
  );
}

export function GoalIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7.5l2.6 1.9-1 3h-3.2l-1-3z" />
      <path d="M12 7.5V4M14.6 9.4l3-1.2M13.6 12.4l1.9 2.6M10.4 12.4l-1.9 2.6M9.4 9.4l-3-1.2" />
    </Base>
  );
}

export function ChevronRightIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M9 5l7 7-7 7" />
    </Base>
  );
}

export function HomeIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 11l8-6.5L20 11" />
      <path d="M6 10v9h12v-9" />
    </Base>
  );
}

export function TrendingUpIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M3 16l5-5 3.5 3.5L20 7" />
      <path d="M15 7h5v5" />
    </Base>
  );
}

export function PulseIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M3 12h3l2-6 4 13 2.5-7H21" />
    </Base>
  );
}

export function GlobeIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3c2.5 2.4 3.8 5.6 3.8 9S14.5 18.6 12 21c-2.5-2.4-3.8-5.6-3.8-9S9.5 5.4 12 3z" />
    </Base>
  );
}

export function SettingsIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3v2.5M12 18.5V21M4.2 7l2.1 1.2M17.7 15.8l2.1 1.2M4.2 17l2.1-1.2M17.7 8.2l2.1-1.2" />
    </Base>
  );
}

export function LifeBuoyIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="3.5" />
      <path d="M4.5 4.5l5 5M14.5 14.5l5 5M19.5 4.5l-5 5M9.5 14.5l-5 5" />
    </Base>
  );
}

export function CrownIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 8l3.5 3L12 5l4.5 6L20 8l-1.5 10h-13z" />
      <path d="M5.5 18h13" />
    </Base>
  );
}

export function NewspaperIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M4 5h13v14H5a2 2 0 0 1-2-2V7" />
      <path d="M17 9h3v8a2 2 0 0 1-2 2M7 8h6M7 11.5h6M7 15h4" />
    </Base>
  );
}

export function UserIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="8" r="3.6" />
      <path d="M5 20a7 7 0 0 1 14 0" />
    </Base>
  );
}

export function RadarIcon(p: IconProps) {
  return (
    <Base {...p}>
      <circle cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
      <path d="M8.5 8.5a5 5 0 0 0 0 7M15.5 8.5a5 5 0 0 1 0 7" />
      <path d="M6 6a9 9 0 0 0 0 12M18 6a9 9 0 0 1 0 12" />
    </Base>
  );
}

export function ClipboardListIcon(p: IconProps) {
  // Tahminlerim — pano / kontrol listesi
  return (
    <Base {...p}>
      <rect x="6" y="4" width="12" height="17" rx="2" />
      <path d="M9 5.5h6" />
      <path d="M9.5 11l1.6 1.6 3.4-3.6M9.5 16.5h5" />
    </Base>
  );
}

export function CheckIcon(p: IconProps) {
  return (
    <Base {...p}>
      <path d="M5 12.5l4.5 4.5L19 6" />
    </Base>
  );
}

// Rozet anahtarı → ikon eşlemesi (cardSignals'tan gelen iconKey için).
export const ICONS = {
  sparkles: SparklesIcon,
  star: StarIcon,
  flame: FlameIcon,
  heart: HeartIcon,
  users: UsersIcon,
  activity: ActivityIcon,
  trophy: TrophyIcon,
  shield: ShieldIcon,
  target: TargetIcon,
  goal: GoalIcon,
  home: HomeIcon,
  trendingUp: TrendingUpIcon,
  pulse: PulseIcon,
} as const;

export type IconKey = keyof typeof ICONS;
