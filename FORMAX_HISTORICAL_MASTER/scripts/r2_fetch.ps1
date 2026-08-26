# Look-alike identity resolution - STEP 1: fetch provider identity for the sides that lack one.
# Only /teams calls, one per (league, season) that is actually needed, cached on disk.
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Out  = Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'
$CallLog = Join-Path $Out '_api_call_log.txt'

$cfg = Get-Content (Join-Path $Root 'Formax.API\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ApiKey = $cfg.ApiFootball.ApiKey; $BaseUrl = $cfg.ApiFootball.BaseUrl
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
    Start-Sleep -Milliseconds 400
    return $resp
}

$leagueId = @{ 'Premier League'=39; 'Championship'=40; 'La Liga'=140; 'Serie A'=135; 'Bundesliga'=78
               'Ligue 1'=61; ('S'+[char]0x00FC+'per Lig')=203; 'Eredivisie'=88
               'UEFA Champions League'=2; 'UEFA Europa League'=3; 'UEFA Conference League'=848 }

$ctxRows = Import-Csv (Join-Path $Work 'lookalike_context.csv')

# --- plan the minimal set of (league, season) calls -------------------------------------------
$needed = @{}
$sideNeed = New-Object System.Collections.ArrayList
foreach ($r in $ctxRows) {
    foreach ($side in @('A','B')) {
        $prov = $r."Prov$side"
        if ($prov) { continue }
        $csList = @(($r."CompSeason$side") -split ' ; ' | Where-Object { $_ })
        # prefer the most recent season - api-football team lists are most complete there
        $pick = ($csList | Sort-Object { ($_ -split '\|')[1] } -Descending | Select-Object -First 1)
        $comp,$season = $pick -split '\|'
        $lid = $leagueId[$comp]
        if (-not $lid) { Write-Host "  !! no league id for '$comp'"; continue }
        $yr = [int]($season -split '/')[0]
        $key = "$lid|$yr"
        $needed[$key] = 1
        [void]$sideNeed.Add([PSCustomObject]@{ No=$r.No; Side=$side; Id=$r."Id$side"; Name=$r."Name$side"
                                               Aliases=$r."Aliases$side"; Competition=$comp; Season=$season
                                               LeagueId=$lid; SeasonYear=$yr; AllCompSeasons=$r."CompSeason$side" })
    }
}
Write-Host ("sides needing a provider id: {0}" -f $sideNeed.Count)
Write-Host ("distinct /teams calls planned: {0}" -f $needed.Count)
$needed.Keys | Sort-Object | ForEach-Object { Write-Host "   teams?league=$(($_ -split '\|')[0])&season=$(($_ -split '\|')[1])" }
$sideNeed | Export-Csv (Join-Path $Work 'lookalike_side_need.csv') -NoTypeInformation -Encoding UTF8

foreach ($k in ($needed.Keys | Sort-Object)) {
    $lid,$yr = $k -split '\|'
    Invoke-AF -Path 'teams' -Query @{ league=$lid; season=$yr } -SaveAs "teams_${lid}_${yr}.json" | Out-Null
}
Write-Host ""
Write-Host ("total api calls logged so far: {0}" -f (Get-Content $CallLog | Measure-Object -Line).Lines)
