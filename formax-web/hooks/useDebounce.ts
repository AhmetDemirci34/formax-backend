import { useEffect, useState } from "react";

/**
 * Debounce hook — girilen değeri belirtilen süre kadar geciktirerek döndürür.
 * Süre dolmadan yeni değer gelirse zamanlayıcı sıfırlanır.
 *
 * Yeni paket eklenmeden, projenin mevcut React bağımlılığıyla çalışır.
 */
export function useDebounce<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
