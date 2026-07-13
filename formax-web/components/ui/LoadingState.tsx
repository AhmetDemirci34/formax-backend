interface Props {
  label?: string;
}

export function LoadingState({ label = "Yükleniyor..." }: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-text-muted">
      <div className="w-8 h-8 rounded-full border-2 border-border border-t-accent animate-spin" />
      <span className="text-sm">{label}</span>
    </div>
  );
}

export function LoadingCard() {
  return (
    <div className="bg-bg-card rounded-xl p-4 animate-pulse space-y-3">
      <div className="flex justify-between items-center">
        <div className="h-3 w-24 bg-bg-elevated rounded" />
        <div className="h-3 w-16 bg-bg-elevated rounded" />
      </div>
      <div className="h-5 w-full bg-bg-elevated rounded" />
      <div className="h-4 w-3/4 bg-bg-elevated rounded" />
      <div className="h-3 w-1/2 bg-bg-elevated rounded" />
    </div>
  );
}
