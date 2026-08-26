using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Formax.Application.Services.News.Intelligence
{
    /// <summary>
    /// Haber metni için ORTAK normalizasyon. Kaynak kalitesi, takım eşleştirme ve olay
    /// tekilleştirmesi AYNI kuralı kullanmalıdır; aksi hâlde "Fenerbahçe" ile
    /// "fenerbahce.org" veya "Beşiktaş" ile "besiktas" eşleşmez ve gerçek haber elenir.
    ///
    /// TÜRKÇE TUZAĞI: "İ".ToLowerInvariant() → "i̇" (i + birleşen nokta). Bu iki karakterli
    /// dizi hiçbir ASCII kalıbıyla eşleşmez. Fold() önce diakritikleri kaldırır, sonra
    /// küçültür → "İSTANBUL" ve "istanbul" aynı sonuca iner.
    /// </summary>
    internal static class NewsTextNormalizer
    {
        private static readonly (char From, string To)[] TurkishMap =
        {
            ('ç', "c"), ('Ç', "c"), ('ğ', "g"), ('Ğ', "g"), ('ı', "i"), ('I', "i"),
            ('İ', "i"), ('i', "i"), ('ö', "o"), ('Ö', "o"), ('ş', "s"), ('Ş', "s"),
            ('ü', "u"), ('Ü', "u"), ('â', "a"), ('î', "i"), ('û', "u"),
            ('á', "a"), ('à', "a"), ('ä', "a"), ('å', "a"), ('é', "e"), ('è', "e"),
            ('ê', "e"), ('ë', "e"), ('í', "i"), ('ï', "i"), ('ó', "o"), ('ò', "o"),
            ('ô', "o"), ('õ', "o"), ('ú', "u"), ('ù', "u"), ('ñ', "n"), ('ý', "y"),
            ('ø', "o"), ('æ', "ae"), ('ß', "ss"), ('š', "s"), ('ž', "z"), ('č', "c"),
            ('ć', "c"), ('đ', "d"), ('ł', "l"), ('ń', "n"), ('ř', "r"), ('ů', "u"),

            // APOSTROF TEK BİÇİME İNER. Türkçe haber başlıkları çekim ekini apostrofla
            // ayırır ("Trabzonspor'un", "Trabzonspor’da") ve yayıncılar iki farklı karakter
            // kullanır. Olay öznesi bu eke bakılarak belirlendiği için ikisi de aynı
            // karaktere indirilmezse özne çözümlenemez.
            ('’', "'"), ('‘', "'"), ('ʼ', "'"), ('`', "'"), ('´', "'")
        };

        private static readonly Dictionary<char, string> Map = TurkishMap
            .GroupBy(x => x.From).ToDictionary(g => g.Key, g => g.First().To);

        /// <summary>Diakritiksiz, küçük harfli ASCII karşılık. Eşleştirmenin tek dili budur.</summary>
        public static string Fold(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (Map.TryGetValue(ch, out var rep)) { sb.Append(rep); continue; }

                var lower = char.ToLowerInvariant(ch);

                // BÜYÜK HARFLİ DİAKRİTİK TUZAĞI: harita "ž/š/č/ć" gibi KÜÇÜK biçimleri
                // içeriyor ama büyük biçimleri ("Ž", "Š", "Č") içermiyordu. Eski kod bu
                // durumda yalnız küçültüp OLDUĞU GİBİ yazıyordu → "Žalgiris" → "žalgiris"
                // ve "zalgiris" ile EŞLEŞMİYORDU.
                //
                // Ölçüldü: HNK Hajduk Split resmi kanalındaki gerçek maç özeti
                // "SAŽETAK | Žalgiris 2:5 Hajduk" videosu, Žalgiris maçına bağlanamıyordu.
                //
                // Düzeltme: küçültülmüş biçim için haritaya BİR KEZ DAHA bakılır. Yeni
                // eşleme eklenmedi; yalnız var olan eşlemeler büyük harflerde de çalışır.
                if (Map.TryGetValue(lower, out var lowerRep)) sb.Append(lowerRep);
                else sb.Append(lower);
            }
            return sb.ToString();
        }

        /// <summary>Yalnız harf/rakam bırakır (hash ve domain karşılaştırması için).</summary>
        public static string Alphanumeric(string? text) =>
            new string(Fold(text).Where(char.IsLetterOrDigit).ToArray());

        /// <summary>Bir takım adının AYIRT EDİCİ parçaları (jenerik ekler atılır).</summary>
        public static IReadOnlyList<string> TeamTokens(string? team)
        {
            var folded = Fold(team);
            if (folded.Length == 0) return Array.Empty<string>();

            var parts = Regex.Matches(folded, @"[a-z0-9]{3,}")
                             .Select(m => m.Value)
                             .Where(w => !GenericTeamWords.Contains(w))
                             .ToList();

            // Tam ad her zaman ilk aday: "manchester united" tek parça olarak da aranır.
            var full = Regex.Replace(folded, @"\s+", " ").Trim();
            var tokens = new List<string>();
            if (full.Length >= 3) tokens.Add(full);
            tokens.AddRange(parts.OrderByDescending(p => p.Length));
            return tokens.Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>Takımın metinde geçip geçmediği — tam ad ya da en ayırt edici parça.</summary>
        public static bool Mentions(string foldedText, string? team)
        {
            if (string.IsNullOrWhiteSpace(team) || foldedText.Length == 0) return false;
            foreach (var token in TeamTokens(team))
            {
                if (token.Length < 4) continue;
                if (foldedText.Contains(token, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// "fc", "sk", "united" gibi tek başına hiçbir takımı ayırt etmeyen kelimeler.
        /// Bunlarla eşleşme yapılırsa "Manchester United" haberi "Newcastle United"
        /// maçına bağlanır.
        /// </summary>
        private static readonly HashSet<string> GenericTeamWords = new(StringComparer.Ordinal)
        {
            "club", "team", "city", "united", "athletic", "atletico", "sport", "sports",
            "spor", "kulub", "kulubu", "futbol", "football", "calcio", "real", "deportivo",
            "fussball", "sporting", "olympique", "racing", "rangers", "wanderers",
            "spor kulubu", "asociacion", "association", "sociedad", "unido"
        };

        /// <summary>
        /// OLAY GÖVDESİ — başlığın anlam taşıyan kelimeleri, EK KIRPILMIŞ hâlde.
        ///
        /// Neden kırpma: Türkçe eklemeli bir dildir. Aynı olayı anlatan iki başlıkta
        /// "kadro" / "kadroda", "Fenerbahçe" / "Fenerbahçe'de", "sakatlık" / "sakatlandı"
        /// geçer. Birebir kelime karşılaştırması bunları FARKLI olay sayar. İlk 5 harf
        /// (kaba gövde) bu eklerin ayırt ediciliğini kaldırır; İngilizce çoğul/çekim
        /// ("injury/injuries", "signs/signed") için de aynı etkiyi yapar.
        /// </summary>
        public static HashSet<string> EventTokens(string? headline)
        {
            var folded = Fold(headline);
            return Regex.Matches(folded, @"[a-z0-9]{4,}")
                        .Select(m => m.Value)
                        .Where(t => !EventStopWords.Contains(t))
                        .Select(t => t.Length <= 5 ? t : t[..5])
                        .ToHashSet(StringComparer.Ordinal);
        }

        /// <summary>İki olay gövdesinin örtüşmesi: Jaccard + kapsama (biri diğerinin özeti olabilir).</summary>
        public static bool SameEvent(HashSet<string> a, HashSet<string> b)
        {
            if (a.Count == 0 || b.Count == 0) return false;

            var inter = a.Count(b.Contains);
            if (inter == 0) return false;

            var union = a.Count + b.Count - inter;
            var jaccard = (double)inter / union;
            var containment = (double)inter / Math.Min(a.Count, b.Count);

            // Kısa başlık uzun başlığın içinde erirse Jaccard düşer; kapsama bunu yakalar.
            return jaccard >= 0.5 || (containment >= 0.75 && inter >= 3);
        }

        /// <summary>
        /// OLAY ANAHTARI — depolama için kararlı kimlik. Aynı gövdeden türeyen başlıklar
        /// (kelime sırası ve ekler farklı olsa da) aynı anahtarı üretir.
        /// </summary>
        public static string EventKey(string? headline, string? signalType = null)
        {
            var tokens = EventTokens(headline)
                .OrderByDescending(t => t.Length)
                .Take(6)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (tokens.Count == 0) tokens.Add(Alphanumeric(headline));
            var signal = string.IsNullOrWhiteSpace(signalType) ? "" : "|" + Fold(signalType);
            return string.Join("-", tokens) + signal;
        }

        private static readonly HashSet<string> EventStopWords = new(StringComparer.Ordinal)
        {
            "news", "live", "report", "reports", "update", "updates", "latest", "video",
            "watch", "match", "game", "today", "this", "that", "with", "from", "have",
            "will", "what", "when", "after", "before", "their", "there", "about",
            "haber", "haberi", "mac", "maci", "bugun", "sonra", "once", "icin", "oldu",
            // Gövde kırpması sonrası oluşan jenerik parçalar.
            "belli", "aciki", "acikl", "sonuc", "yeni", "buyuk", "iste", "gelis"
        };

        /// <summary>Kısa, kararlı içerik hash'i (depolama anahtarı).</summary>
        public static string Hash(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes, 0, 12);
        }
    }
}
