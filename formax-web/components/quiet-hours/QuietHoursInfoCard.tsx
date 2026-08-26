import { InfoFilledIcon, BellOutlineIcon } from "@/components/notification-settings/icons";

/**
 * QuietHoursInfoCard — özelliğin kapsamını anlatan bilgi kartı (Stitch görseli):
 * sol üstte dolu amber info rozeti, ortada gri açıklama metni, sağ altta soluk
 * lime çan illüstrasyonu (metnin arkasına taşan). Etkileşimli değildir.
 */
export function QuietHoursInfoCard() {
  return (
    <div className="relative mx-4 overflow-hidden rounded-2xl bg-profile-container px-4 py-4">
      {/* Sağ altta taşan soluk çan illüstrasyonu */}
      <BellOutlineIcon
        size={92}
        className="pointer-events-none absolute -bottom-3 right-1 text-profile-accent/25"
        aria-hidden
      />

      <div className="relative flex gap-3">
        <InfoFilledIcon size={20} className="mt-0.5 shrink-0 text-signal-amber" />
        <p className="min-w-0 flex-1 text-[13px] leading-[1.5] text-profile-muted">
          Sessiz saatler açıkken, belirlediğiniz başlangıç ve bitiş saatleri arasında
          uygulamaya hiçbir bildirim almazsınız. Bu süre zarfında oluşan bildirimler, sessiz
          saatler bittiğinde size iletilir.
        </p>
      </div>
    </div>
  );
}
