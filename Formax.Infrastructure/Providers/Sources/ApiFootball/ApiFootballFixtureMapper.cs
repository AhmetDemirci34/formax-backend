using System;
using System.Collections.Generic;
using System.Globalization;
using Formax.Application.DTOs.Fixtures;
using Formax.Infrastructure.Normalize.Mapping;
using Formax.Infrastructure.Normalize.Raw;

namespace Formax.Infrastructure.Providers.Sources.ApiFootball;

/// <summary>
/// api-football fikstürlerini ortak <see cref="RawFixture"/> modeline çevirir.
/// Provider-özgü tek yer burasıdır.
///
/// Girdi, <see cref="ApiFootballGdpProvider"/>'ın taşıdığı tipli <see cref="SportsFixtureResult"/>
/// listesidir (ham JSON değil) — bu liste mevcut sağlayıcının kendi eşlemesinden geçmiş durumdadır.
/// Burada iş kuralı, isim düzeltmesi veya uydurma değer YOKTUR; yalnız alan eşlemesi yapılır.
/// Eksik alanlar null bırakılır.
///
/// KRİTİK: <see cref="RawFixture.ProviderMatchId"/> = api-football fixture id. Kimlik zinciri
/// (Identity → ProviderReference → Persist → Match.ExternalMatchId) bu değere dayanır.
/// </summary>
public sealed class ApiFootballFixtureMapper : IProviderMapper<RawFixture>
{
    public string ProviderName => ApiFootballGdpProvider.Name;

    public IReadOnlyList<RawFixture> Map(object? payload)
    {
        if (payload is not IEnumerable<SportsFixtureResult> fixtures)
            return Array.Empty<RawFixture>();

        var result = new List<RawFixture>();
        foreach (var f in fixtures)
        {
            if (f is null || string.IsNullOrWhiteSpace(f.ExternalMatchId))
                continue; // kimliksiz kayıt kimlik zincirine giremez

            result.Add(new RawFixture
            {
                ProviderName = ProviderName,
                ProviderMatchId = f.ExternalMatchId,
                Competition = NullIfBlank(f.LeagueName),
                Season = null, // api-football sezonu bu DTO'da taşınmıyor — uydurulmaz
                Round = NullIfBlank(f.Round),
                HomeTeam = NullIfBlank(f.HomeTeamName),
                AwayTeam = NullIfBlank(f.AwayTeamName),
                // Ham başlama zamanı metni (UTC, ISO-8601); ayrıştırma Normalize Engine'in işi.
                Kickoff = f.MatchDate == default
                    ? null
                    : f.MatchDate.ToString("o", CultureInfo.InvariantCulture),
                Status = NullIfBlank(f.Status),
                Venue = NullIfBlank(f.Venue),
                HomeScore = f.HomeScore,
                AwayScore = f.AwayScore
            });
        }

        return result;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
