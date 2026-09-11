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

        /// <summary>
        /// SİSTEMİN İÇ DURUMUNU ANLATAN CÜMLELER — kelime değil, CÜMLE olarak atılır.
        ///
        /// NEDEN CÜMLE DÜZEYİ: "sezon verileri" gibi bir ifadeden tek tek kelime silmek
        /// geriye bozuk bir cümle bırakır. Bu ifadeler futbol değil, deponun iç işleyişidir
        /// ve kullanıcı metninde hiç bulunmamalıdır — bu yüzden cümlenin tamamı düşer.
        ///
        /// ÖLÇÜLDÜ (06.09.2026, Manchester United–Manchester City, gerçek LLM çıktısı):
        /// "Sezon verilerinin henüz tamamlanmadığı bu erken dönemde, her iki takımın da
        /// hücum gücü ön plana çıkarken…". Cümledeki sayı ligin BAŞKA bir maçına aitti.
        /// Kök neden pakette ve prompt'ta kapatıldı; bu süzgeç ikinci emniyet kemeridir.
        /// </summary>
        private static readonly string[] SystemTalk =
        {
            @"sezon\s+veriler",        // "sezon verileri tamamlanmadı"
            @"veriler(i|in)?\s+eksik",
            @"veri\s+taban",
            @"kay(ı|i)tlar(ı|i)m(ı|i)z",
            @"depo(m|)uz(da|)",
            @"ma(ç|c)\s+bekliyor",
            @"sonu(ç|c)lar(ı|i)\s+hen(ü|u)z\s+kesinle(ş|s)me"
        };

        public string Sanitize(string? text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var cleaned = DropSystemTalkSentences(text.Trim());

            foreach (var term in Banned)
                cleaned = Regex.Replace(cleaned, WordPattern(term), "", RegexOptions.IgnoreCase);

            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"\s+([.,!?])", "$1");

            if (cleaned.Length > maxLen)
                cleaned = TrimToSentence(cleaned, maxLen);

            return cleaned;
        }

        /// <summary>
        /// Sistemin iç durumundan söz eden cümleleri TAMAMEN atar.
        ///
        /// Metin cümle sınırlarından bölünür; içinde <see cref="SystemTalk"/>
        /// kalıplarından biri geçen cümle atılır, kalanlar sırası bozulmadan birleşir.
        /// Hepsi atılırsa boş döner ve çağıran fallback'e geçer — yarım cümle bırakmaktansa
        /// hiç metin göstermemek doğrudur.
        /// </summary>
        public static string DropSystemTalkSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            // Cümle sonu: . ! ? ve ardından boşluk/son. Kısaltmalar için nokta+boşluk yeterli.
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var kept = sentences.Where(s =>
                !SystemTalk.Any(p => Regex.IsMatch(s, p, RegexOptions.IgnoreCase)));

            return string.Join(" ", kept).Trim();
        }

        /// <summary>
        /// Yasaklı terim KELİME OLARAK aranır — kelimenin İÇİNDEN değil.
        ///
        /// Ölçüldü (Beşiktaş–Eyüpspor, 15.08): "kazanır" terimi kelime sınırı olmadan
        /// silindiği için "Beşiktaş iki kez kazanırken" cümlesi kullanıcıya
        /// "Beşiktaş iki kez ken" diye çıktı — kumar dili değil, masum bir fiil kırılmıştı.
        /// Aynı tuzak "kesin" için de vardı ("kesintisiz", "kesinlikle").
        /// Yasak listesi DEĞİŞMEDİ; yalnız eşleşme kelime düzeyine indirildi.
        /// </summary>
        private static string WordPattern(string term) =>
            char.IsLetter(term[0]) && char.IsLetter(term[^1])
                ? @"\b" + Regex.Escape(term) + @"\b"
                : Regex.Escape(term);   // "%100", "100%" gibi işaretli terimler

        /// <summary>
        /// Uzunluk sınırı aşıldığında metni SON TAM CÜMLEDE bitirir. Ölçüldü (14.08):
        /// sabit kesme kullanıcıya "…savunma hatalarıyla karşı karşıya…" ve "…rakiplerine
        /// karşı b…" gibi kelime ortasından kopmuş kartlar gösteriyordu. Cümle sonu yoksa
        /// en yakın boşluktan kesilir; hiçbir durumda kelime ortasından bölünmez.
        /// </summary>
        private static string TrimToSentence(string text, int maxLen)
        {
            var window = text[..maxLen];

            var lastStop = window.LastIndexOfAny(new[] { '.', '!', '?' });
            // Çok erken biten bir nokta (kısaltma vb.) metni anlamsız kısaltmasın.
            if (lastStop >= maxLen / 2)
                return window[..(lastStop + 1)].TrimEnd();

            var lastSpace = window.LastIndexOf(' ');
            if (lastSpace > 0)
                return window[..lastSpace].TrimEnd() + "…";

            return window.TrimEnd() + "…";
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
