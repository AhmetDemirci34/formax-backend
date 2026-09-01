using System;
using System.Collections.Generic;

namespace Formax.Application.DTOs.Fixtures
{
    /// <summary>
    /// GÜN-BAZLI TOPLU FİKSTÜR ÇEKİMİNİN DÜRÜST SONUCU.
    ///
    /// NEDEN VAR (31.08.2026 sonuç alım düzeltmesi): eski gün döngüsü "hep ya da hiç"ti —
    /// pencerenin TEK bir günü hata verdiğinde (kota/plan/taşıma) istisna fırlatılıyor ve
    /// BAŞARIYLA ÇEKİLMİŞ diğer günler de çöpe gidiyordu. Ölçülen kanıt: 30.08.2026 21:01
    /// turunda <c>fixtures?date=2026-08-30</c> yanıtı (921 fikstür, La Liga maçları "FT")
    /// kendi L2 cache'imize YAZILDI, ama aynı turda sonraki gün hata verdiği için Matches
    /// tablosuna TEK SATIR yazılmadı; maçlar NotStarted kaldı.
    ///
    /// Bu tip başarıyı ve başarısızlığı AYIRIR: çağıran hem eldeki gerçek veriyi yazabilir
    /// hem de hangi günün erişilemediğini bilir. "Kısmi başarı" asla "tam başarı" gibi
    /// sunulmaz — <see cref="FailedDates"/> doluysa tur EKSİKTİR.
    /// </summary>
    public sealed class SportsFixtureDayBatch
    {
        /// <summary>Başarıyla çekilen günlerden gelen fikstürlerin tamamı.</summary>
        public List<SportsFixtureResult> Fixtures { get; } = new();

        /// <summary>Sağlayıcıdan geçerli gövde alınan günler.</summary>
        public List<DateTime> SucceededDates { get; } = new();

        /// <summary>Erişilemeyen günler ve sağlayıcının verdiği gerçek sebep.</summary>
        public List<(DateTime Date, string Reason)> FailedDates { get; } = new();

        /// <summary>
        /// ABONELİK PLANI bu günü kapatıyor (geçici hata DEĞİL). Ölçüldü 31.08.2026:
        /// api-football Free plan yalnız [bugün-1, bugün+1] aralığındaki <c>date=</c>
        /// sorgularını kabul ediyor; daha eski günler "Free plans do not have access to
        /// this date" ile reddediliyor. Bu günler tekrar denenerek AÇILMAZ — o maçların
        /// sonucu ancak fikstür kimliğiyle tek tek alınabilir.
        /// </summary>
        public List<DateTime> PlanBlockedDates { get; } = new();

        /// <summary>Talep edilen gün sayısı.</summary>
        public int RequestedDayCount { get; set; }

        /// <summary>Hiçbir gün alınamadı — çağıran bunu "o pencerede maç yok" SANMAMALIDIR.</summary>
        public bool IsTotalFailure => SucceededDates.Count == 0 && FailedDates.Count > 0;
    }
}
