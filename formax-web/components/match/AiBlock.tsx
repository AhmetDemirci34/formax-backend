import type { AiDto } from "@/types/api";

interface Props {
  ai: AiDto;
}

const STATE_CONFIG: Record<
  string,
  { icon: string; borderColor: string; labelColor: string; label: string }
> = {
  Extended:     { icon: "◉", borderColor: "border-accent/40",       labelColor: "text-accent",       label: "Analiz Aktif" },
  Short:        { icon: "◎", borderColor: "border-formax-amber/30", labelColor: "text-formax-amber", label: "Sınırlı Veri" },
  SelfRetracted:{ icon: "○", borderColor: "border-border",          labelColor: "text-text-muted",   label: "Geri Çekildi" },
  Silent:       { icon: "○", borderColor: "border-border",          labelColor: "text-text-muted",   label: "Sessiz" },
};

export function AiBlock({ ai }: Props) {
  const cfg = STATE_CONFIG[ai.state] ?? STATE_CONFIG.Silent;

  if (ai.state === "Silent") return null;

  return (
    <div className={`bg-bg-card rounded-xl border ${cfg.borderColor} p-4`}>
      <div className="flex items-center gap-2 mb-2">
        <span className={`text-sm ${cfg.labelColor}`}>{cfg.icon}</span>
        <span className={`text-xs font-semibold uppercase tracking-wider ${cfg.labelColor}`}>
          AI · {cfg.label}
        </span>
      </div>
      <p className="text-sm text-text-secondary leading-relaxed">{ai.summary}</p>
      {(ai.state === "Extended" || ai.state === "Short") && (
        <p className="mt-2 text-xs text-text-muted italic">
          Bu analiz bilgi desteği amaçlıdır. Karar sizindir.
        </p>
      )}
    </div>
  );
}
