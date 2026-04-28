using System;
using Formax.Application.DTOs.Matches;
using Formax.Application.Interfaces;

namespace Formax.Application.Services.Sapma
{
    public sealed class SapmaMotor : ISapmaMotor
    {
        private readonly IOynanmaSinyalProvider _oynanmaProvider;
        private readonly IGucSkoruCalculator _gucSkoruCalculator;
        private readonly IMatchOynanmaSnapshotWriter _snapshotWriter;

        public SapmaMotor(
            IOynanmaSinyalProvider oynanmaProvider,
            IGucSkoruCalculator gucSkoruCalculator,
            IMatchOynanmaSnapshotWriter snapshotWriter)
        {
            _oynanmaProvider = oynanmaProvider;
            _gucSkoruCalculator = gucSkoruCalculator;
            _snapshotWriter = snapshotWriter;
        }

        public SapmaMotorResult CalculateForListItem(MatchListItemDto match)
        {
            var oynanma = _oynanmaProvider.GetAsync(match.MatchId)
                .GetAwaiter().GetResult();

            var nowUtc = DateTime.UtcNow;
            var ageSeconds = oynanma == null
                ? int.MaxValue
                : (int)Math.Max(0, (nowUtc - oynanma.LastUpdatedAtUtc).TotalSeconds);

            var freshness = ageSeconds <= 45 ? "Live"
                          : ageSeconds <= 300 ? "LastKnown"
                          : "Stale";

            var analysisMuted = ageSeconds > 120;

            var oynanmaSkoru = Clamp01to100(oynanma?.Intensity ?? 50);
            var oynanmaYonu = NormalizeSide(oynanma?.Side ?? "Denge");

            if (oynanmaSkoru >= 45 && oynanmaSkoru <= 55)
                oynanmaYonu = "Denge";

            if (analysisMuted)
            {
                oynanmaSkoru = 50;
                oynanmaYonu = "Denge";
            }

            if (oynanma != null && !analysisMuted && ageSeconds <= 45)
            {
                _snapshotWriter.TryWriteAsync(match.MatchId, oynanmaSkoru, nowUtc, "Live")
                    .GetAwaiter().GetResult();
            }

            var kickoffUtc = match.StartTime;
            var timeToKickoff = kickoffUtc - nowUtc;

            if (IsPreMatchStatus(match.Status)
                && timeToKickoff > TimeSpan.Zero
                && timeToKickoff <= TimeSpan.FromMinutes(10)
                && oynanma != null
                && !analysisMuted)
            {
                _snapshotWriter.TryWritePreMatchFinalAsync(match.MatchId, oynanmaSkoru, nowUtc)
                    .GetAwaiter().GetResult();
            }

            var guc = _gucSkoruCalculator.CalculateForMatch(match.MatchId);
            var gucSkoru = Clamp01to100(guc.GucSkoru);
            var gercekGucYonu = NormalizeSide(guc.GercekGucYonu);

            var sapma = Math.Abs(oynanmaSkoru - gucSkoru);

            if (oynanmaYonu != "Denge"
                && gercekGucYonu != "Denge"
                && oynanmaYonu != gercekGucYonu)
            {
                sapma = Math.Min(100, sapma + 25);
            }

            var bolge = ResolveBolge(sapma);

            // 🔥 Yeni sessizlik eşiği (40)
            var sessiz = analysisMuted || sapma < 40;

            var metin = BuildCopy(
                bolge,
                sessiz,
                sapma,
                analysisMuted,
                freshness,
                ageSeconds == int.MaxValue ? 0 : ageSeconds,
                oynanmaYonu,
                gercekGucYonu);

            return new SapmaMotorResult
            {
                OynanmaSkoru = oynanmaSkoru,
                GucSkoru = gucSkoru,
                Sapma = sapma,
                OynanmaYonu = oynanmaYonu,
                GercekGucYonu = gercekGucYonu,
                SapmaBolgesi = bolge,
                SessizMi = sessiz,
                SapmaMetni = metin,
                OynanmaFreshness = freshness,
                OynanmaAgeSeconds = ageSeconds == int.MaxValue ? 0 : ageSeconds,
                AnalysisMuted = analysisMuted
            };
        }

