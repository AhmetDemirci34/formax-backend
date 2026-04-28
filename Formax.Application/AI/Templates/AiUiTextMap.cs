using Formax.Application.AI.Enums;
using System.Collections.Generic;

namespace Formax.Application.AI.Templates
{
    public static class AiUiTextMap
    {
        // ---------------------------------------------
        // ANA ANALİZ METNİ (MEVCUT DAVRANIŞ - DEĞİŞMEDİ)
        // ---------------------------------------------
        public static string GetAnalysisText(AiDepthLevel level)
        {
            return level switch
            {
                AiDepthLevel.Basic =>
                    "Maç genel hatlarıyla dengede ilerliyor.",

                AiDepthLevel.Reduced =>
                    "Bu analiz, kaydedilmediği için sınırlı gösteriliyor.",

                AiDepthLevel.Full =>
                    "Maçın temposu ve oyun yapısı sonucu doğrudan etkileyebilir.",

                AiDepthLevel.Extended =>
                    "Tempo, oyuncu profilleri ve psikolojik baskılar birlikte değerlendirildiğinde çoklu senaryolar oluşuyor.",

                _ => string.Empty
            };
        }

        public static bool ShouldShowRegisterHint(AiDepthLevel level)
            => level == AiDepthLevel.Reduced;

        // ---------------------------------------------------------
        // EXTENDED BAĞLAM — ANAHTARLI ve SÜREKLİLİK DESTEKLİ
        // Premium değil, state değil
        // ---------------------------------------------------------
        public static IReadOnlyDictionary<string, List<string>> ExtendedContextMap
            => new Dictionary<string, List<string>>
        {
            {
                "tempo_psikoloji",
                new List<string>
                {
                    "Maçın temposu yalnızca skorla değil, oyuncuların karar alma hızlarıyla da şekillenir.",
                    "Tempo düştüğünde psikolojik baskı daha görünür hale gelir."
                }
            },
            {
                "ikinci_yari_davranis",
                new List<string>
                {
                    "Bazı eşleşmelerde ikinci yarı, ilk yarıdan tamamen farklı bir psikolojik akış gösterebilir.",
                    "Oyunun ilerleyen bölümlerinde risk algısı değişebilir."
                }
            },
            {
                "risk_dengesi",
                new List<string>
                {
                    "Risk, sadece olasılık üzerinden değil, maçın duygusal kırılma anları üzerinden de okunur.",
                    "Skor değişmese bile oyun yönü ve baskı dengesi farklılaşabilir."
                }
            }
        };

        // ---------------------------------------------------------
        // GERİYE DÖNÜK UYUMLULUK
        // (AŞAMA-2.1’de kullandığın çağrıları BOZMAZ)
        // ---------------------------------------------------------
        public static List<string> GetExtendedContextBlocks()
        {
            return ExtendedContextMap["tempo_psikoloji"];
        }

        // ---------------------------------------------------------
        // AŞAMA-4.1 — PREMIUM ADI GEÇMEDEN VAR OLMASI
        // SADECE BİLGİ METNİ, DAVRANIŞ YOK
        // ---------------------------------------------------------
        public static string? GetAdditionalContextInfo(bool canExpand)
        {
            if (!canExpand)
                return null;

            return
                "Bu analizde bazı bağlamlar bilinçli olarak sade tutuluyor.";
        }

        // ---------------------------------------------------------
        // AŞAMA-4.2 — SÜREKLİLİK HİSSİ (PASİF)
        // Çelişmeme var, hafıza / iddia / yönlendirme yok
        // ---------------------------------------------------------
        public static string? GetContinuityHint(
            string? lastContextKey,
            string? currentContextKey)
        {
            if (string.IsNullOrWhiteSpace(lastContextKey))
                return null;

            if (lastContextKey == currentContextKey)
            {
                return
                    "Bu değerlendirme, önceki analizle benzer bir çerçevede ilerliyor.";
            }

            return
                "Bu değerlendirme, önceki bağlamdan farklı bir açıdan ele alınıyor.";
        }
    }
}
