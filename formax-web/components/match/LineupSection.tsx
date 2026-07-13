import type { LineupSectionDto, PlayerStatusSectionDto } from "@/types/api";

// ── Lineup ────────────────────────────────────────────────────────────────────

interface LineupProps {
  lineup: LineupSectionDto;
  homeTeamName: string;
  awayTeamName: string;
}

function PlayerRow({
  number,
  name,
  position,
  isCaptain,
}: {
  number: number;
  name: string;
  position: string;
  isCaptain: boolean;
}) {
  return (
    <div className="flex items-center gap-2 py-1.5">
      <span className="w-5 text-xs text-text-muted text-right shrink-0">{number}</span>
      <span className="flex-1 text-sm text-text-primary min-w-0 truncate">
        {name}
        {isCaptain && <span className="ml-1 text-xs text-formax-amber">(K)</span>}
      </span>
      <span className="text-xs text-text-muted px-1 py-0.5 rounded bg-bg-elevated">{position}</span>
    </div>
  );
}

export function LineupSection({ lineup, homeTeamName, awayTeamName }: LineupProps) {
  if (!lineup.lineupsAnnounced) {
    return (
      <div className="bg-bg-card rounded-xl border border-border p-4">
        <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-2">
          Kadrolar
        </h3>
        <p className="text-sm text-text-muted">Kadrolar henüz açıklanmadı.</p>
      </div>
    );
  }

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        İlk 11
      </h3>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <div className="text-xs font-semibold text-text-secondary mb-2 truncate">
            {homeTeamName}
          </div>
          {lineup.homeStartingXI.map((p) => (
            <PlayerRow key={p.shirtNumber} number={p.shirtNumber} name={p.playerName} position={p.position} isCaptain={p.isCaptain} />
          ))}
        </div>
        <div>
          <div className="text-xs font-semibold text-text-secondary mb-2 truncate">
            {awayTeamName}
          </div>
          {lineup.awayStartingXI.map((p) => (
            <PlayerRow key={p.shirtNumber} number={p.shirtNumber} name={p.playerName} position={p.position} isCaptain={p.isCaptain} />
          ))}
        </div>
      </div>
    </div>
  );
}

// ── Player status (injuries/suspensions) ─────────────────────────────────────

interface StatusProps {
  playerStatus: PlayerStatusSectionDto;
}

export function PlayerStatusSection({ playerStatus }: StatusProps) {
  const allPlayers = [
    ...playerStatus.injuries.map((p) => ({ ...p, category: "Sakatı" as const })),
    ...playerStatus.suspensions.map((p) => ({ ...p, category: "Cezalı" as const })),
    ...playerStatus.doubtful.map((p) => ({ ...p, category: "Şüpheli" as const })),
  ];

  if (allPlayers.length === 0) return null;

  const BADGE: Record<string, string> = {
    "Sakatı":   "bg-formax-red/10 text-formax-red",
    "Cezalı":   "bg-formax-amber/10 text-formax-amber",
    "Şüpheli":  "bg-text-muted/10 text-text-muted",
  };

  return (
    <div className="bg-bg-card rounded-xl border border-border p-4">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Sakatlık / Ceza
      </h3>
      <div className="space-y-2">
        {allPlayers.map((p, i) => (
          <div key={i} className="flex items-start gap-2">
            <span
              className={`text-xs font-semibold px-1.5 py-0.5 rounded shrink-0 ${BADGE[p.category]}`}
            >
              {p.category}
            </span>
            <div className="flex-1 min-w-0">
              <span className="text-sm text-text-primary">{p.playerName}</span>
              {p.reason && (
                <span className="text-xs text-text-muted ml-1">· {p.reason}</span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
