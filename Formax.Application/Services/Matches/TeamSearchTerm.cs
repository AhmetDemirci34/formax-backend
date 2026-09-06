using System;
using System.Linq;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// TAKIM ARAMA TERİMİ — normalizasyon ve eşleşme kuralının TEK yeri.
    ///
    /// NEDEN VERİTABANI COLLATION'INA GÜVENİLMİYOR: FormaxDB collation'ı
    /// <c>Turkish_CI_AS</c>'tir — büyük/küçük harf duyarsız ama AKSAN DUYARLI. Yani
    /// "fenerbahce" yazan kullanıcı "Fenerbahçe"yi BULAMAZ. Türk kullanıcıların
    /// çoğunlukla klavyeden ç/ş/ı/ğ yazmadığı bir üründe bu, aramanın çalışmaması
    /// demektir.
    ///
    /// Bu yüzden eşleşme, projenin zaten kullandığı diakritik-katlayıcı normalizasyonla
    /// yapılır: "Fenerbahçe" → "fenerbahce", "İSTANBUL" → "istanbul". Türkçe "İ" tuzağı
    /// (ToLowerInvariant → "i̇") da orada çözülüdür.
    ///
    /// SQL GÜVENLİĞİ: terim sorguya METİN OLARAK GÖMÜLMEZ. Eşleşme bellekte, katlanmış
    /// takım adı dizini üzerinde yapılır; dolayısıyla LIKE joker karakterleri (%, _, [)
    /// ve enjeksiyon yüzeyi diye bir şey oluşmaz — kullanıcı ne yazarsa yazsın düz metin
    /// olarak karşılaştırılır.
    /// </summary>
    public static class TeamSearchTerm
    {
        /// <summary>Aramanın başlaması için gereken en az karakter sayısı.</summary>
        public const int MinLength = 2;

        /// <summary>Kullanıcı girdisini eşleşme diline indirger (kırpar + katlar).</summary>
        public static string Normalize(string? raw)
            => NewsTextNormalizer.Fold((raw ?? string.Empty).Trim());

        /// <summary>Terim aramaya yetiyor mu? (kırpılmış hâli en az iki karakter)</summary>
        public static bool IsSearchable(string? raw) => Normalize(raw).Length >= MinLength;

        /// <summary>
        /// Takım adı bu terimle eşleşiyor mu?
        ///
        /// KURAL: katlanmış ad, katlanmış terimi İÇERMELİ. "fener" → "Fenerbahçe" bulur.
        /// Bulanık/benzerlik eşleşmesi YOKTUR: "Trabzon" yazan kullanıcıya "Trabzonspor"
        /// gelir ama "Konyaspor" GELMEZ. Yakın-yazım eşleştirmesi, farklı takımları
        /// birbirine karıştırmanın en hızlı yoludur.
        /// </summary>
        public static bool Matches(string? teamName, string normalizedTerm)
        {
            if (normalizedTerm.Length < MinLength) return false;
            var name = NewsTextNormalizer.Fold(teamName);
            return name.Length > 0 && name.Contains(normalizedTerm, StringComparison.Ordinal);
        }
    }
}
