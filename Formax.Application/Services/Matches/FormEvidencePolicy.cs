using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Formax.Application.DTOs.Matches;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// FORMAX — FORM VERİSİNİN KALİTE KAPISI. Tek kaynak; her yüzey (Maç Detayı insight'ı,
    /// Reasoning paketi, LLM anlatısı, Outlook, çıkış guard'ı) AYNI kuralı okur.
    ///
    /// NEDEN VAR — ölçülen üç hata (18.08.2026 denetimi, 24 maç / 471 cümle):
    ///   1) VERİ YOK ≠ SIFIR. Malaga'nın kapsam içi hiç maçı yokken Outlook "Deplasman
    ///      performansı 0/100", "momentum rakipte", "dar bir Atletico üstünlüğü" üretti.
    ///      Sıfır maç bir başarısızlık değil, BİLGİSİZLİKTİR.
    ///   2) ESKİ VERİ = GÜNCEL FORM DEĞİL. "Lask Linz'in galibiyet hasreti sürüyor" cümlesi
    ///      2024 Ekim–Aralık tarihli beş maça dayanıyordu (607 gün). Sayılar doğruydu, cümle
    ///      yanlıştı: zamansal kesinlik (hasret/uzun süredir/son dönemde) TAZE veri ister.
    ///   3) KAPSAM SESSİZ KALIYOR. Form yalnız allow-list'teki liglerden hesaplanır; LASK'ın
    ///      Avusturya ligi kapsam dışı olduğu için tam bir sezon görünmüyordu. "Son beş maçı"
    ///      denirken aslında "kapsamımdaki son beş maç" kastediliyor.
    ///
    /// EŞİKLER UYDURULMADI, ÖLÇÜLDÜ (kapsam içi 230 takım, 18.08.2026):
    ///   • Örneklem: 0 maç → 9 takım, 1–4 maç → 19, 5+ → 202. Beş, on maçlık pencerenin
    ///     yarısıdır ve oranlara en fazla %20 granülerlik verir (mevcut MinComparableSample
    ///     ile AYNI değer; o sabit artık buraya taşındı).
    ///   • Tazelik: veri yaşı dağılımı 0–30g:137, 61–90:36, 91–120:40, 121–180:3,
    ///     181–270:0, 271–365:0, 365+:5. 181–365 bandı TAMAMEN BOŞ olduğu için 180 gün,
    ///     aktif hiçbir takımı kesmeden bayat veriyi ayırır (sonraki takım 370 gün).
    /// </summary>
    public static class FormEvidencePolicy
    {
        /// <summary>Oran/karşılaştırma cümlesi için her iki tarafta gereken en az gerçek maç.</summary>
        public const int MinSample = 5;

        /// <summary>Bu yaştan eski form verisi "güncel form" olarak anlatılamaz (gün).</summary>
        public const int MaxAgeDays = 180;

        /// <summary>Kıtasal kupa turnuvaları — takımın ulusal ligi kapsam dışıysa örneklem yalnız bunlardan oluşur.</summary>
        private static readonly string[] ContinentalKeywords =
        {
            "champions league", "europa league", "conference league", "şampiyonlar ligi",
            "uefa", "libertadores", "sudamericana"
        };

        /// <summary>
        /// Son maç listesinden kanıt kalitesini çıkarır. Liste, kaynakta zaten yalnız
        /// OYNANMIŞ ve KAPSAM İÇİ maçlardan kurulur; burada yeni sorgu yapılmaz.
        /// </summary>
        public static FormEvidence FromLastMatches(IReadOnlyList<LastMatchDto>? matches, int take = MinSample)
        {
            if (matches == null || matches.Count == 0) return FormEvidence.None;

            var used = matches.Take(take).ToList();
            if (used.Count == 0) return FormEvidence.None;

            // En yeni maç listenin başındadır (kaynak MatchDate DESC sıralar); yine de
            // tarih ayrıştırılabilenlerin en büyüğü alınır — sıraya güvenilmez.
            DateTime? newest = null;
            foreach (var m in used)
            {
                var d = ParseDisplayDate(m.Date);
                if (d.HasValue && (newest == null || d.Value > newest.Value)) newest = d.Value;
            }

            var comps = used
                .Select(m => (m.Competition ?? "").Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Build(used.Count, newest, comps);
        }

        /// <summary>Sayı + tarih + turnuva listesinden kanıt (metrikleri DTO'dan gelen çağıranlar için).</summary>
        public static FormEvidence Build(int sampleCount, DateTime? newestUtc, IReadOnlyList<string>? competitions)
        {
            if (sampleCount <= 0) return FormEvidence.None;

            int? age = newestUtc.HasValue
                ? Math.Max(0, (int)Math.Round((DateTime.UtcNow - newestUtc.Value).TotalDays))
                : null;

            var comps = competitions ?? Array.Empty<string>();
            var onlyContinental = comps.Count > 0 && comps.All(IsContinental);

            return new FormEvidence(sampleCount, newestUtc, age, comps, onlyContinental);
        }

        private static bool IsContinental(string competition)
        {
            var c = (competition ?? "").ToLowerInvariant();
            return ContinentalKeywords.Any(k => c.Contains(k, StringComparison.Ordinal));
        }

        /// <summary>"dd.MM.yyyy" görüntü tarihini UTC güne çevirir; ayrıştırılamazsa null.</summary>
        private static DateTime? ParseDisplayDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParseExact(
                s.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
                ? d
                : null;
        }
    }

    /// <summary>
    /// Bir takımın form kanıtının KALİTESİ. Sayının kendisi değil, o sayıya ne kadar
    /// yaslanılabileceği burada tanımlıdır. Tüketiciler karar vermez, bu kaydı okur.
    /// </summary>
    public sealed record FormEvidence(
        int SampleCount,
        DateTime? NewestMatchUtc,
        int? AgeDays,
        IReadOnlyList<string> Competitions,
        bool OnlyContinentalCups)
    {
        public static readonly FormEvidence None =
            new(0, null, null, Array.Empty<string>(), false);

        /// <summary>Hiç gerçek maç var mı? false → hiçbir form ifadesi kurulamaz.</summary>
        public bool HasData => SampleCount > 0;

        /// <summary>Oran/karşılaştırma için yeterli örneklem var mı?</summary>
        public bool IsSufficient => SampleCount >= FormEvidencePolicy.MinSample;

        /// <summary>Veri "güncel form" sayılacak kadar taze mi?</summary>
        public bool IsFresh => AgeDays.HasValue && AgeDays.Value <= FormEvidencePolicy.MaxAgeDays;

        /// <summary>
        /// ZAMANSAL KESİNLİK İZNİ — "galibiyet hasreti", "uzun süredir", "son dönemde",
        /// "seri", "yükselişte" gibi ifadeler yalnız yeterli VE taze veriyle kurulabilir.
        /// </summary>
        public bool AllowsTrendClaim => IsSufficient && IsFresh;

        /// <summary>Karşılaştırmalı oran cümlesi izni (iki tarafta da aranır).</summary>
        public bool AllowsComparison => IsSufficient;
    }
}
