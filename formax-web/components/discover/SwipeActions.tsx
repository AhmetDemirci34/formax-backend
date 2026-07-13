"use client";

import { motion } from "framer-motion";

interface Props {
  onPass: () => void;
  onLike: () => void;
  disabled?: boolean;
}

// Premium aksiyon çubuğu — GEÇ (sol) · İLGİMİ ÇEKTİ (sağ). İnce, minimal, neon değil.
export function SwipeActions({ onPass, onLike, disabled }: Props) {
  return (
    <div className="flex items-center justify-center gap-5">
      <ActionButton
        onClick={onPass}
        disabled={disabled}
        label="İlgilenmiyorum"
        ring="border-white/15"
        text="text-text-secondary"
      >
        <XIcon />
      </ActionButton>

      <span className="text-[11px] font-medium tracking-wider text-text-muted">KAYDIR</span>

      <ActionButton
        onClick={onLike}
        disabled={disabled}
        label="İlgimi çekti"
        ring="border-formax-green/60"
        text="text-formax-green"
        emphasis
      >
        <HeartIcon />
      </ActionButton>
    </div>
  );
}

function ActionButton({
  onClick,
  disabled,
  label,
  ring,
  text,
  emphasis,
  children,
}: {
  onClick: () => void;
  disabled?: boolean;
  label: string;
  ring: string;
  text: string;
  emphasis?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-2">
      <motion.button
        type="button"
        onClick={onClick}
        disabled={disabled}
        aria-label={label}
        whileTap={{ scale: 0.92 }}
        whileHover={{ scale: 1.04 }}
        className={`flex h-16 w-16 items-center justify-center rounded-full border bg-bg-card/70 backdrop-blur-md transition-colors disabled:opacity-40 ${ring} ${text} ${
          emphasis ? "shadow-[0_8px_30px_-8px_rgba(34,197,94,0.5)]" : ""
        }`}
      >
        {children}
      </motion.button>
      <span className={`text-[11px] font-semibold tracking-wide ${text}`}>{label}</span>
    </div>
  );
}

function XIcon() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round">
      <path d="M6 6l12 12M18 6L6 18" />
    </svg>
  );
}

function HeartIcon() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 21s-7.5-4.6-10-9.2C.6 8.5 2.3 5 5.7 5c2 0 3.4 1.1 4.3 2.3C10.9 6.1 12.3 5 14.3 5c3.4 0 5.1 3.5 3.7 6.8C19.5 16.4 12 21 12 21z" />
    </svg>
  );
}
