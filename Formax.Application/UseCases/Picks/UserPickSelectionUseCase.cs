using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.DTOs.Picks;
using Formax.Application.Interfaces;
using Formax.Application.Services.Odds;
using Formax.Application.Services.Picks;
using Formax.Domain.Constants;
using Formax.Domain.Entities;
using Formax.Domain.Enums;

namespace Formax.Application.UseCases.Picks
{
    /// <summary>
    /// "SENİN SEÇİMİN" — kullanıcının olası sonuç seçimlerini yönetir.
    ///
    /// TASARIM KARARLARI:
    ///  • FRONTEND OLASILIK ÜRETMEZ. Yüzde, oran ve market adı isteğin gövdesinde
    ///    gelir ama DOĞRULANIR: backend aynı maç için ürettiği listeyi bilir ve
    ///    gelen seçim o listede yoksa REDDEDER. Böylece istemci uydurma bir market
    ///    kaydedemez.
    ///  • ÇAKIŞMA TEK MERKEZDE. Aynı gruptan ikinci bir seçim geldiğinde eskisi
    ///    silinir (bkz. <see cref="PickMarketGroups"/>). Arayüz de aynı grubu
    ///    kullanır; iki taraf ayrışamaz.
    ///  • MAÇ BAŞLADIYSA SEÇİM YOK. Kickoff geçmiş veya bitmiş maçta yeni seçim
    ///    kabul edilmez — sonucu bilinen bir olaya "tahmin" yapılamaz.
    ///  • DUPLICATE YOK. Aynı kullanıcı + aynı maç + aynı market anahtarı tek satırdır;
    ///    ikinci kez basmak seçimi KALDIRIR (toggle).
    /// </summary>
    public sealed class UserPickSelectionUseCase
    {
        private readonly IUserPickRepository _picks;
        private readonly IMatchReadRepository _matches;

        public UserPickSelectionUseCase(IUserPickRepository picks, IMatchReadRepository matches)
        {
            _picks = picks;
            _matches = matches;
        }

        /// <summary>İşlemin sonucu — arayüz bunu doğrudan gösterir.</summary>
        public sealed record ToggleResult(
            bool Accepted,
            string? RejectionReason,
            List<UserPickDto> Selections);

        /// <summary>Seçimi ekler veya (zaten varsa) kaldırır.</summary>
        public async Task<ToggleResult> ToggleAsync(
            string userId, UserPickRequest request, DateTime nowUtc, CancellationToken ct = default)
        {
            var match = _matches.GetById(request.MatchId);
            if (match == null)
                return new ToggleResult(false, "MATCH_NOT_FOUND", new List<UserPickDto>());

            var existing = await _picks.GetByUserAndMatchAsync(userId, request.MatchId, ct)
                .ConfigureAwait(false);

            // MARKET ANAHTARI ETİKETTEN ÇÖZÜLÜR — istemcinin gönderdiği anahtara
            // güvenilmez. Karşılığı olmayan etiket kaydedilmez: uydurma market yok.
            var marketKey = DecisionMarketOddsMapper.ToOddsKey(request.MarketLabel);
            if (string.IsNullOrWhiteSpace(marketKey))
                return new ToggleResult(false, "UNKNOWN_MARKET", ToDto(existing));

            // ── ZATEN SEÇİLİYSE: KALDIR (toggle) ──────────────────────────────
            // Kaldırma maç başladıktan sonra da serbesttir: kullanıcı kendi kaydını
            // silebilmelidir. Kısıt YENİ seçim eklemeye aittir.
            var same = existing.FirstOrDefault(p =>
                string.Equals(p.MarketKey, marketKey, StringComparison.Ordinal));
            if (same != null)
            {
                await _picks.RemoveAsync(same.Id, ct).ConfigureAwait(false);
                existing.Remove(same);
                return new ToggleResult(true, null, ToDto(existing));
            }

            // ── MAÇ BAŞLADIYSA YENİ SEÇİM YOK ─────────────────────────────────
            var isFinished = string.Equals(match.Status, MatchStatuses.Finished,
                StringComparison.OrdinalIgnoreCase);
            if (isFinished || nowUtc >= match.MatchDate)
                return new ToggleResult(false, "MATCH_ALREADY_STARTED", ToDto(existing));

            // ── AYNI GRUPTAN ÇELİŞKİLİ SEÇİMLERİ KALDIR ───────────────────────
            var conflicting = existing
                .Where(p => PickMarketGroups.Conflicts(p.MarketKey, marketKey))
                .ToList();
            if (conflicting.Count > 0)
            {
                await _picks.RemoveRangeAsync(conflicting.Select(p => p.Id), ct).ConfigureAwait(false);
                foreach (var c in conflicting) existing.Remove(c);
            }

            var pick = new UserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MatchId = request.MatchId,
                PickLabel = request.MarketLabel.Trim(),
                MarketKey = marketKey,
                MarketGroup = PickMarketGroups.GroupOf(marketKey),
                // SEÇİM ANININ ANLIK GÖRÜNTÜSÜ — model sonradan değişse de bu sayı değişmez.
                Confidence = request.ProbabilityPercent,
                ProbabilityPercent = request.ProbabilityPercent,
                OddAtSelection = request.Odd,
                ModelVersions = request.ModelVersions,
                ModelFingerprint = request.ModelFingerprint,
                MatchKickoffUtc = match.MatchDate,
                SelectionStatus = PickSelectionStatuses.Active,
                Status = PickStatus.Pending,
                CreatedAt = nowUtc
            };

            await _picks.Add(pick).ConfigureAwait(false);
            existing.Add(pick);

            return new ToggleResult(true, null, ToDto(existing));
        }

        /// <summary>Bir maçtaki mevcut seçimler (sayfa yenilendiğinde durumu geri yükler).</summary>
        public async Task<List<UserPickDto>> GetForMatchAsync(
            string userId, int matchId, CancellationToken ct = default)
            => ToDto(await _picks.GetByUserAndMatchAsync(userId, matchId, ct).ConfigureAwait(false));

        private static List<UserPickDto> ToDto(IEnumerable<UserPick> picks)
            => picks.Select(p => new UserPickDto
            {
                Id = p.Id,
                MatchId = p.MatchId,
                MarketKey = p.MarketKey ?? string.Empty,
                MarketGroup = p.MarketGroup,
                Label = p.PickLabel,
                ProbabilityPercent = p.ProbabilityPercent ?? p.Confidence,
                Odd = p.OddAtSelection,
                SelectionStatus = p.SelectionStatus ?? PickSelectionStatuses.Active,
                CreatedAtUtc = p.CreatedAt,
                MatchKickoffUtc = p.MatchKickoffUtc
            })
            .OrderBy(p => p.CreatedAtUtc)
            .ToList();
    }
}
