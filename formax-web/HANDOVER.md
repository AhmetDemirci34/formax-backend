# FORMAX — HANDOVER (Yeni Sohbet Başlangıç Referansı)

> Bu doküman yeni sohbetin TEK başlangıç referansıdır. Tüm analiz/mimari/UX/reuse tartışmaları
> KİLİTLENMİŞTİR. Yeni sohbet doğrudan implementasyondan devam eder. Tekrar analiz YOK.

---

## 1. Proje Özeti
- **FORMAX = AI Football Discovery Platform.** Skor / bahis / fixture uygulaması DEĞİLDİR.
- **Ürün amacı:** Kullanıcının tek sorusunu en hızlı yanıtlamak → **"Bugün hangi maçı izlemeliyim?"**
- **Home ekranı amacı:** Bu soruya birkaç saniyede cevap veren AI keşif vitrini. Bilgi **rapor değil, hikâye** olarak sunulur.
- **Discovery mantığı:** Backend (Radar + Data Engine + Player Intelligence) analiz eder, sıralar, seçer; **Frontend yalnız render eder**.
- **Kilitli vizyon:** İki referans PNG (1: FORMAX Home gerçek görünüm, 2: Discovery Center UX mimarisi) NİHAİ tasarımdır — yorumlanmaz, yeniden tasarlanmaz, optimize edilmez. Görev: **birebir (pixel-perfect) uygulamak.**

## 2. Kilitli Kararlar (değişmez)
1. Home **tek sayfa**: Header → AI Category Selector → Dynamic Content → Bottom Navigation. Yalnız Dynamic Content değişir.
2. **7 kategori (LOCKED):** AI Önerilerim · Bugünkü Maçlar · Yüksek Olasılıklar · Kupon Önerileri · Sürpriz Maçlar · Derbiler · Filtrele.
3. **Hero yalnız AI Önerilerim'de** görünür. Diğer kategoriler = AI-sıralı maç listesi (Hero YOK).
4. **AI Önerilerim blok sırası (LOCKED):** 1) Hero Match → 2) AI Olası Sonuçlar → 3) AI Yorumu → 4) Radar'ın Öne Çıkardıkları → 5) AI Beklentileri → 6) AI Senaryoları → 7) Oran Hareketleri → 8) Günün AI Kombini → 9) Sana Özel Maçlar.
5. **Kupon Önerileri** = çok-adımlı akış: Öneriler → Kuponlarım → Kupon Düzenle → Kupon Detayı → "Kupon Hazır!".
6. **Design token'lar korunur, silinmez, PNG'ye göre genişletildi.** Yeni UI dili üretilmez.
7. **İkonlar inline SVG** (Lucide kullanılmaz). Tek kaynak: `components/discover/icons.tsx`.
8. **Ücretli AI servisi YOK** (OpenAI/Claude/Gemini/Mistral). FORMAX kendi engine'lerini kullanır. LLM için `Llm:Provider=Mock` (varsayılan) → deterministik fallback; ileride Ollama.
9. **Radar Engine değişmez** (yalnız maç/takım seviyesi Intelligence üretir; oyuncu seviyesi ÜRETMEZ).
10. **Player kimliği v1 = PlayerName + TeamId.** Player entity/registry oluşturulmaz (ileride). `ExternalPlayerId` nullable.
11. **Backend tüm görsel atmosferi üretir; Frontend renk/blur/opacity/gradient HESAPLAMAZ.** Backend **CSS üretmez**, yapılandırılmış veri döner (VisualLayer{Color,Opacity,Blur,Intensity}, VisualGradient{Angle,Stops}).
12. **Kategori mantığı backend'de.** Frontend seçim gönderir, backend liste döndürür.
13. Eski swipe Home ekranı silinmedi → `app/page.swipe.tsx.bak`.

## 3. Kilitli UI (dokunulmaz)
- **DiscoverHeader** (`components/discover/DiscoverHeader.tsx`) — 🔒 LOCKED.
- **BottomNav** (`components/ui/BottomNav.tsx`) — 🔒 LOCKED (sekmeler: Keşfet/Maçlar/Radar/Takip/Profil; global, `app/layout.tsx`).
- **HeroSection + tüm Hero alt-componentleri** (`components/discover/hero/*`) — 🔒 LOCKED (2 polish turu onaylı).
- **AICommentSection** (07 = AI Yorumu) — ✅ ONAYLANDI.
- **Design System** (globals.css token + GlassCard/SectionHeader/Badge) — 🔒 temel.

