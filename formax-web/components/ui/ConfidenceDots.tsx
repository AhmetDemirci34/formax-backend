import type { ConfidenceLabel } from "@/types/api";

interface Props {
  label: ConfidenceLabel;
  size?: "sm" | "md";
}

const CONFIG: Record<
  ConfidenceLabel,
  { dots: [boolean, boolean, boolean]; color: string; text: string }
> = {
  HIGH:   { dots: [true,  true,  true],  color: "text-formax-green", text: "GÜÇLÜ" },
  MEDIUM: { dots: [true,  true,  false], color: "text-formax-amber",  text: "ORTA" },
  LOW:    { dots: [true,  false, false], color: "text-formax-red",    text: "ZAYIF" },
};

export function ConfidenceDots({ label, size = "md" }: Props) {
  const { dots, color, text } = CONFIG[label] ?? CONFIG.LOW;
  const dotSize = size === "sm" ? "w-1.5 h-1.5" : "w-2 h-2";

  return (
    <span className={`inline-flex items-center gap-1 ${color}`}>
      {dots.map((filled, i) => (
        <span
          key={i}
          className={`rounded-full ${dotSize} ${
            filled ? "bg-current" : "bg-current opacity-20"
          }`}
        />
      ))}
      {size === "md" && (
        <span className="ml-1 text-xs font-semibold tracking-wider">{text}</span>
      )}
    </span>
  );
}
