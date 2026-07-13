import type { StandingSectionDto } from "@/types/api";

interface Props {
  standing: StandingSectionDto;
}

export function StandingsSection({ standing }: Props) {
  return (
    <div className="bg-bg-card rounded-xl border border-border p-4 overflow-x-auto">
      <h3 className="text-xs font-semibold text-text-muted uppercase tracking-wider mb-3">
        Puan Tablosu
      </h3>
      <table className="w-full text-xs min-w-[320px]">
        <thead>
          <tr className="text-text-muted border-b border-border-dim">
            <th className="text-left pb-2 pr-2 font-medium w-6">#</th>
            <th className="text-left pb-2 font-medium">Takım</th>
            <th className="text-right pb-2 font-medium">O</th>
            <th className="text-right pb-2 font-medium">G</th>
            <th className="text-right pb-2 font-medium">B</th>
            <th className="text-right pb-2 font-medium">M</th>
            <th className="text-right pb-2 font-bold">P</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border-dim/50">
          {standing.tableSlice.map((row) => (
            <tr
              key={row.position}
              className={`${
                row.isHighlighted
                  ? "bg-accent/5 text-text-primary"
                  : "text-text-secondary"
              }`}
            >
              <td className="py-1.5 pr-2 text-text-muted">{row.position}</td>
              <td className="py-1.5 font-medium truncate max-w-[100px]">
                {row.teamName}
              </td>
              <td className="py-1.5 text-right">{row.played}</td>
              <td className="py-1.5 text-right">{row.won}</td>
              <td className="py-1.5 text-right">{row.drawn}</td>
              <td className="py-1.5 text-right">{row.lost}</td>
              <td className="py-1.5 text-right font-bold text-text-primary">
                {row.points}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