## 4. Home Screen Architecture (son hal)
```
HomePage (app/page.tsx)
├── DiscoverHeader              (LOCKED, sabit)
├── AICategorySelector          (LOCKED nav, sabit) → CategoryCard ×7
├── DYNAMIC CONTENT AREA        (yalnız burası kategoriyle değişir)
│   └── (aktif = AI Önerilerim) HeroSection → AIPredictions → AIComment →
│        Radar → AIExpectations → AIScenarios → MarketMovements → AICombo → Featured
└── BottomNav                   (LOCKED, sabit, layout.tsx global)
```
- **Kategori davranışı:** kategori değişince Header/Selector/BottomNav **RE-RENDER OLMAZ**; yalnız Dynamic Content değişir. Hero yalnız "AI Önerilerim"de mount edilir; diğer kategorilerde **MatchList** (Hero yok).
- **Not:** Şu an `page.tsx` doğrudan AI Önerilerim bloklarını diziyor (DynamicContentHost henüz yok — yapısal katman, görsel bloklar tamamlanınca eklenecek).
- **Layout:** `app/layout.tsx` telefon çerçevesini (`max-w-[430px]`) ve global `<BottomNav/>`'i sağlar. `AppShell` sticky header slotu + doğru padding'li scroll alanı verir.

## 5. Reuse Map
**♻️ Reuse (aynen):** DiscoverHeader, BottomNav, AICategorySelector+CategoryCard, tüm Hero componentleri, PrimaryCtaButton, ConfidenceRing, TeamLogoPair, GlassCard, SectionHeader, Badge, StatPill, LiveUpdateBadge, Sparkline, TextLink, MetricStatCard, WinProbabilityCard, AICommentSection, PredictionCard, TeamCrest, TagChip, LoadingState, ErrorState, ConfidenceDots, icons.tsx.
**🔧 Update:** `page.tsx` (Home kompozisyonu büyüyecek), TrendingMatches/TrendingMatchCard/OtherMatches → FeaturedMatchesSection/FeaturedMatchCard, feed/MatchCard+QuickSignals+ReasonChips → MatchListItem tohumu.
**🆕 New (kalan):** RadarSection+RadarVisual+RadarSignalCard, AIExpectationsSection (atomlar hazır, section dosyası YOK), AIScenariosSection+ScenarioCard, MarketMovementsSection+MarketRow, AIComboSection+ComboLegItem+ComboTotalCard+StarRating, FeaturedMatchesSection+FeaturedMatchCard, MatchList+MatchListItem, DynamicContentHost + kategori view'ları, CouponFlow (+CouponCard/Editor/Detail/Ready), FilterPanel(+RangeSlider), `useCategoryContent` hook.

