interface Props {
  title?: string;
  message?: string;
}

/**
 * FORMAX · EmptyState — backend gerçek veri döndürmediğinde gösterilir.
 * Mock/placeholder maç ÜRETİLMEZ; kullanıcı yalnızca "şu an içerik yok" mesajı görür.
 */
export function EmptyState({
  title = "Şu an gösterilecek maç yok",
  message = "Yeni maçlar eklendiğinde burada görünecek.",
}: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 py-16 text-center text-text-muted">
      <div className="grid h-12 w-12 place-items-center rounded-full border border-white/10 bg-white/[0.03] text-lg">
        ⌀
      </div>
      <p className="text-sm font-semibold text-text-secondary">{title}</p>
      <p className="max-w-xs text-xs">{message}</p>
    </div>
  );
}
