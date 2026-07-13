using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v2 — LLM çıktısı güvenlik filtresi (STEP-3: output filter zorunlu).
    /// Kumar dilini temizler, uzunluğu sınırlar. Temizlik sonrası anlamlı metin
    /// kalmazsa <see cref="IsAcceptable"/> false döner → çağıran fallback'e geçer.
    /// </summary>
    public sealed class RadarOutputGuard
    {
        // Sert yasaklı terimler (kumar/garanti dili) — bulunursa cümleden ayıklanır.
        private static readonly string[] Banned =
        {
            "iddaa", "kupon", "bahis", "garanti", "garantili",
            "%100", "100%", "kesin kazanç", "kesin", "mutlaka kazan", "kazanır"
        };

        public string Sanitize(string? text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var cleaned = text.Trim();
            foreach (var term in Banned)
                cleaned = Regex.Replace(cleaned, Regex.Escape(term), "", RegexOptions.IgnoreCase);

            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"\s+([.,!?])", "$1");

            if (cleaned.Length > maxLen)
                cleaned = cleaned[..maxLen].TrimEnd() + "…";

            return cleaned;
        }

        /// <summary>Temizlenmiş metin yeterince anlamlı mı (çok kısa/boş değil mi).</summary>
        public bool IsAcceptable(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text.Trim().Length >= 8;

        /// <summary>Yasaklı terim oranı çok yüksekse (LLM kumar diline kaydıysa) reddet.</summary>
        public bool ContainsHardBan(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var lower = text.ToLowerInvariant();
            return new[] { "iddaa", "kupon", "bahis", "garanti" }.Any(lower.Contains);
        }
    }
}
