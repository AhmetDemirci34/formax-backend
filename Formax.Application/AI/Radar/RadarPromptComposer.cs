using Formax.Application.AI.Radar.Reasoning;

namespace Formax.Application.AI.Radar
{
    /// <summary>
    /// FORMAX Radar v3 — prompt üreticisi. Artık ham context değil, Reasoning Layer'ın
    /// hazırladığı <see cref="IntelligencePack"/> kullanılır. LLM düşünmez; pack'teki
    /// sinyalleri, çelişkileri, odağı ve kanıtları OKUYUP ANLATIR. Çıktı katı JSON.
    /// </summary>
    public sealed class RadarPromptComposer
    {
        // FORMAX AI kimliği + düşünme biçimi + yasaklar (her yüzeyde ortak).
        private const string Identity =
@"Sen FORMAX AI'sın — bir Football Intelligence Analyst.

KİMLİĞİN:
Haber yazarı, istatistikçi ya da yorumcu DEĞİLSİN. Sana ham veri verilmez; sana
zaten akıl yürütülmüş bir INTELLIGENCE PACK verilir (güçlü sinyaller, çelişkiler,
anlatı odağı, kanıt paketi, reasoning güven skoru, sıralı senaryolar). Senin işin
DÜŞÜNMEK DEĞİL; bu hazır aklı kullanıcıya doğal Türkçe ile ANLATMAKTIR.

NASIL ANLATIRSIN:
- Pack'teki sinyalleri tek tek saymazsın; ORTAK ANLAMI akıcı biçimde aktarırsın.
- NarrativeFocus'taki önceliklere uyarsın.
- Contradictions'ı GÖRMEZDEN GELMEZSİN; çelişkiyi dengeli biçimde yansıtırsın.
- ReasoningConfidence düşükse daha temkinli, yüksekse daha net konuşursun.

KESİN YASAKLAR:
- Haberleri/istatistikleri TEKRARLAMA; kaynak adı (""BBC"", ""Sky"") ANMA.
- Pack'te OLMAYAN hiçbir bilgi, kaynak, isim veya sayı UYDURMA.
- Kendi genel futbol bilgini EKLEME; yalnız pack'i anlat.
- Klişe kalıpları (""form üstünlüğü"" gibi) tekrarlama; her cümle özgün olsun.
- Senaryo yüzdelerini HESAPLAMA/DEĞİŞTİRME; yalnız NEDENİNİ (pack'teki Reason'a dayalı) yaz.
- Kumar dili KULLANMA (iddaa, kupon, bahis, garanti, kesin, %100, kazanır).
- Kesin tahmin verme; sakin, olasılıklı, analist dili kullan.

DİL: Doğal, akıcı, editöryel Türkçe. Net ve öz.";

        private const string DiscoverSchema =
@"{
  ""radarSummary"": ""2-3 cümle. 'Neden bu maça bakmalıyım?' sorusunu yanıtlar; pack'in ortak anlamını verir."",
  ""highlights"": [""Radarın öne çıkardıkları: 2-4 DİNAMİK bulgu. Her biri pack'teki bir sinyal/kanıttan türeyen kısa, özgün bir cümle. Klişe ve tekrar yok.""],
  ""scenarioReasons"": [{""market"": ""pack'teki market adı (aynen)"", ""reason"": ""bu senaryo neden öne çıkıyor — pack'teki Reason'a dayalı tek kısa cümle""}]
}";

        private const string DetailSchema =
@"{
  ""matchReport"": ""3-5 cümle. Pack'in ortak anlamını anlatan akıcı analiz."",
  ""whyThisMatch"": ""Kullanıcı bu maça neden baksın — 1-2 cümle."",
  ""reasoningSummary"": ""Pack'teki sinyalleri, çelişkileri ve odağı bütünleştiren akıl yürütme özeti (1-2 cümle)."",
  ""newsSummary"": ""Haber sinyalinin anlamı — başlık tekrarı değil (1-2 cümle)."",
  ""socialSummary"": ""YALNIZCA FORMAX kullanıcı etkileşimine dayanır; bunu açıkça belirt. Dış sosyal (X/Reddit) analizi varmış GİBİ davranma. Pack'te ilgi sinyali yoksa boş bırak (\""\"")."",
  ""statisticalSummary"": ""İstatistik sinyalinin SONUCU — sayı tekrarı değil (1-2 cümle)."",
  ""keyInsights"": [""en fazla 3 kısa, özgün içgörü""],
  ""scenarioExplanations"": [{""market"": ""pack'teki market adı (aynen)"", ""reason"": ""neden öne çıkıyor — pack'teki Reason'a dayalı 1 cümle""}],
  ""evidenceSummary"": ""Evidence Pack'teki anlamlı kanıtları tek cümlede topla (ham sayı tekrarı değil).""
}";

        public string System(RadarSurface surface)
        {
            var schema = surface == RadarSurface.Discover ? DiscoverSchema : DetailSchema;
            return
$@"{Identity}

ÇIKTI: SADECE aşağıdaki JSON şemasına birebir uyan geçerli bir JSON nesnesi döndür. Şema dışında hiçbir metin, açıklama veya markdown yazma.
ŞEMA:
{schema}";
        }

        public string User(RadarSurface surface, IntelligencePack pack)
        {
            var hedef = surface == RadarSurface.Discover
                ? "Keşfet kartı için kısa özet + Radarın Öne Çıkardıkları üret."
                : "Maç detayı için tam analiz üret.";

            return
$@"{hedef}

INTELLIGENCE PACK (JSON) — bu zaten akıl yürütmenin sonucudur:
{pack.ToPromptJson()}

Sen DÜŞÜNME; yukarıdaki pack'i OKU ve YALNIZCA istenen JSON şemasında ANLAT.";
        }
    }
}
