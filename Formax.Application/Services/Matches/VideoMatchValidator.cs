using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Formax.Application.Services.News.Intelligence;

namespace Formax.Application.Services.Matches
{
    /// <summary>
    /// VİDEO ↔ MAÇ EŞLEŞTİRME DOĞRULAMASI — "Önemli Anları İzle" hattının kapısı.
    ///
    /// KURAL: yanlış eşleşme yapmaktansa hiç video göstermemek yeğdir. Bir video ancak
    /// aşağıdakilerin HEPSİ doğrulanırsa maça bağlanır:
    ///   1. HER İKİ takım adı içerikte geçiyor (tek takım adı YETMEZ),
    ///   2. Yayın zamanı maçın penceresinde (ilk düdükten sonra, makul bir kuyruk içinde),
    ///   3. İçerik bir OLAY/özet anlatıyor (gol, kart, penaltı, VAR, maç özeti) —
    ///      genel takım içeriği (antrenman, transfer, röportaj) önemli an DEĞİLDİR,
    ///   4. Başlıkta dakika yazıyorsa, o dakika maçın GERÇEK olaylarından biriyle uyuşuyor.
    ///
    /// Hiçbir aşamada sahte kimlik/URL üretilmez; yalnız var olan içerik kabul/ret edilir.
    /// </summary>
    public static class VideoMatchValidator
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        /// <summary>Maç bittikten sonra özet/önemli an videolarının yayımlanabileceği kuyruk.</summary>
        public static readonly TimeSpan PublishTail = TimeSpan.FromDays(7);

        /// <summary>Bir videonun maçtan önce yayımlanmış olması onu önemli an yapmaz.</summary>
        public static readonly TimeSpan PreKickoffTolerance = TimeSpan.Zero;

        public readonly record struct Result(bool Accepted, string Reason);

        /// <summary>Maçın gerçek olay dakikaları — başlıktaki dakikayı çapraz doğrulamak için.</summary>
        public sealed class MatchContext
        {
            public string HomeTeam { get; init; } = "";
            public string AwayTeam { get; init; } = "";
            public DateTime KickoffUtc { get; init; }
            public IReadOnlyCollection<int> EventMinutes { get; init; } = Array.Empty<int>();
        }

        /// <param name="sourceTeam">
        /// İçeriğin geldiği DOĞRULANMIŞ resmi hesabın kulübü (varsa).
        ///
        /// NEDEN GEREKLİ: bir kulüp kendi kanalında videoyu yayımlarken KENDİ adını
        /// başlığa yazmaz. Ölçüldü (Trabzonspor resmi kanalı): "…Kasımpaşa maçı
        /// değerlendirmesi" — başlıkta yalnız rakip geçiyor. Kaynağın kimliği zaten
        /// doğrulanmış olduğu için o kulüp "anılmış" sayılır; KARŞI TAKIM yine başlıkta
        /// AÇIKÇA geçmek zorundadır. Yani kural gevşemez: hâlâ iki taraf da doğrulanır,
        /// biri metinden biri doğrulanmış kaynak kimliğinden.
        /// </param>
        public static Result Validate(
            string? title, string? summary, DateTime publishedUtc, MatchContext ctx,
            string? sourceTeam = null)
        {
            var raw = ((title ?? "") + " " + (summary ?? "")).Trim();
            if (raw.Length == 0) return new Result(false, "içerik boş");

            // 1) HER İKİ TAKIM — katı kural.
            var folded = NewsTextNormalizer.Fold(raw);
            var sourceFolded = NewsTextNormalizer.Fold(sourceTeam);

            bool SourceIs(string team) =>
                sourceFolded.Length > 0 &&
                NewsTextNormalizer.Fold(team) is { Length: > 0 } t &&
                (sourceFolded.Contains(t, StringComparison.Ordinal) ||
                 t.Contains(sourceFolded, StringComparison.Ordinal));

            // Kontrollü takma ad tablosu ("Nott'm Forest", "Man Utd"…) — serbest benzerlik değil.
            var hasHome = PostMatch.TeamNameAliases.Mentions(folded, ctx.HomeTeam) || SourceIs(ctx.HomeTeam);
            var hasAway = PostMatch.TeamNameAliases.Mentions(folded, ctx.AwayTeam) || SourceIs(ctx.AwayTeam);
            if (!hasHome || !hasAway)
                return new Result(false, "her iki takım adı doğrulanamadı");

            // 2) ZAMAN PENCERESİ — maçtan önce yayımlanan içerik önemli an olamaz.
            if (publishedUtc < ctx.KickoffUtc - PreKickoffTolerance)
                return new Result(false, "maçtan önce yayımlanmış");
            if (publishedUtc > ctx.KickoffUtc + PublishTail)
                return new Result(false, "maç penceresinin dışında yayımlanmış");

            // 3) OLAY/ÖZET İÇERİĞİ — genel takım içeriği önemli an değildir.
            if (!EventSignal.IsMatch(folded))
                return new Result(false, "olay/özet içeriği değil");
            if (NonMatchContent.IsMatch(folded))
                return new Result(false, "maç dışı içerik (transfer/antrenman/röportaj)");

            // 4) DAKİKA ÇAPRAZ DOĞRULAMA — başlıkta dakika varsa gerçek olayla uyuşmalı.
            var (minute, _) = MatchMinuteExtractor.Extract(title, summary);
            if (minute.HasValue && ctx.EventMinutes.Count > 0
                && !ctx.EventMinutes.Any(m => Math.Abs(m - minute.Value) <= 1))
                return new Result(false, $"başlıktaki {minute}. dakika maçın olaylarıyla uyuşmuyor");

            return new Result(true, "doğrulandı");
        }

        /// <summary>
        /// Bir olayı ya da maç özetini anlatan içerik işaretleri.
        ///
        /// ÇOK DİLLİ: resmi kulüp kanalları özeti KENDİ dillerinde yayımlıyor. Ölçüldü —
        /// HNK Hajduk Split resmi kanalı maç özetini "SAŽETAK" başlığıyla yayımlıyor;
        /// yalnız Türkçe/İngilizce sözcüklere bakan kapı gerçek maç özetini eliyordu.
        /// </summary>
        private static readonly Regex EventSignal = new(
            @"(\bgol\b|\bgolu\b|goller|\bgoal\b|goals|penalti|penalty|kirmizi kart|red card|" +
            @"sari kart|yellow card|\bvar\b|ozet|ozetler|highlight|highlights|" +
            @"sazetak|resumen|resume|zusammenfassung|sintesi|melhores momentos|" +
            @"gecis|kurtaris|save|maç sonucu|mac sonucu|full match|extended)", Opts);

        /// <summary>
        /// Maçla ilgisiz kulüp içeriği. Ölçüldü: resmi kanallarda maç günü bile
        /// "Inside …", "training", "press conference" gibi içerikler yayımlanıyor.
        /// </summary>
        private static readonly Regex NonMatchContent = new(
            @"(transfer|imza att|signs for|antrenman|training|idman|basin toplantisi|" +
            @"press conference|roportaj|interview|magaza|store|bilet|ticket|" +
            @"\binside\b|behind the scenes|kamp|pre-?season|hazirlik maci)", Opts);
    }
}
