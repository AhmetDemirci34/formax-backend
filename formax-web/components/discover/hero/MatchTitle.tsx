interface Props {
  top: string;
  bottom: string;
}

/**
 * FORMAX · MatchTitle (05)
 * PNG iki-satır büyük başlık: "MAN CITY" / "LIVERPOOL".
 */
export function MatchTitle({ top, bottom }: Props) {
  return (
    <h1 className="text-center text-[34px] font-black leading-[0.98] tracking-[-0.02em] text-text-primary">
      {top}
      <br />
      {bottom}
    </h1>
  );
}
