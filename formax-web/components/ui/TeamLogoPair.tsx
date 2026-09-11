import type { ReactNode } from "react";
import { TeamCrest } from "@/components/ui/TeamCrest";

interface TeamLite {
  name: string;
  logoUrl?: string | null;
}

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
 * İki takım arması + araya "VS" veya merkez slot.
 *
 * ARMA KAYNAĞI: ortak TeamCrest component'i → yalnız backend'in gönderdiği logoUrl
 * (HomeTeamLogoUrl / AwayTeamLogoUrl). Statik logo eşlemesi YOK. Görsel yüklenemezse
 * (404/CORS) TeamCrest monogram fallback'ine düşer — buradaki eski yerel placeholder
 * boş bir disk gösteriyordu ve yükleme hatasını hiç yakalamıyordu.
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
        <TeamCrest name={home.name} logoUrl={home.logoUrl} size={size} />
        {showVs ? (
          <span className="text-[11px] font-black uppercase tracking-wider text-text-secondary">VS</span>
        ) : null}
        <TeamCrest name={away.name} logoUrl={away.logoUrl} size={size} />
      </div>
    );
  }

  return (
    <div className="flex items-start justify-center gap-3">
      <div className="flex min-w-0 flex-col items-center gap-2" style={{ width: size + 30 }}>
        <TeamCrest name={home.name} logoUrl={home.logoUrl} size={size} />
        <span className="w-full break-words text-center text-[13px] font-extrabold uppercase leading-tight tracking-[0.02em] text-text-primary">
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
      <div className="flex min-w-0 flex-col items-center gap-2" style={{ width: size + 30 }}>
        <TeamCrest name={away.name} logoUrl={away.logoUrl} size={size} />
        <span className="w-full break-words text-center text-[13px] font-extrabold uppercase leading-tight tracking-[0.02em] text-text-primary">
          {away.name}
        </span>
      </div>
    </div>
  );
}
