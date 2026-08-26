/**
 * Hafif dokunsal geri bildirim — Interaction Spec §3 ("Tap: Haptic + Navigate").
 * Web'de yalnızca Vibration API destekleyen cihazlarda çalışır; desteklenmiyorsa
 * sessizce yok sayılır (iOS Safari desteklemez).
 */
export function haptic(duration = 10): void {
  if (typeof navigator === "undefined") return;
  navigator.vibrate?.(duration);
}