        private static string ResolveBolge(int sapma)
        {
            if (sapma >= 75) return "Yüksek Sapma";
            if (sapma >= 60) return "Yanılma Riski";
            if (sapma >= 40) return "Dikkat Çekici";
            return "Denge Bölgesi";
        }

        private static string BuildCopy(
            string bolge,
            bool sessiz,
            int sapma,
            bool analysisMuted,
            string freshness,
            int ageSeconds,
            string oynanmaYonu,
            string gercekGucYonu)
        {
            if (analysisMuted)
                return $"Şu an veri bayat (yaş: {ageSeconds}s). Ölçüm korunuyor, anlatım sessizde.";

            if (!string.Equals(freshness, "Live", StringComparison.OrdinalIgnoreCase))
                return $"Veri tazeliği Live değil ({freshness}). Ölçüm gösteriliyor.";

            if (sessiz)
            {
                if (sapma <= 7)
                    return "Dengeye yakın. Oynanma sinyali ile güç skoru birbirine yakın.";

                if (sapma <= 15)
                    return "Denge bölgesi. Küçük bir ölçüm farkı var, ama alarm seviyesi değil.";

                return "Denge bölgesi. Ölçüm farkı artıyor; vitrin bunu sadece gösterir.";
            }

            var sideText = oynanmaYonu == "Home"
                ? "ev sahibine"
                : oynanmaYonu == "Away"
                    ? "deplasman tarafına"
                    : "dağılımda";

            var ayrisimVar =
                oynanmaYonu != "Denge" &&
                gercekGucYonu != "Denge" &&
                oynanmaYonu != gercekGucYonu;

            // 🔥 40–59
            if (bolge == "Dikkat Çekici")
            {
                if (oynanmaYonu == "Denge")
                    return $"Dikkat çekici ölçüm farkı (sapma: {sapma}). Denge görünümünde ayrışma artıyor.";

                if (ayrisimVar)
                    return $"Dikkat çekici ölçüm farkı. Çoğunluk {sideText} yoğunlaşmış, güç yönü ({gercekGucYonu}) ile ayrışma başlıyor (sapma: {sapma}).";

                return $"Dikkat çekici ölçüm farkı. Çoğunluk {sideText} yoğunlaşıyor; ayrışma dikkat seviyesinde (sapma: {sapma}).";
            }

            // 🔥 60–74
            if (bolge == "Yanılma Riski")
            {
                if (oynanmaYonu == "Denge")
                    return $"Yanılma riski bölgesi. Denge görünümüne rağmen ölçüm farkı yükseliyor (sapma: {sapma}).";

                if (ayrisimVar)
                    return $"Yanılma riski bölgesi. Çoğunluk {sideText} yoğunlaşmış, güç yönü ({gercekGucYonu}) ile ayrışıyor (sapma: {sapma}).";

                return $"Yanılma riski bölgesi. Çoğunluk {sideText} yoğunlaşmış; ölçüm farkı belirgin (sapma: {sapma}).";
            }

            // 🔥 75+
            return $"Yüksek sapma. Çoğunluk {sideText} yoğunlaşmış; ölçüm farkı çok yüksek (sapma: {sapma}).";
        }

        private static int Clamp01to100(int v) => Math.Max(0, Math.Min(100, v));

        private static string NormalizeSide(string side)
        {
            return side switch
            {
                "Home" => "Home",
                "Away" => "Away",
                _ => "Denge"
            };
        }

        private static bool IsPreMatchStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            var s = status.Trim();
            return s.Equals("PreMatch", StringComparison.OrdinalIgnoreCase)
                   || s.Equals("NotStarted", StringComparison.OrdinalIgnoreCase);
        }
    }
}