// Takım arması — logoUrl varsa görsel, yoksa isimden monogram. (Uydurma veri yok.)
interface Props {
  name: string;
  logoUrl?: string | null;
  size?: number;
}

export function TeamCrest({ name, logoUrl, size = 88 }: Props) {
  if (logoUrl) {
    return (
      // eslint-disable-next-line @next/next/no-img-element
      <img
        src={logoUrl}
        alt={name}
        width={size}
        height={size}
        className="rounded-full object-contain bg-white/95 shadow-[0_4px_16px_rgba(0,0,0,0.45)]"
        style={{ width: size, height: size }}
      />
    );
  }

  const initials =
    name.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "").slice(0, 3).toUpperCase() || "—";

  return (
    <div
      className="rounded-full bg-white/95 shadow-[0_4px_16px_rgba(0,0,0,0.45)] flex items-center justify-center"
      style={{ width: size, height: size }}
    >
      <span className="font-black text-[#0a0e16]" style={{ fontSize: size * 0.28 }}>
        {initials}
      </span>
    </div>
  );
}
