import { ClipboardListIcon } from "@/components/discover/icons";

/**
 * EmptyState — boş liste durumu (illüstrasyon + metin). Tekil kategori veya
 * tüm liste boş olduğunda gösterilir.
 */
export function EmptyState({ title, message }: { title: string; message: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-white/[0.06] bg-goalai-surface-bright px-6 py-12 text-center">
      <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-white/[0.04] text-text-muted">
        <ClipboardListIcon size={26} />
      </span>
      <p className="text-[15px] font-semibold text-text-primary">{title}</p>
      <p className="max-w-[240px] text-[13px] leading-relaxed text-text-muted">{message}</p>
    </div>
  );
}
