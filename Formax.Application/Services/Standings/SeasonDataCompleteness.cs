using System;
using System.Collections.Generic;
using System.Linq;
using Formax.Domain.Constants;
using Formax.Domain.Entities;

namespace Formax.Application.Services.Standings
{
    /// <summary>
    /// SEZON VERİ TAMLIĞI — "puan durumu tablosu GÜNCEL mi?"
    ///
    /// DÜZELTME 31.08.2026 — ERTELENMİŞ MAÇ TABLOYU EKSİK YAPMAZ.
    /// Önceki kural "başlama saati geçmiş her maçın sonucu depoda olmalı" diyordu ve
    /// ERTELENMİŞ maçı da eksik sayıyordu. Bu YANLIŞ: ertelenen maç oynanmamıştır,
    /// sonucu yoktur ve BEKLENMEZ; ligde takımların oynadığı maç sayısının farklı olması
    /// normaldir. Ölçülen sonuç: Eredivisie 2026/27 tablosu, yalnızca NEC Nijmegen–Excelsior
    /// (22.08, Postponed) yüzünden IsComplete=false görünüyordu — oysa tablo günceldi.
    ///
    /// KESİN HESAP:
    ///   Expected = kickoff + pay geçmiş  VE  oynanması beklenen (Postponed/Cancelled/
    ///              Abandoned DEĞİL)
    ///   Missing  = Expected içinde kesin sonucu (Status=Finished) bulunmayanlar
    ///   IsComplete = Missing == 0
    ///
    /// Oynanmamış maçlar ayrı ayrı SAYILIR (gizlenmez): tablo güncel olabilir ama
    /// "1 ertelenmiş maç bulunuyor" bilgisi kullanıcıya verilebilir olmalıdır.
    ///
    /// Bu sınıf yeni veri çekmez; verilen fikstür listesini sayar.
    /// </summary>
    public static class SeasonDataCompleteness
    {
        /// <summary>
        /// Bir maçın bitmiş SAYILMASI için kickoff üzerinden geçmesi gereken süre (dk).
        /// 90 dk oyun + devre arası + uzatmalar + sağlayıcı gecikmesi. Bu süre dolmadan
        /// "sonuç eksik" denmez (yeni başlamış maç eksiklik değildir).
        /// </summary>
        public const int SettleMarginMinutes = 210;

        public sealed record Result(
            int Expected,
            int Included,
            int Missing,
            bool IsComplete,
            DateTime CheckedAtUtc,
            IReadOnlyList<int> MissingMatchIds,
            int Postponed,
            int Cancelled,
            int Abandoned,
            int StaleResult)
        {
            public static Result Empty(DateTime now) =>
                new(0, 0, 0, true, now, Array.Empty<int>(), 0, 0, 0, 0);

            /// <summary>Oynanmamış (ertelenmiş/iptal/yarıda kalmış) toplam maç sayısı.</summary>
            public int NotPlayed => Postponed + Cancelled + Abandoned;
        }

        /// <summary>
        /// AŞAMA-DUYARLI TAMLIK — UEFA turnuvalarında yalnız LİG AŞAMASI değerlendirilir.
        ///
        /// Eleme ve knockout maçlarının eksik sonucu puan durumu tamlığını BOZMAZ: o maçlar
        /// tabloya hiç girmez, dolayısıyla tablonun "güncel" olup olmadığını da belirleyemez.
        /// Ölçüldü (01.09.2026): aşama süzgeci yokken UCL 4/90, UEL 13/80, UECL 34/246 "eksik"
        /// görünüyordu — oysa bu maçların hiçbiri tabloya girmiyordu.
        /// </summary>
        public static Result EvaluateForStandings(
            int leagueId, IReadOnlyList<Match> fixtures, DateTime nowUtc)
        {
            if (fixtures == null || fixtures.Count == 0) return Result.Empty(nowUtc);

            if (!Domain.Constants.LockedCompetitions.IsUefa(leagueId))
                return Evaluate(fixtures, nowUtc);

            var leaguePhaseOnly = fixtures
                .Where(f => CompetitionPhaseResolver.Resolve(leagueId, f.Round)
                            == CompetitionPhase.LeaguePhase)
                .ToList();

            return Evaluate(leaguePhaseOnly, nowUtc);
        }

        /// <summary>
        /// <paramref name="fixtures"/> = sezon içinde başlama saati geçmiş TÜM lig maçları
        /// (durumu ne olursa olsun). Kesinleşmiş sayılan tek durum: Status=Finished.
        /// </summary>
        public static Result Evaluate(IReadOnlyList<Match> fixtures, DateTime nowUtc)
        {
            if (fixtures == null || fixtures.Count == 0) return Result.Empty(nowUtc);

            var cutoff = nowUtc.AddMinutes(-SettleMarginMinutes);

            // Kickoff + pay geçmiş olanlar. Henüz oynanmakta olan maç değerlendirmeye
            // girmez (eksiklik sayılmaz).
            var pastDue = fixtures.Where(f => f.MatchDate <= cutoff).ToList();
            if (pastDue.Count == 0) return Result.Empty(nowUtc);

            // OYNANMAYACAK MAÇLAR — beklenenin DIŞINDA tutulur, ama sayılır.
            var postponed = pastDue.Count(f => Is(f, MatchStatuses.Postponed));
            var cancelled = pastDue.Count(f => Is(f, MatchStatuses.Cancelled));
            var abandoned = pastDue.Count(f => Is(f, MatchStatuses.Abandoned));

            var expected = pastDue.Where(f => !MatchStatuses.IsNotPlayed(f.Status)).ToList();
            if (expected.Count == 0)
                return Result.Empty(nowUtc) with
                {
                    Postponed = postponed, Cancelled = cancelled, Abandoned = abandoned
                };

            var missingIds = expected
                .Where(f => !Is(f, MatchStatuses.Finished))
                .Select(f => f.Id)
                .ToList();

            // SONUÇ ALIM HATTININ BORCU: oynanması beklenen ama hâlâ NotStarted/Live
            // duran maçlar. Missing ile AYNI kümedir — ta ki sağlayıcı eşleyicimizde bir
            // boşluk oluşana kadar. İkisi ayrıştığı an, Finished da olmayan / bilinen bir
            // beklemede durumu da olmayan bir maç var demektir; bu, status eşlemesinde
            // kapatılmamış bir kod olduğunun sinyalidir.
            var stale = expected.Count(f => MatchStatuses.IsAwaitingResult(f.Status));

            return new Result(
                Expected:        expected.Count,
                Included:        expected.Count - missingIds.Count,
                Missing:         missingIds.Count,
                IsComplete:      missingIds.Count == 0,
                CheckedAtUtc:    nowUtc,
                MissingMatchIds: missingIds,
                Postponed:       postponed,
                Cancelled:       cancelled,
                Abandoned:       abandoned,
                StaleResult:     stale);
        }

        private static bool Is(Match m, string status)
            => string.Equals(m.Status, status, StringComparison.OrdinalIgnoreCase);
    }
}
