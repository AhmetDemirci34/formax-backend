import type { NabizFeedItemDto } from "@/types/api";

interface Props {
  items: NabizFeedItemDto[];
}

const TYPE_ICON: Record<string, string> = {
  News:       "📰",
  Twitter:    "🐦",
  Instagram:  "📸",
  Forum:      "💬",
  Official:   "📢",
};

function timeAgo(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 60) return `${mins}d önce`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}s önce`;
  return `${Math.floor(hours / 24)}g önce`;
}

export function NabizFeed({ items }: Props) {
  if (!items?.length) return null;

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Nabız
      </h3>
      <div className="space-y-3">
        {items.slice(0, 8).map((item, i) => (
          <div key={i} className="flex gap-3">
            <span className="text-base shrink-0">{TYPE_ICON[item.type] ?? "•"}</span>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-1.5 mb-0.5">
                <span className="text-xs font-semibold text-text-secondary">
                  {item.author}
                </span>
                {item.authorVerified && (
                  <span className="text-formax-green text-xs">✓</span>
                )}
                <span className="text-xs text-text-muted ml-auto shrink-0">
                  {timeAgo(item.publishedAt)}
                </span>
              </div>
              <p className="text-sm text-text-primary leading-snug">{item.headline}</p>
              {item.summary && (
                <p className="text-xs text-text-secondary mt-0.5 line-clamp-2">
                  {item.summary}
                </p>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
