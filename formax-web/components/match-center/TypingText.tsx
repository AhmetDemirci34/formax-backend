"use client";

import { useEffect, useState } from "react";
import { useReducedMotion } from "framer-motion";

interface TypingTextProps {
  text: string;
  /** Karakter başına gecikme (ms). */
  speed?: number;
  className?: string;
}

/**
 * Karakter-karakter yazma efekti (Teknik Doküman §6 "Typing Effect").
 * AI analiz metni için kullanılır. Erişilebilirlik: tam metin sr-only sunulur,
 * animasyonlu kısım aria-hidden. reduced-motion'da metin anında tam görünür.
 */
export function TypingText({ text, speed = 16, className }: TypingTextProps) {
  const reduce = useReducedMotion();
  const [count, setCount] = useState(0);

  useEffect(() => {
    // reduced-motion: metni tek adımda (async) tamamla
    if (reduce) {
      const t = setTimeout(() => setCount(text.length), 0);
      return () => clearTimeout(t);
    }
    let i = 0;
    const timer = setInterval(() => {
      i += 1;
      setCount(i);
      if (i >= text.length) clearInterval(timer);
    }, speed);
    return () => clearInterval(timer);
  }, [text, speed, reduce]);

  const typing = count < text.length;

  return (
    <p className={className}>
      <span aria-hidden="true">{text.slice(0, count)}</span>
      {typing && <span className="goalai-caret" aria-hidden="true" />}
      <span className="sr-only">{text}</span>
    </p>
  );
}
