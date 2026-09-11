using System;

namespace Formax.Application.Services.PostMatch
{
    /// <summary>
    /// RESMÎ VİDEO KAYNAK KADEMELERİ — hangi kaynağa ÖNCE bakılacağının tek sırası.
    ///
    /// NEDEN KADEME: bir maçın özetini birden çok resmî kaynak yayımlayabilir (UEFA,
    /// lig, yayıncı, iki kulüp). Hepsi "resmî"dir ama hepsi AYNI DEĞERDE değildir:
    /// maçın görüntü hakkı turnuvanın/ligin, oradan yayıncınındır. Kulüp kanalları
    /// çoğu turnuvada yalnız kendi cephesini yayımlar. Sıra bu gerçeği izler.
    ///
    /// KÜÇÜK SAYI = ÖNCE BAKILIR. Kademe bir KALİTE notu değil, HAK SAHİPLİĞİ sırasıdır.
    ///
    /// EV/DEPLASMAN AYRIMI kaynağın kendisinde DEĞİL, maç bağlamında çözülür
    /// (<see cref="OfficialVideoSources.EffectiveTier"/>): aynı kulüp bir maçta ev
    /// sahibi, ötekinde deplasmandır.
    /// </summary>
    public static class OfficialVideoSourceTiers
    {
        /// <summary>1 — Turnuva/federasyon (UEFA, FIFA, TFF). Görüntünün asıl hak sahibi.</summary>
        public const int Federation = 1;

        /// <summary>2 — Ligin kendi resmî sitesi/kanalı.</summary>
        public const int League = 2;

        /// <summary>3 — Maçın resmî yayıncısı (ör. TRT SPOR).</summary>
        public const int Broadcaster = 3;

        /// <summary>
        /// 4/5 — Kulüp. Listede TEK kademe olarak durur; maç bağlamında ev sahibi 4,
        /// deplasman 5 olur.
        /// </summary>
        public const int Club = 4;

        /// <summary>5 — Kulüp kaynağının deplasman konumundaki karşılığı.</summary>
        public const int AwayClub = 5;

        /// <summary>6 — Lisanslı ve gömmeye açık resmî spor yayıncısı.</summary>
        public const int LicensedSportsOutlet = 6;

        /// <summary>
        /// 7 — YouTube RSS. YARDIMCI KEŞİF: kendi başına bir hak sahibi değildir,
        /// yalnız yukarıdaki kaynakların kanallarını okumanın anahtarsız yoludur.
        /// </summary>
        public const int AuxiliaryDiscovery = 7;

        /// <summary>Kademenin insan okuyabilir adı (teşhis/rapor).</summary>
        public static string Label(int tier) => tier switch
        {
            Federation           => "Federasyon/Turnuva",
            League               => "Lig",
            Broadcaster          => "Yayinci",
            Club                 => "Ev sahibi kulup",
            AwayClub             => "Deplasman kulubu",
            LicensedSportsOutlet => "Lisansli spor kaynagi",
            AuxiliaryDiscovery   => "Yardimci kesif",
            _                    => "Bilinmeyen"
        };
    }
}
