using System;

namespace Formax.Domain.Entities
{
    /// <summary>Sağlayıcı adları — takım kimlik kaydının kaynağı.</summary>
    public static class TeamIdentityProviders
    {
        /// <summary>UEFA resmî maç merkezi (match.uefa.com) takım kimliği.</summary>
        public const string Uefa = "uefa";
    }

    /// <summary>Kimliğin NASIL kurulduğu — provenance; isim benzerliği tek başına yeterli sayılmaz.</summary>
    public static class TeamIdentityEvidence
    {
        /// <summary>
        /// DOĞRULANMIŞ MAÇ BAĞLANTISINDAN türetildi: sonuç botu o maçı organizasyon + sıralı takım çifti +
        /// başlama penceresi ile zaten eşleştirmiş ve <c>OfficialMatchLinks</c> satırını yazmış. En güçlü kanıt.
        /// </summary>
        public const string VerifiedMatchLink = "VerifiedMatchLink";

        /// <summary>
        /// Kaynağın yazdığı ad, AYNI ORGANİZASYONDA oynadığı bilinen FORMAX takımları arasında TEK adayla
        /// eşleşti. İsim tek başına değil, "organizasyon + tekillik" ile birlikte kanıt sayılır.
        /// </summary>
        public const string NameUniqueInCompetition = "NameUniqueInCompetition";
    }

    /// <summary>
    /// TAKIM SAĞLAYICI KİMLİĞİ — kanonik FORMAX takımı ile bir sağlayıcının takım kimliği arasındaki kalıcı bağ.
    ///
    /// NEDEN VAR: <see cref="Team.ExternalTeamId"/> ve <c>ApiFootballTeamId</c> YALNIZ api-football kimliğidir.
    /// Resmî UEFA fikstür kaynağı kendi takım kimliğini (ör. "52277" = Lens) verir; bu kimliği isim benzerliğiyle
    /// her turda yeniden tahmin etmek yanlış takım yazma riskidir (ölçüldü 18.09.2026: "Inter" adı FORMAX'ta
    /// Inter / Inter Turku / Inter Club d'Escaldes ile, "Paris" Paris FC / Paris Saint Germain ile eşleşiyor).
    /// Kimlik bir kez kanıtla kurulur ve burada saklanır.
    ///
    /// Bu tablo YALNIZ eşleme tutar: takım adı, logosu ve diğer ürün verisi <see cref="Team"/> satırındadır.
    /// </summary>
    public sealed class TeamProviderIdentity
    {
        public int Id { get; set; }

        /// <summary>Kanonik FORMAX takımı.</summary>
        public int TeamId { get; set; }

        /// <summary><see cref="TeamIdentityProviders"/>.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>Sağlayıcının kendi takım kimliği (ör. UEFA "52277").</summary>
        public string ProviderTeamId { get; set; } = string.Empty;

        /// <summary>Sağlayıcının yazdığı ad — eşlemenin kanıtı (teşhis için saklanır).</summary>
        public string? ProviderTeamName { get; set; }

        /// <summary><see cref="TeamIdentityEvidence"/>.</summary>
        public string MatchedBy { get; set; } = string.Empty;

        /// <summary>Bu kimliğin ilk kurulduğu an.</summary>
        public DateTime FirstSeenUtc { get; set; }

        /// <summary>En son doğrulandığı an (aynı eşleme yeniden görüldüğünde tazelenir).</summary>
        public DateTime VerifiedAtUtc { get; set; }
    }
}
