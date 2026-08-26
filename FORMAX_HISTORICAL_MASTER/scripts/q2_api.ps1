# Quality gate - targeted api-football pulls: ONLY the missing-score rows and the 4 conflicts.
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Out  = Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
New-Item -ItemType Directory -Force -Path $Raw | Out-Null
$CallLog = Join-Path $Out '_api_call_log.txt'

$cfg = Get-Content (Join-Path $Root 'Formax.API\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ApiKey  = $cfg.ApiFootball.ApiKey
$BaseUrl = $cfg.ApiFootball.BaseUrl
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw 'ApiFootball:ApiKey not found' }

function Invoke-AF {
    param([string]$Path,[hashtable]$Query,[string]$SaveAs)
    $qs = ($Query.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '&'
    $cache = Join-Path $Raw $SaveAs
    if (Test-Path $cache) { Write-Host "CACHED  $Path`?$qs"; return (Get-Content $cache -Raw -Encoding UTF8 | ConvertFrom-Json) }
    $resp = Invoke-RestMethod -Uri "$BaseUrl/$Path`?$qs" -Headers @{ 'x-apisports-key' = $ApiKey } -Method Get -TimeoutSec 60
    $resp | ConvertTo-Json -Depth 30 | Out-File $cache -Encoding utf8
    Add-Content $CallLog "$(Get-Date -Format s)`t$Path`?$qs`tresults=$($resp.results)"
    Write-Host "CALLED  $Path`?$qs -> results=$($resp.results)"
    if ($resp.errors -and $resp.errors.PSObject.Properties.Count -gt 0) { Write-Host "  ERRORS: $($resp.errors | ConvertTo-Json -Compress)" }
    Start-Sleep -Milliseconds 400
    return $resp
}

# canonical league ids: FORMAX locked scope (appsettings Coverage:LeagueAllowList)
# 39 Premier League | 40 Championship | 140 La Liga | 135 Serie A | 78 Bundesliga | 61 Ligue 1 | 203 Super Lig | 88 Eredivisie

# ---- A) missing scores -----------------------------------------------------------------
# 207 of the 209 gaps are one league-season (Super Lig 2025/26) spread over ~30 dates:
# one league+season call costs 1 request, ~30 date calls would cost 30, and 207 per-fixture
# calls are impossible (no provider fixture id exists for these openfootball-sourced rows).
# The two isolated gaps are pulled date-scoped.
Invoke-AF -Path 'fixtures' -Query @{ league=203; season=2025 }                        -SaveAs 'fx_203_2025.json'      | Out-Null
Invoke-AF -Path 'fixtures' -Query @{ league=61;  season=2025; date='2026-05-17' }     -SaveAs 'fx_61_2025_d.json'     | Out-Null
Invoke-AF -Path 'fixtures' -Query @{ league=88;  season=2025; date='2026-05-10' }     -SaveAs 'fx_88_2025_d.json'     | Out-Null

# ---- B) the 4 conflicts, date-scoped (1 request each) ------------------------------------
Invoke-AF -Path 'fixtures' -Query @{ league=135; season=2020; date='2020-09-19' } -SaveAs 'fx_conf_verona_roma.json'   | Out-Null
Invoke-AF -Path 'fixtures' -Query @{ league=78;  season=2024; date='2024-12-14' } -SaveAs 'fx_conf_union_bochum.json'  | Out-Null
Invoke-AF -Path 'fixtures' -Query @{ league=88;  season=2023; date='2023-10-01' } -SaveAs 'fx_conf_nec_vitesse.json'   | Out-Null
Invoke-AF -Path 'fixtures' -Query @{ league=203; season=2018; date='2019-01-18' } -SaveAs 'fx_conf_akhisar_bjk.json'   | Out-Null

Write-Host ""
Write-Host "--- conflict fixtures as api-football reports them:"
foreach ($f in 'fx_conf_verona_roma','fx_conf_union_bochum','fx_conf_nec_vitesse','fx_conf_akhisar_bjk') {
    $j = Get-Content (Join-Path $Raw "$f.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        Write-Host ("   [{0}] {1} {2} v {3} | goals {4}-{5} | ft {6}-{7} | ht {8}-{9} | status {10} ({11}) | id {12}" -f `
            $f, $x.fixture.date.Substring(0,10), $x.teams.home.name, $x.teams.away.name, $x.goals.home, $x.goals.away,
            $x.score.fulltime.home, $x.score.fulltime.away, $x.score.halftime.home, $x.score.halftime.away,
            $x.fixture.status.short, $x.fixture.status.long, $x.fixture.id)
    }
}
Write-Host ""
Write-Host ("total api calls logged this session: {0}" -f (Get-Content $CallLog | Measure-Object -Line).Lines)
