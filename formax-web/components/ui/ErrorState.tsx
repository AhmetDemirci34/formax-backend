interface Props {
  message?: string;
  onRetry?: () => void;
}

export function ErrorState({
  message = "Veri yüklenemedi.",
  onRetry,
}: Props) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-text-muted">
      <div className="text-3xl">⚠</div>
      <p className="text-sm text-center max-w-xs">{message}</p>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-2 px-4 py-2 text-sm rounded-lg bg-bg-elevated text-text-secondary hover:bg-bg-hover transition-colors"
        >
          Tekrar Dene
        </button>
      )}
    </div>
  );
}
