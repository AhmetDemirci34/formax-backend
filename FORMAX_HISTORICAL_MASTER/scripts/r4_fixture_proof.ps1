# Look-alike resolution - STEP 3: the two sides that name matching cannot separate are resolved
# by FIXTURE EVIDENCE (identical match dates and scores), which is independent of spelling.
#   #12 A "RCD Espanyol de Barcelona"  -> both "Espanyol" and "Barcelona" contain a matching token
#   #42 A "Vikingur"                   -> both "Vikingur Reykjavik" and "Vikingur Gota" match
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Out  = Join-Path $Root 'FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'
$CallLog = Join-Path $Out '_api_call_log.txt'
$cfg = Get-Content (Join-Path $Root 'Formax.API\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json

function Invoke-AF {
    param([string]$Path,[hashtable]$Query,[string]$SaveAs)
    $qs = ($Query.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '&'
    $cache = Join-Path $Raw $SaveAs
    if (Test-Path $cache) { Write-Host "CACHED  $Path`?$qs"; return (Get-Content $cache -Raw -Encoding UTF8 | ConvertFrom-Json) }
    $resp = Invoke-RestMethod -Uri "$($cfg.ApiFootball.BaseUrl)/$Path`?$qs" -Headers @{ 'x-apisports-key'=$cfg.ApiFootball.ApiKey } -Method Get -TimeoutSec 60
    $resp | ConvertTo-Json -Depth 30 | Out-File $cache -Encoding utf8
    Add-Content $CallLog "$(Get-Date -Format s)`t$Path`?$qs`tresults=$($resp.results)"
    Write-Host "CALLED  $Path`?$qs -> results=$($resp.results)"
    Start-Sleep -Milliseconds 400
    return $resp
}

$master = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$ctx    = Import-Csv (Join-Path $Work 'lookalike_context.csv')
$resolved = Import-Csv (Join-Path $Work 'lookalike_side_resolved.csv')

# identity detail for the two provider ids not covered by the cached squad lists
foreach ($pid2 in @('2248','853')) { Invoke-AF -Path 'teams' -Query @{ id=$pid2 } -SaveAs "team_$pid2.json" | Out-Null }

$targets = @(
  @{ No='12'; Side='A'; League=140; Season=2025; File='fx_140_2025.json' }
  @{ No='42'; Side='A'; League=848; Season=2025; File='fx_848_2025.json' }
)
$outRows = New-Object System.Collections.ArrayList
foreach ($t in $targets) {
    $j = Invoke-AF -Path 'fixtures' -Query @{ league=$t.League; season=$t.Season } -SaveAs $t.File

    $row = $ctx | Where-Object { $_.No -eq $t.No } | Select-Object -First 1
    $cid = $row."Id$($t.Side)"; $cname = $row."Name$($t.Side)"
    $comp,$season = (($row."CompSeason$($t.Side)" -split ' ; ') | Sort-Object { ($_ -split '\|')[1] } -Descending | Select-Object -First 1) -split '\|'

    # fingerprint of the canonical identity, from the master
    $mine = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($m in $master) {
        if ($m.Season -ne $season -or $m.Competition -ne $comp) { continue }
        if ($m.HomeTeamId -eq $cid) { [void]$mine.Add("H|$($m.Date)|$($m.HomeGoals)|$($m.AwayGoals)") }
        elseif ($m.AwayTeamId -eq $cid) { [void]$mine.Add("A|$($m.Date)|$($m.AwayGoals)|$($m.HomeGoals)") }
    }
    Write-Host ("`n#{0}{1} '{2}' -> {3} {4}: {5} master fixtures" -f $t.No,$t.Side,$cname,$comp,$season,$mine.Count)

    # fingerprint of every provider team in the same league-season
    $prov = @{}
    foreach ($x in $j.response) {
        if ($null -eq $x.goals.home) { continue }
        $d = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-dd')
        foreach ($side in @(@($x.teams.home.id,$x.teams.home.name,'H',$x.goals.home,$x.goals.away),
                            @($x.teams.away.id,$x.teams.away.name,'A',$x.goals.away,$x.goals.home))) {
            $k = "$($side[0])"
            if (-not $prov.ContainsKey($k)) { $prov[$k] = [PSCustomObject]@{ Id=$side[0]; Name=$side[1]; Set=(New-Object 'System.Collections.Generic.HashSet[string]') } }
            [void]$prov[$k].Set.Add("$($side[2])|$d|$($side[3])|$($side[4])")
        }
    }
    $best = $null; $bestScore = 0.0; $second = 0.0; $secondName = ''
    foreach ($k in $prov.Keys) {
        $inter = 0
        foreach ($x in $mine) { if ($prov[$k].Set.Contains($x)) { $inter++ } }
        if ($inter -eq 0) { continue }
        $sc = $inter / [Math]::Min($mine.Count, $prov[$k].Set.Count)
        if ($sc -gt $bestScore) { $second = $bestScore; $secondName = $(if ($best) { $best.Name } else { '' }); $bestScore = $sc; $best = $prov[$k] }
        elseif ($sc -gt $second) { $second = $sc; $secondName = $prov[$k].Name }
    }
    if ($best) {
        Write-Host ("   best: {0} (id {1}) overlap {2:N2}   runner-up: {3} {4:N2}" -f $best.Name,$best.Id,$bestScore,$secondName,$second)
    } else { Write-Host "   no fixture overlap found" }

    $accept = ($best -and $bestScore -ge 0.8 -and ($second -eq 0 -or $bestScore -ge 2*$second))
    [void]$outRows.Add([PSCustomObject]@{ No=$t.No; Side=$t.Side; CanonicalId=$cid; CanonicalName=$cname
                                          ProviderId=$(if ($accept) { $best.Id } else { '' })
                                          ProviderName=$(if ($accept) { $best.Name } else { '' })
                                          Overlap=[Math]::Round($bestScore,3); RunnerUp=[Math]::Round($second,3)
                                          RunnerUpName=$secondName
                                          Method=$(if ($accept) { 'fixture fingerprint (dates + scores)' } else { 'UNRESOLVED - fixture evidence not decisive' }) })
}
$outRows | Export-Csv (Join-Path $Work 'lookalike_fixture_proof.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
$outRows | Format-Table -AutoSize
Write-Host ("total api calls logged: {0}" -f (Get-Content $CallLog | Measure-Object -Line).Lines)