## 6. Backend Durumu
**Konum:** `C:\Users\dikim\Desktop\FORMAX_Backend` (.NET Clean Architecture: Formax.Domain/Application/Infrastructure/API). Çalışan app: `http://127.0.0.1:5063`.
**Tamamlanan (build 0 hata):**
- Radar v2/v2.1/v2.2/v3 — MatchIntelligenceContextBuilder, ReasoningEngine (IntelligencePack), ScenarioRanking (MarketProbabilityEngine), RadarNarrativePipeline + HttpLLMClient (Mock/Ollama/OpenAI, varsayılan Mock→fallback).
- Data Engine v1 — Global Fixture Discovery (FormaxMatchIdFactory, TheSportsDB/FootballData provider'ları, `Fixtures` tablosu, scheduler).
- Data Engine v2 — Global News Discovery (Google/Bing news provider, `MatchNewsArticles`, dedup/cluster/confidence).
- Data Engine v2.1 — Match Intelligence (SignalExtractor, SourceQuality, FreshnessPolicy, `MatchEvidenceRecords`).
- Evidence→Reasoning entegrasyonu (`MatchDetailDto.AiNarrative`; `GetMatchDetailAIContextUseCase.ExecuteAsync` host).
- **Player Intelligence Engine v1** — `Formax.Application/Services/Players/Intelligence/` (PlayerIntelligence, PlayerSignal, IPlayerIntelligenceEngine/PlayerIntelligenceEngine, IPlayerStatsProvider/PlayerStats, `ApiFootballPlayerStatsProvider`). Graceful fallback, IMemoryCache. IntelligenceScore + PrimaryReason/SecondaryReason + ExternalPlayerId? + LastUpdated.
- **Hero Intelligence System (KISMİ, DONDURULDU):** `Formax.Application/Services/Hero/` — HeroSelectionEngine (HeroSelectionResult{HomeHero?,AwayHero?,HeroReasonType,HeroConfidence}, HeroReasonType enum, HeroSelectionWeights), HeroVisualIdentityEngine (VisualLayer/VisualGradient + 6 alt-kimlik + seed renk tablosu), HeroNarrativeBuilder.

**DONDURULAN / artık geliştirilmeyecek:** Hero Backend katmanı. **Yapılmadı ve yapılmayacak (donduruldu):** HeroAICommentBuilder, HeroAggregate, HeroDto, HeroDtoMapper.
**Hero Backend neden donduruldu:** Kullanıcı kararı — Hero için backend yeterli seviyeye ulaştı; ürün önceliği **UI'ı tamamlamaya** kaydı. Şu an frontend statik/demo veri kullanıyor; backend entegrasyonu sonraki faza bırakıldı.
**Build komutları:** `dotnet build Formax.API/Formax.API.csproj -c Debug -o <scratch>` (çalışan app bin'i kilitler → ayrı -o). `.slnx` MSBuild'de çalışmaz. Migration: `dotnet ef migrations add X --project Formax.Infrastructure --startup-project Formax.Infrastructure`.

## 7. Frontend Durumu
**Konum:** `formax-web/` (Next.js 16 App Router + React 19 + Tailwind v4 + Framer Motion).
**Tamamlanan componentler:**
- `components/layout/AppShell.tsx`
- `app/globals.css` (token'lar + `.fx-*` reçeteleri + layout token'ları)
- `components/ui/`: GlassCard, SectionHeader, Badge, StatPill, LiveUpdateBadge, Sparkline, TextLink, MetricStatCard, TeamLogoPair, PrimaryCtaButton
- `components/discover/`: DiscoverHeader(🔒), AICategorySelector+CategoryCard, AICommentSection, PredictionCard, AIPredictionsSection, WinProbabilityCard, icons.tsx
- `components/discover/hero/*` (tümü, 🔒)
- `components/ui/BottomNav.tsx` (🔒)
- `app/page.tsx` sırası: AICategorySelector → HeroSection → **AIPredictionsSection** → AICommentSection
**En son tamamlanan:** **AI Olası Sonuçlar** (AIPredictionsSection + PredictionCard) — `next build` 0, `tsc` 0, preview alındı.
**Eksik componentler (AI Önerilerim, kilitli sırada):** Radar'ın Öne Çıkardıkları → AI Beklentileri (AIExpectationsSection dosyası yok; atomlar hazır: Sparkline/MetricStatCard/WinProbabilityCard/TextLink) → AI Senaryoları → Oran Hareketleri → Günün AI Kombini → Sana Özel Maçlar. Sonra yapısal: DynamicContentHost + diğer kategori view'ları + Kupon akışı + Filtrele.
**Frontend build/kontrol:** `cd formax-web && npx tsc --noEmit` + `npx next build`. Preview MCP (`.claude/launch.json` "formax-web", port 3000).

## 8. Kalan İş Listesi (sırayla)
01. **Radar'ın Öne Çıkardıkları** — RadarSection + RadarVisual + RadarSignalCard (referans: SON DAKİKA/Sakatlık, TAKTİK ANALİZ, MAÇ DİNAMİĞİ, HAVA DURUMU · severity renkli).
02. **AI Beklentileri** — AIExpectationsSection (mevcut atomları besle: MetricStatCard ×3 [xG/Topa Sahip/Şut] + WinProbabilityCard).
03. **AI Senaryoları** — AIScenariosSection + ScenarioCard ×3.
04. **Oran Hareketleri** — MarketMovementsSection + MarketRow (Market/Önceki/Güncel/Değişim/Trend).
05. **Günün AI Kombini** — AIComboSection + ComboLegItem ×4 + ComboTotalCard + StarRating.
06. **Sana Özel Maçlar** — FeaturedMatchesSection + FeaturedMatchCard (yatay; ÖNE ÇIKAN + tag + İlgi Skoru bar).
07. **AICategorySelector** referans hizalama (referans PNG'de ikon-üstte kart; mevcut chip stili — birebir uyum kontrolü).
08. **DynamicContentHost** + kategori view'ları (Today/High/Surprise/Derby = MatchList; Hero yok).
09. **MatchList + MatchListItem**.
10. **Kupon akışı** (CouponFlow: Öneriler→Kuponlarım→Düzenle→Detay→Hazır).
11. **Filtrele** (FilterPanel + RangeSlider).

## 9. Yeni Sohbet Kuralları
Yeni sohbette YAPMA (hepsi kilitlendi): ❌ Product Architecture ❌ Backend önerisi ❌ UX önerisi ❌ Reuse analizi ❌ Component analizi ❌ Kod öncesi tekrar analiz ❌ Yeni fikir ❌ "Bence şöyle daha iyi olur." Yalnızca **implementasyon** yapılır; referans PNG birebir uygulanır.

## 10. Yeni Sohbetin İlk Görevi
Referans Home ekranını yukarıdan aşağıya birebir tamamlamaya **AI Olası Sonuçlar'dan SONRAKİ ilk eksik componentle** devam et:
- **İLK:** `01 — Radar'ın Öne Çıkardıkları` (RadarSection).
- **Sonra sırayla:** AI Beklentileri → AI Senaryoları → Oran Hareketleri → Günün AI Kombini → Sana Özel Maçlar → (yapısal) AICategorySelector uyum → DynamicContentHost → MatchList → Kupon akışı → Filtrele.
- Kilitli Header/BottomNav/Hero'ya dokunma. Mevcut componentleri reuse et; yalnız eksikleri referansa göre yaz.

## 11. Çalışma Disiplini (her component sonunda ZORUNLU rapor)
Her component bitince şu formatta **gerçek doğrulama** raporu ver (tek satır ✓ yeterli DEĞİL):
```
COMPONENT
  Adı: …
  Durum: (Yeni / Güncellendi / Reuse)
  Dosya: …
  Kullanılan mevcut componentler: …
  Yeni oluşturulan componentler: …

DOĞRULAMA
  ✓ next build sonucu: (EXIT 0 / hata)
  ✓ TypeScript sonucu: (tsc 0 / hata)
  ✓ Responsive ekran görüntüsü: (375px preview)
  ✓ Preview ekran görüntüsü: (430px preview)
  ✓ Referans PNG karşılaştırması

REFERANS KARŞILAŞTIRMASI
  Header %… · Spacing %… · Typography %… · Glass %… · Glow %… · Border %… · Component Order %…
  Eksikler: …
```
**Onay almadan sonraki componente GEÇME.**

---

### Ek Teknik Notlar (yeni sohbet için kritik)
- **Design token'lar** (`globals.css`): `bg-deep #07070C`, `bg-glass #14141C`, `neon #2EE66E`, `neon-deep #34D27A`, `signal-purple #A855F7`, `signal-amber #F5A623`, `signal-red #F5454D`, `signal-blue #4DA6FF`, `signal-yellow #F5C84B`, `text-primary/secondary/muted`. Tailwind: `bg-bg-deep`, `text-neon`, `text-signal-purple`, `border-white/[0.06]` vb.
- **Efekt sınıfları:** `.fx-glass`, `.fx-glow-green/purple/amber/soft-green`, `.fx-icon-glow-green`, `.fx-ring-glow-green`, `.fx-ambient`, `.fx-stadium`, `.fx-player-blue/red`, `.fx-grad-cta`, `.fx-section-glow`.
- **Layout token'ları:** `--app-max-width:430px`, `--header-height:60px`, `--bottom-nav-height:84px`, `--safe-top/bottom`. Radius: `rounded-[var(--radius-section)]` (28px), kartlar `rounded-2xl`.
- **Section kalıbı:** `GlassCard sectionGlow className="p-5"` + `SectionHeader` (icon/accent/title/right) + içerik. Yatay scroll: `-mx-4 px-4 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden`.
- **Statik veri:** iş mantığı/API yok; referans PNG değerleri component içinde sabit (ileride backend besleyecek).
- **Preview:** `.claude/launch.json` → server adı `formax-web`, port 3000. Next dev "N" rozeti sol-altta görünür (prod'da yok).
