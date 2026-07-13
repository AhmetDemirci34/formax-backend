import type { SapmaDto } from "@/types/api";

interface Props {
  sapma: SapmaDto;
}

// ── Meter bar (0–100) ────────────────────────────────────────────────────────

function Meter({
  label,
  value,
  color,
}: {
  label: string;
  value: number;
  color: "accent" | "amber" | "green" | "red";
}) {
  const barClass =
    color === "accent" ? "bg-accent"
    : color === "amber"  ? "bg-formax-amber"
    : color === "green"  ? "bg-formax-green"
    : "bg-formax-red";

  const textClass =
    color === "accent" ? "text-accent"
    : color === "amber"  ? "text-formax-amber"
    : color === "green"  ? "text-formax-green"
    : "text-formax-red";

  return (
    <div className="space-y-1">
      <div className="flex justify-between text-xs">
        <span className="text-text-secondary">{label}</span>
        <span className={`font-bold tabular-nums ${textClass}`}>%{value}</span>
      </div>
      <div className="h-1.5 bg-bg-elevated rounded-full overflow-hidden">
        <div
          className={`h-full rounded-full transition-all ${barClass}`}
          style={{ width: `${Math.min(value, 100)}%` }}
        />
      </div>
    </div>
  );
}

// ── Yön badge ────────────────────────────────────────────────────────────────

function DirectionBadge({ label, value }: { label: string; value: string }) {
  const isHome = value === "Home";
  const isAway = value === "Away";
  const isDenge = value === "Denge" || (!isHome && !isAway);

  const cls = isHome
    ? "text-accent bg-accent/10 border-accent/20"
    : isAway
    ? "text-formax-amber bg-formax-amber/10 border-formax-amber/20"
    : "text-text-muted bg-bg-elevated border-border";

  const turkishValue = isHome ? "Ev Sahibi" : isAway ? "Deplasman" : "Dengede";

  return (
    <div className="flex items-center justify-between text-xs">
      <span className="text-text-secondary">{label}</span>
      <span className={`px-2 py-0.5 rounded border text-xs font-semibold ${cls}`}>
        {turkishValue}
      </span>
    </div>
  );
}

// ── Ana component ─────────────────────────────────────────────────────────────

// Engineer-speak sapmaMetni → plain user language (presentation only).
function userFriendlyMessage(sapma: SapmaDto): string {
  if (sapma.sessizMi) {
    return "Bu maçta güçlü bir yön sinyali oluşmadı. Model ve piyasa benzer düşünüyor.";
  }
  switch (sapma.sapmaBolgesi) {
    case "Yüksek Sapma":
      return "Piyasa ile model arasında belirgin bir fark oluştu — dikkat çekici bir maç.";
    case "Yanılma Riski":
      return "Piyasa ile model bir miktar ayrışıyor; sonuç sürpriz olabilir.";
    case "Dikkat Çekici":
      return "Piyasa ile model arasında küçük bir fark var, izlemeye değer.";
    default:
      return "Model ve piyasa benzer düşünüyor; belirgin bir fırsat sinyali yok.";
  }
}

export function SapmaBlock({ sapma }: Props) {
  // Sapma < 60 = sessiz, göster ama bilgi ver
  const isSessiz = sapma.sessizMi;

  // Sapma bölgesi rengi
  const bolgeColor =
    sapma.sapmaBolgesi === "Yüksek Sapma" ? "red"
    : sapma.sapmaBolgesi === "Yanılma Riski" ? "amber"
    : "accent";

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider">
          Piyasa Sinyali
        </h3>
        {isSessiz ? (
          <span className="text-xs text-text-muted bg-bg-elevated px-2 py-0.5 rounded">
            Yeterli sinyal yok
          </span>
        ) : (
          <span
            className={`text-xs font-semibold px-2 py-0.5 rounded border ${
              bolgeColor === "red"
                ? "text-formax-red bg-formax-red/10 border-formax-red/20"
                : bolgeColor === "amber"
                ? "text-formax-amber bg-formax-amber/10 border-formax-amber/20"
                : "text-accent bg-accent/10 border-accent/20"
            }`}
          >
            {sapma.sapmaBolgesi}
          </span>
        )}
      </div>

      {/* Sapma metni — kullanıcı diline çevrilmiş */}
      {(
        <p className="text-sm text-text-secondary leading-relaxed mb-3">
          {userFriendlyMessage(sapma)}
        </p>
      )}

      {/* Meter'lar — kullanıcı dili terminoloji */}
      <div className="space-y-2.5 mb-3">
        <Meter label="Kullanıcı İlgisi" value={sapma.oynanmaSkoru} color="accent" />
        <Meter label="Takım Gücü"        value={sapma.gucSkoru}     color="green" />
        <Meter
          label="Piyasa Farkı"
          value={sapma.sapma}
          color={sapma.sapma >= 70 ? "red" : sapma.sapma >= 50 ? "amber" : "accent"}
        />
      </div>

      {/* Yön bilgileri */}
      <div className="space-y-2 border-t border-border-dim pt-3">
        <DirectionBadge label="İlgi Yönü"  value={sapma.oynanmaYonu} />
        <DirectionBadge label="Güç Yönü"   value={sapma.gercekGucYonu} />
      </div>
    </div>
  );
}
