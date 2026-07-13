interface TeamLite {
  name: string;
  logoUrl?: string | null;
}

import type { ReactNode } from "react";

interface Props {
  home: TeamLite;
  away: TeamLite;
  size?: number;
  /** Aradaki "VS" gösterilsin mi. */
  showVs?: boolean;
  /** Armaların altında takım isimleri (referans Hero: arma + altında isim). */
  showNames?: boolean;
  /** İki armanın arasına yerleşen slot (referans Hero: VS yerine AI Güven halkası). */
  center?: ReactNode;
}

/**
 * FORMAX · TeamLogoPair (05, shared)
 * İki takım logosu + araya "VS". Gerçek logo yoksa nötr YUVARLAK logo placeholder
 * (takım adı/monogram değil) gösterilir. Hero, Combo, Featured kullanır.
 * `showNames` = referans Hero'da armaların altında takım isimleri (arma ile hizalı).
 */
export function TeamLogoPair({
  home,
  away,
  size = 44,
  showVs = true,
  showNames = false,
  center,
}: Props) {
  if (!showNames) {
    return (
      <div className="flex items-center gap-3">
        <RoundLogo team={home} size={size} />
        {showVs ? (
          <span className="text-[11px] font-black uppercase tracking-wider text-text-secondary">VS</span>
        ) : null}
        <RoundLogo team={away} size={size} />
      </div>
    );
  }

  return (
    <div className="flex items-start justify-center gap-3">
      <div className="flex flex-col items-center gap-2" style={{ width: size + 30 }}>
        <RoundLogo team={home} size={size} />
        <span className="text-center text-[13px] font-extrabold uppercase leading-tight tracking-[0.02em] text-text-primary">
          {home.name}
        </span>
      </div>
      {center ? (
        <div className="shrink-0" style={{ marginTop: -6 }}>
          {center}
        </div>
      ) : showVs ? (
        <span
          className="text-[11px] font-black uppercase tracking-wider text-text-secondary"
          style={{ marginTop: size / 2 - 7 }}
        >
          VS
        </span>
      ) : null}
      <div className="flex flex-col items-center gap-2" style={{ width: size + 30 }}>
        <RoundLogo team={away} size={size} />
        <span className="text-center text-[13px] font-extrabold uppercase leading-tight tracking-[0.02em] text-text-primary">
          {away.name}
        </span>
      </div>
    </div>
  );
}

function RoundLogo({ team, size }: { team: TeamLite; size: number }) {
  if (team.logoUrl) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={team.logoUrl}
        alt={team.name}
        width={size}
        height={size}
        className="rounded-full bg-white/95 object-contain shadow-[0_4px_16px_rgba(0,0,0,0.45)]"
        style={{ width: size, height: size }}
      />
    );
  }

  // Yuvarlak logo placeholder — nötr disk + iç halka (blank arma hissi).
  return (
    <span
      role="img"
      aria-label={team.name}
      className="relative grid place-items-center rounded-full bg-gradient-to-b from-white/95 to-white/70 shadow-[0_4px_16px_rgba(0,0,0,0.45)] ring-1 ring-black/10"
      style={{ width: size, height: size }}
    >
      <span
        className="rounded-full border-2 border-bg-deep/15"
        style={{ width: size * 0.5, height: size * 0.5 }}
      />
    </span>
  );
}
