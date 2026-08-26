using System;
using System.Security.Cryptography;
using System.Text;

namespace Formax.Application.Services.Fixtures
{
    /// <summary>
    /// FORMAX Match Identity Engine — sistemin TEK kimlik otoritesi. Dış API MatchId'sine
    /// bağımlı OLMAYAN, deterministik kendi kimliğimiz. Aynı gerçek maç hangi kaynaktan
    /// (api-football / GDP discovery / news) gelirse gelsin AYNI FORMAX_MATCH_ID'ye çözülür
    /// → çok-kaynak birleştirmenin (GDP ↔ News ↔ Evidence ↔ AI Context ↔ UI) anahtarı.
    ///
    ///   Kimlik = SHA256( homeSlug | awaySlug | yyyyMMdd(UTC-gün) )  → "FMX-" + 16 hex
    ///
    /// KİMLİK NEDEN LİG İÇERMEZ: Lig adı sağlayıcıya göre değişir ("Serie A" / "Brazilian
    /// Serie A" / "Serie A TIM") ve ülke bağlamı olmadan çözülemez; aynı gerçek maça farklı
    /// kimlik ürettiriyordu (kimlik boşluğunun kök nedeni). Bir takım aynı UTC gününde tek
    /// maç oynar → (ev + deplasman + gün) çok-kaynak arasında kararlı ve kesin bir anahtardır.
    /// Lig, kimliğin GİRDİSİ değil; kimliğe bağlanan bir META veridir (Fixture/Match üzerinde tutulur).
    ///
    /// TARİH KIND-GÜVENLİ: DB'de maç saatleri UTC olarak saklanır (Kind çoğunlukla Unspecified).
    /// Girdiyi UTC kabul ederiz; yalnız açıkça Local ise UTC'ye çeviririz. Böylece eski
    /// ToUniversalTime() gün-kayması (Unspecified'ı yerel sanıp kaydırma) ortadan kalkar ve
    /// her çağıran (context/detail/news) aynı günü üretir.
    /// </summary>
    public sealed class FormaxMatchIdFactory
    {
        private readonly TeamIdentityResolver _teams;

        public FormaxMatchIdFactory(TeamIdentityResolver teams)
        {
            _teams = teams;
        }

        public string Create(DateTime matchDateUtc, string homeTeam, string awayTeam)
        {
            // Kind-güvenli UTC gün: Unspecified/Utc olduğu gibi kabul edilir (kaymaz),
            // yalnız gerçek Local değer UTC'ye normalize edilir.
            var utc = matchDateUtc.Kind == DateTimeKind.Local
                ? matchDateUtc.ToUniversalTime()
                : matchDateUtc;

            var key = string.Join('|',
                _teams.Slug(homeTeam),
                _teams.Slug(awayTeam),
                utc.ToString("yyyyMMdd"));

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            return "FMX-" + Convert.ToHexString(hash, 0, 8); // 16 hex karakter
        }
    }
}
