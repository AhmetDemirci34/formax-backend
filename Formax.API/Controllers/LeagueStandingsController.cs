using System;
using System.Threading;
using System.Threading.Tasks;
using Formax.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Formax.API.Controllers;

/// <summary>
/// PUAN DURUMU UCU — iç kaynaklı projeksiyon (LeagueStandingsSnapshot).
///
/// GET /api/leagues/{leagueId}/seasons/{seasonId}/standings
///
/// Bu uç HESAP TETİKLEMEZ ve DIŞ İSTEK ÜRETMEZ: snapshot cache/DB'den okunur. Snapshot
/// hiç yoksa bir kez üretilir (lig+sezon başına tek hesap, eşzamanlı istekler bekler).
/// Sezon çözülemezse 404 + açık neden döner; sıra veya tarih UYDURULMAZ.
/// </summary>
[ApiController]
[Route("api/leagues")]
public class LeagueStandingsController : ControllerBase
{
    private readonly ILeagueStandingsService _standings;
    private readonly ILeagueSeasonResolver _seasons;

    public LeagueStandingsController(ILeagueStandingsService standings, ILeagueSeasonResolver seasons)
    {
        _standings = standings;
        _seasons = seasons;
    }

    [HttpGet("{leagueId:int}/seasons/{seasonId:int}/standings")]
    public async Task<IActionResult> Get(int leagueId, int seasonId, CancellationToken ct)
    {
        var snapshot = await _standings.GetAsync(leagueId, seasonId, ct);
        if (snapshot == null)
        {
            var resolution = _seasons.Resolve(leagueId, new DateTime(seasonId, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            return NotFound(new
            {
                leagueId,
                seasonId,
                error = resolution.Error ?? "STANDINGS_NOT_AVAILABLE",
            });
        }

        return Ok(snapshot);
    }

    /// <summary>Mevcut sezonun snapshot'ı — sezon kimliğini çağıranın bilmesi gerekmez.</summary>
    [HttpGet("{leagueId:int}/standings/current")]
    public async Task<IActionResult> GetCurrent(int leagueId, CancellationToken ct)
        => await Get(leagueId, _seasons.SeasonYearOf(DateTime.UtcNow), ct);
}
