using Formax.Application.AI.Radar;
using Xunit;

namespace Formax.Tests;

/// <summary>
/// ANLATIDA SİSTEM DİLİ YOK — kullanıcı futbol okur, deponun iç durumunu değil.
///
/// ÖLÇÜLEN HATA (06.09.2026, Manchester United–Manchester City, GERÇEK LLM çıktısı):
/// Intelligence Pack modele ligin eksik maç sayısını (<c>SezonVerisiTam</c>,
/// <c>EksikMacSayisi</c>) gönderiyor, prompt da "sezon verilerinin tamamlanmadığını
/// söylersin" diye AÇIKÇA talimat veriyordu. Model bunu Türkçeye çevirdi:
///
///   "Sezon verilerinin henüz tamamlanmadığı bu erken dönemde, her iki takımın da
///    hücum gücü ön plana çıkarken savunma hattındaki kırılganlıklar dikkat çekiyor."
///
/// Cümledeki eksiklik anlatılan iki takımla İLGİSİZ bir maça aitti. Kök neden pakette
/// ve prompt'ta kapatıldı; bu süzgeç ikinci emniyet kemeridir.
/// </summary>
public class NarrativeSystemTalkTests
{
    private readonly RadarOutputGuard _guard = new();

    [Fact]
    public void OlculenGercekCikti_SistemCumlesiAtilir()
    {
        const string measured =
            "Premier League'de Manchester United ve Manchester City karşı karşıya geliyor. " +
            "Sezon verilerinin henüz tamamlanmadığı bu erken dönemde, her iki takımın da " +
            "hücum gücü ön plana çıkarken savunma hattındaki kırılganlıklar dikkat çekiyor. " +
            "Maçın genelinde yüksek temponun belirleyici olması bekleniyor.";

        var cleaned = _guard.Sanitize(measured, 2000);

        Assert.DoesNotContain("Sezon verilerinin", cleaned);
        // Diğer cümleler SIRASI BOZULMADAN korunur — anlatı boşa çıkmaz.
        Assert.StartsWith("Premier League'de Manchester United", cleaned);
        Assert.Contains("yüksek temponun belirleyici olması", cleaned);
    }

    [Theory]
    [InlineData("Sezon verileri henüz tamamlanmadı.")]
    [InlineData("Ligde 3 maç bekliyor.")]
    [InlineData("Veri tabanında bu takım için kayıt yok.")]
    [InlineData("Sonuçları henüz kesinleşmemiş maçlar var.")]
    [InlineData("Verileri eksik olduğu için yorum yapılmıyor.")]
    public void SistemDiliCumleleri_Atilir(string sentence)
    {
        var text = "Ev sahibi baskı kurmayı seviyor. " + sentence + " Deplasman ise kontra atağa yaslanıyor.";

        var cleaned = _guard.Sanitize(text, 2000);

        Assert.DoesNotContain(sentence, cleaned);
        Assert.Contains("Ev sahibi baskı kurmayı seviyor.", cleaned);
        Assert.Contains("Deplasman ise kontra atağa yaslanıyor.", cleaned);
    }

    [Fact]
    public void FutbolCumleleri_Korunur()
    {
        const string football =
            "İki takım da bu sezon gol yollarında üretken. " +
            "Ev sahibi son maçında üç gol attı. " +
            "Karşılıklı gol ihtimali öne çıkıyor.";

        Assert.Equal(football, _guard.Sanitize(football, 2000));
    }

    [Fact]
    public void TumCumlelerSistemDiliyse_BosDoner()
    {
        // Yarım/anlamsız metin göstermektense HİÇ metin göstermemek doğrudur;
        // çağıran bu durumda fallback'e geçer.
        var cleaned = _guard.Sanitize("Sezon verileri tamamlanmadı. Ligde 2 maç bekliyor.", 2000);
        Assert.Equal(string.Empty, cleaned);
    }

    [Fact]
    public void BosGirdi_BosDoner()
    {
        Assert.Equal(string.Empty, RadarOutputGuard.DropSystemTalkSentences(""));
        Assert.Equal(string.Empty, RadarOutputGuard.DropSystemTalkSentences("   "));
    }
}
