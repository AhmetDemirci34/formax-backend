using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Services.Fixtures;
using Formax.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Formax.Infrastructure.Maintenance
{
    /// <summary>
    /// Tek kimlik otoritesine geçiş — bir defalık, idempotent yeniden-anahtarlama (re-key).
    ///
    /// Eski FORMAX_MATCH_ID'ler lig-bağımlı + gün-kaymalı algoritmayla üretilmişti; yeni
    /// deterministik algoritma (<see cref="FormaxMatchIdFactory"/>) lig-bağımsız + Kind-güvenlidir.
    /// Bu servis mevcut <c>Fixtures</c> verisinden (ev + deplasman + UTC gün) her fixture'ın YENİ
    /// kimliğini gerçek factory ile yeniden hesaplar ve üç tabloyu hizalar:
    ///   • Fixtures.FormaxMatchId            (news üretiminin anahtarı)
    ///   • MatchEvidenceRecords.FormaxMatchId (Evidence Store)
    ///   • MatchNewsArticles.FormaxMatchId    (Haber Store)
    ///
    /// GARANTİLER:
    ///   • Hesap runtime tüketicileriyle BİREBİR aynı kod yolunu kullanır (aynı id garanti edilir).
    ///   • Hiçbir kayıt SİLİNMEZ. Kaynağı (Fixture) olmayan yetim kanıtlar DOKUNULMAZ (kayıpsız).
    ///   • ContentHash global unique olduğundan re-key çakışma üretmez.
    ///   • Fixtures.FormaxMatchId unique → önce geçici değer, sonra kesin değer (transient çakışma yok).
    ///   • Deterministik → ikinci çalıştırma no-op (idempotent).
    /// </summary>
    public sealed class CanonicalIdentityRekeyService
    {
        private readonly FormaxDbContext _db;
        private readonly FormaxMatchIdFactory _idFactory;

        public CanonicalIdentityRekeyService(FormaxDbContext db, FormaxMatchIdFactory idFactory)
        {
            _db = db;
            _idFactory = idFactory;
        }

        private sealed record Mapping(int FixtureId, string OldId, string NewId, string Home, string Away, DateTime KickoffUtc);

        public sealed record RekeyReport(
            int FixtureCount,
            int ChangedCount,
            int EvidenceMoved,
            int NewsMoved,
            int OrphanEvidenceIds,
            bool Applied,
            IReadOnlyList<object> Sample);

        public async Task<RekeyReport> RunAsync(bool apply, CancellationToken ct = default)
        {
            var fixtures = await _db.Fixtures
                .Select(f => new { f.Id, f.FormaxMatchId, f.HomeTeam, f.AwayTeam, f.KickoffUtc })
                .ToListAsync(ct);

            var mappings = fixtures
                .Select(f => new Mapping(
                    f.Id,
                    f.FormaxMatchId,
                    _idFactory.Create(f.KickoffUtc, f.HomeTeam, f.AwayTeam),
                    f.HomeTeam, f.AwayTeam, f.KickoffUtc))
                .ToList();

            var changed = mappings
                .Where(m => !string.Equals(m.OldId, m.NewId, StringComparison.Ordinal))
                .ToList();

            // Yeni kimlikler benzersiz olmalı (aksi halde farklı maçlar aynı id'ye düşerdi → durdur).
            var dupNewId = mappings.GroupBy(m => m.NewId).FirstOrDefault(g => g.Count() > 1);
            if (dupNewId != null)
                throw new InvalidOperationException(
                    $"Yeni kimlik çakışması: {dupNewId.Key} → {dupNewId.Count()} fixture. Migration durduruldu.");

            // Etki: kaç kanıt/haber yeni id'ye taşınacak.
            var oldIds = changed.Select(m => m.OldId).Distinct(StringComparer.Ordinal).ToList();
            var evidenceMoved = oldIds.Count == 0 ? 0
                : await _db.MatchEvidenceRecords.CountAsync(e => oldIds.Contains(e.FormaxMatchId), ct);
            var newsMoved = oldIds.Count == 0 ? 0
                : await _db.MatchNewsArticles.CountAsync(n => oldIds.Contains(n.FormaxMatchId), ct);

            // Fixture'ı olmayan (zaten yetim) kanıt id sayısı — dokunulmaz, bilgi amaçlı.
            var fixtureNewIds = mappings.Select(m => m.NewId).ToHashSet(StringComparer.Ordinal);
            var fixtureOldIds = mappings.Select(m => m.OldId).ToHashSet(StringComparer.Ordinal);
            var allEvidenceIds = await _db.MatchEvidenceRecords
                .Select(e => e.FormaxMatchId).Distinct().ToListAsync(ct);
            var orphanEvidenceIds = allEvidenceIds
                .Count(id => !fixtureNewIds.Contains(id) && !fixtureOldIds.Contains(id));

            var sample = changed
                .Take(15)
                .Select(m => (object)new
                {
                    m.Home,
                    m.Away,
                    Date = m.KickoffUtc.ToString("yyyy-MM-dd"),
                    m.OldId,
                    m.NewId
                })
                .ToList();

            if (apply && changed.Count > 0)
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                // 1) Evidence & News: değere göre yeniden anahtarla (FormaxMatchId non-unique → güvenli).
                foreach (var m in changed)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "UPDATE MatchEvidenceRecords SET FormaxMatchId = {0} WHERE FormaxMatchId = {1}",
                        new object[] { m.NewId, m.OldId }, ct);
                    await _db.Database.ExecuteSqlRawAsync(
                        "UPDATE MatchNewsArticles SET FormaxMatchId = {0} WHERE FormaxMatchId = {1}",
                        new object[] { m.NewId, m.OldId }, ct);
                }

                // 2) Fixtures: unique index'i korumak için önce geçici (benzersiz) değer, sonra kesin id.
                await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE Fixtures SET FormaxMatchId = CONCAT('TMP-', CAST(Id AS nvarchar(12)))", ct);
                foreach (var m in mappings)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "UPDATE Fixtures SET FormaxMatchId = {0} WHERE Id = {1}",
                        new object[] { m.NewId, m.FixtureId }, ct);
                }

                await tx.CommitAsync(ct);
            }

            return new RekeyReport(
                fixtures.Count,
                changed.Count,
                evidenceMoved,
                newsMoved,
                orphanEvidenceIds,
                apply && changed.Count > 0,
                sample);
        }
    }
}
