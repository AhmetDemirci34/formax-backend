import type { ReactNode } from "react";

/**
 * PredictionSection — kategori başlığı (renkli nokta + başlık) + sağda sayaç.
 */
export function PredictionSection({
  title,
  count,
  tone,
  children,
}: {
  title: string;
  count: number;
  tone: "active" | "pending" | "finished";
  children: ReactNode;
}) {
  const dot =
    tone === "active" ? "bg-goalai-accent" : tone === "pending" ? "bg-signal-amber" : "bg-text-muted";

  return (
    <section className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2 text-[12px] font-bold uppercase tracking-wide text-text-primary">
          <span className={`h-2 w-2 rounded-full ${dot}`} />
          {title}
        </span>
        <span className="text-[11px] font-medium text-text-muted">{count} Tahmin</span>
      </div>
      <div className="flex flex-col gap-2.5">{children}</div>
    </section>
  );
}
