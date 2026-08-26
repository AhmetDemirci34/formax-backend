import { InfoIcon } from "@/components/discover/icons";
import { BellOutlineIcon } from "./icons";

/**
 * InformationCard — bildirimlerin çalışma mantığını anlatan sabit bilgi kutusu
 * (Stitch görseli): solda lime info ikonu, ortada metin, sağda büyük çan görseli.
 * Etkileşimli değildir.
 */
export function InformationCard() {
  return (
    <div className="mx-4 flex items-center gap-3 overflow-hidden rounded-xl bg-profile-container px-4 py-4">
      {/* Lime "i" — kendi dairesel zemininde (Stitch'te ikon çevrelenmiş görünür) */}
      <span className="flex h-7 w-7 shrink-0 items-center justify-center self-start rounded-full bg-profile-accent/10 text-profile-accent">
        <InfoIcon size={16} />
      </span>

      <p className="min-w-0 flex-1 text-[13px] leading-[1.45] text-white">
        Takip ettiğin içerik açık olduğu sürece o içerikle ilgili önemli gelişmeler sana
        otomatik olarak bildirilir.
      </p>

      {/* Büyük çan illüstrasyonu — tam lime, ses dalgalarıyla */}
      <BellOutlineIcon size={52} className="shrink-0 text-profile-accent" aria-hidden />
    </div>
  );
}
