import { UserIcon } from "@/components/discover/icons";

/**
 * FollowHeader — Feed görünümü başlığı (F marka + "TAKİP" + alt metin + profil).
 * AppShell'in sticky üst bölgesine verilir.
 */
export function FollowHeader() {
  return (
    <div className="flex items-center justify-between gap-3 px-4 pb-2 pt-3">
      <div className="flex items-center gap-2.5">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-goalai-accent text-[18px] font-black italic text-[#0a0e16]">
          F
        </span>
        <div className="leading-tight">
          {/* BAŞLIK EKRANIN KAPSAMINI SÖYLER. Eski alt metin ("son gelişmeler")
              artık gösterilmeyen bir akışa işaret ediyordu. */}
          <h1 className="text-[19px] font-bold uppercase tracking-tight text-text-primary">
            Takip Ettiğim Maçlar
          </h1>
        </div>
      </div>
      <span className="flex h-9 w-9 items-center justify-center rounded-full bg-goalai-surface-bright ring-1 ring-white/10">
        <UserIcon size={18} className="text-text-secondary" />
      </span>
    </div>
  );
}
