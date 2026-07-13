import type { HeroPlayerVM } from "./heroData";

interface Props {
  player: HeroPlayerVM;
  side: "left" | "right";
}

/**
 * FORMAX · PlayerSpotlight (05) — Left/Right tek parametrik component (reusable).
 * Gerçek oyuncu fotoğrafı yok → takım-tonu parıltı + silhouette placeholder + ad/form.
 * PNG'deki oyuncu ölçek/konumu (alt köşeye yaslı, dış tarafta) korunur.
 */
export function PlayerSpotlight({ player, side }: Props) {
  const isLeft = side === "left";

  // Referans: oyuncular kartın ANA görsel kütlesi (sol/sağ yarıyı dolduran büyük figür).
  // Aydınlatma StadiumBackground atmosfer yığınında. Burası "Players" katmanı: büyük
  // placeholder figür + ad/form. Gerçek foto gelince SADECE bu figür <img> ile değişir;
  // footprint (konum/ölçek) referansla aynı kalır → layout değişmez.
  const figureFill = player.tone === "blue" ? "rgba(120,180,255,1)" : "rgba(255,120,125,1)";

  return (
    <div
      className={`pointer-events-none absolute bottom-0 h-[86%] w-[62%] ${isLeft ? "left-0" : "right-0"}`}
    >
      {player.photoUrl ? (
        // Gerçek oyuncu fotoğrafı (HeroSelectionEngine → HomeHero/AwayHero)
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={player.photoUrl}
          alt={`${player.firstName} ${player.lastName}`}
          className={`absolute bottom-0 h-full w-full object-contain ${
            isLeft ? "object-left-bottom" : "object-right-bottom"
          }`}
        />
      ) : (
        // Foto yoksa: büyük placeholder figür — referans footprint'i doldurur
        <svg
          viewBox="0 0 100 150"
          preserveAspectRatio={isLeft ? "xMinYMax meet" : "xMaxYMax meet"}
          className="absolute bottom-0 h-full w-full opacity-[0.42] blur-[2px]"
          style={{
            maskImage: "linear-gradient(to top, black 30%, transparent 88%)",
            WebkitMaskImage: "linear-gradient(to top, black 30%, transparent 88%)",
            transform: isLeft ? "none" : "scaleX(-1)",
          }}
          aria-hidden
        >
          <defs>
            <linearGradient id={`pl-${side}`} x1="0" y1="1" x2="0" y2="0">
              <stop offset="0%" stopColor={figureFill} stopOpacity="0.95" />
              <stop offset="100%" stopColor={figureFill} stopOpacity="0.35" />
            </linearGradient>
          </defs>
          {/* Baş */}
          <circle cx="52" cy="24" r="14" fill={`url(#pl-${side})`} />
          {/* Omuz + gövde (aşağı doğru genişleyen atletik figür) */}
          <path d="M30 150 C28 96 34 60 52 60 C70 60 76 96 74 150 Z" fill={`url(#pl-${side})`} />
          {/* Dış kol */}
          <path d="M30 66 C18 74 14 100 18 132 L28 130 C26 104 30 84 38 74 Z" fill={`url(#pl-${side})`} />
        </svg>
      )}
    </div>
  );
}
