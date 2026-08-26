# Look-alike identity resolution - STEP 4: decide every pair, run the wrong-merge safety tests,
# write FORMAX_HISTORICAL_TEAM_IDENTITY_RESOLUTION.csv and apply CONFIRMED_SAME to the master.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'
$stamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssZ')

$master  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$teams   = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$ctx     = Import-Csv (Join-Path $Work 'lookalike_context.csv')
$sideRes = Import-Csv (Join-Path $Work 'lookalike_side_resolved.csv')
$look    = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_LOOKALIKES.csv')

# ---- provider identity catalogue (from every cached /teams pull) --------------------------------
$prov = @{}
foreach ($f in (Get-ChildItem $Raw -Filter 'teams_*.json')) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($t in $j.response) {
        $k = "$($t.team.id)"
        if (-not $prov.ContainsKey($k)) {
            $prov[$k] = [PSCustomObject]@{ Id=$t.team.id; Name=$t.team.name; Country=$t.team.country
                                           Founded=$t.team.founded; Venue=$t.venue.name; City=$t.venue.city }
        }
    }
}
Write-Host "provider identities available: $($prov.Count)"

# ---- provider id per side ------------------------------------------------------------------------
$sideProv = @{}     # "No|Side" -> pscustomobject
foreach ($r in $ctx) {
    foreach ($s in @('A','B')) {
        $p = ($r."Prov$s" -split ',' | Where-Object { $_ } | Select-Object -First 1)
        if ($p) { $sideProv["$($r.No)|$s"] = [PSCustomObject]@{ Id=$p; Method='api-football fixture rows already in the master' } }
    }
}
foreach ($r in $sideRes) {
    if (-not $r.ProviderId) { continue }
    $sideProv["$($r.No)|$($r.Side)"] = [PSCustomObject]@{ Id=$r.ProviderId; Method="squad list of the season: $($r.Method)" }
}
# the two decided by fixture evidence
$sideProv['12|A'] = [PSCustomObject]@{ Id='540'; Method='fixture fingerprint: 38/38 La Liga 2025/26 fixtures identical to provider team 540 (runner-up 0.03)' }
$sideProv['42|A'] = [PSCustomObject]@{ Id='580'; Method='opponent proof: 2025-08-07 master "Vikingur (FRO) v Linfield 2-1" = api-football "Vikingur Gota (580) v Linfield (583) 2-1"' }

# ---- evidence indexes for the safety tests --------------------------------------------------------
$faced   = New-Object 'System.Collections.Generic.HashSet[string]'
$srcCS   = @{}   # canonical id -> set of "source|competition|season"
$cs      = @{}   # canonical id -> set of "competition|season"
foreach ($m in $master) {
    [void]$faced.Add("$($m.HomeTeamId)||$($m.AwayTeamId)"); [void]$faced.Add("$($m.AwayTeamId)||$($m.HomeTeamId)")
    foreach ($id in @($m.HomeTeamId,$m.AwayTeamId)) {
        if (-not $srcCS.ContainsKey($id)) { $srcCS[$id] = New-Object 'System.Collections.Generic.HashSet[string]'; $cs[$id] = New-Object 'System.Collections.Generic.HashSet[string]' }
        [void]$srcCS[$id].Add("$($m.Source)|$($m.Competition)|$($m.Season)")
        [void]$cs[$id].Add("$($m.Competition)|$($m.Season)")
    }
}

# ---- decide -------------------------------------------------------------------------------------
$res = New-Object System.Collections.ArrayList
$mergePairs = New-Object System.Collections.ArrayList
foreach ($r in $ctx) {
    $pa = $sideProv["$($r.No)|A"]; $pb = $sideProv["$($r.No)|B"]
    $da = if ($pa -and $prov.ContainsKey("$($pa.Id)")) { $prov["$($pa.Id)"] } else { $null }
    $db = if ($pb -and $prov.ContainsKey("$($pb.Id)")) { $prov["$($pb.Id)"] } else { $null }

    $decision = 'UNRESOLVED'; $conf = 'LOW'; $ev = New-Object System.Collections.ArrayList
    if (-not $pa -or -not $pb) {
        [void]$ev.Add('provider identity could not be established for ' + $(if (-not $pa) { 'side A' } else { 'side B' }))
    }
    elseif ("$($pa.Id)" -eq "$($pb.Id)") {
        $decision = 'CONFIRMED_SAME'; $conf = 'HIGH'
        [void]$ev.Add("identical api-football team id $($pa.Id)")
        [void]$ev.Add("A: $($pa.Method)"); [void]$ev.Add("B: $($pb.Method)")
        # --- wrong-merge safety tests
        if ($faced.Contains("$($r.IdA)||$($r.IdB)")) { $decision = 'UNRESOLVED'; $conf='LOW'; [void]$ev.Add('SAFETY FAIL: the two identities played against each other') }
        $shared = @($srcCS[$r.IdA] | Where-Object { $srcCS[$r.IdB].Contains($_) })
        if ($shared.Count -gt 0) { $decision = 'UNRESOLVED'; $conf='LOW'; [void]$ev.Add("SAFETY FAIL: one source lists both spellings inside $($shared[0])") }
        if ($da -and $db -and $da.Country -ne $db.Country) { $decision='UNRESOLVED'; $conf='LOW'; [void]$ev.Add("SAFETY FAIL: country mismatch $($da.Country) vs $($db.Country)") }
        if ($decision -eq 'CONFIRMED_SAME') {
            [void]$ev.Add('safety tests passed: never met on the pitch, no single source uses both spellings in one season, same country')
            [void]$mergePairs.Add([PSCustomObject]@{ IdA=$r.IdA; IdB=$r.IdB; ProviderId=$pa.Id })
        }
    }
    else {
        $decision = 'CONFIRMED_DIFFERENT'; $conf = 'HIGH'
        [void]$ev.Add("different api-football team ids $($pa.Id) vs $($pb.Id)")
        if ($da -and $db) { [void]$ev.Add("$($da.Name) [$($da.Country), founded $($da.Founded), $($da.Venue)] vs $($db.Name) [$($db.Country), founded $($db.Founded), $($db.Venue)]") }
    }

    [void]$res.Add([PSCustomObject]@{
        LookalikeId       = 'LA{0:d3}' -f [int]$r.No
        SourceNameA       = $r.NameA;  SourceNameB = $r.NameB
        CanonicalTeamA    = $r.IdA;    CanonicalTeamB = $r.IdB
        ProviderTeamIdA   = $(if ($pa) { $pa.Id } else { '' })
        ProviderTeamIdB   = $(if ($pb) { $pb.Id } else { '' })
        ProviderNameA     = $(if ($da) { $da.Name } else { '' })
        ProviderNameB     = $(if ($db) { $db.Name } else { '' })
        CountryA          = $(if ($da) { $da.Country } else { '' })
        CountryB          = $(if ($db) { $db.Country } else { '' })
        FoundedA          = $(if ($da) { $da.Founded } else { '' })
        FoundedB          = $(if ($db) { $db.Founded } else { '' })
        VenueA            = $(if ($da) { $da.Venue } else { '' })
        VenueB            = $(if ($db) { $db.Venue } else { '' })
        Decision          = $decision
        Evidence          = ($ev -join ' | ')
        Confidence        = $conf
        APICheckedAt      = $stamp })
}
# the 21 pairs that were already proven different keep their verdict, recorded for completeness
foreach ($p in ($look | Where-Object { $_.Status -notlike 'NEEDS*' })) {
    [void]$res.Add([PSCustomObject]@{
        LookalikeId='LA-PRE'; SourceNameA=$p.NameA; SourceNameB=$p.NameB
        CanonicalTeamA=$p.IdA; CanonicalTeamB=$p.IdB
        ProviderTeamIdA=''; ProviderTeamIdB=''; ProviderNameA=''; ProviderNameB=''
        CountryA=''; CountryB=''; FoundedA=''; FoundedB=''; VenueA=''; VenueB=''
        Decision='CONFIRMED_DIFFERENT'; Evidence=("proven from the historical data itself: " + $p.Evidence)
        Confidence='HIGH'; APICheckedAt=$stamp })
}
$res | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_IDENTITY_RESOLUTION.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
$res | Group-Object Decision | Format-Table Count,Name -AutoSize
Write-Host ("merges to apply: {0}" -f $mergePairs.Count)

# ---- apply CONFIRMED_SAME to the master ------------------------------------------------------------
$parent = @{}; foreach ($t in $teams) { $parent[$t.CanonicalTeamId] = $t.CanonicalTeamId }
function Find { param($x) $r=$x; while ($parent[$r] -ne $r) { $r=$parent[$r] }; while ($parent[$x] -ne $r) { $n=$parent[$x]; $parent[$x]=$r; $x=$n }; $r }
$byId = @{}; foreach ($t in $teams) { $byId[$t.CanonicalTeamId] = $t }
$cnt = @{}; foreach ($m in $master) { foreach ($id in @($m.HomeTeamId,$m.AwayTeamId)) { if (-not $cnt.ContainsKey($id)) { $cnt[$id]=0 }; $cnt[$id]++ } }
foreach ($p in $mergePairs) {
    $ra = Find $p.IdA; $rb = Find $p.IdB
    if ($ra -eq $rb) { continue }
    # keep the identity with more matches (richer history), absorb the other
    if ($cnt[$ra] -ge $cnt[$rb]) { $parent[$rb] = $ra } else { $parent[$ra] = $rb }
}
$remap = @{}; foreach ($t in $teams) { $remap[$t.CanonicalTeamId] = Find $t.CanonicalTeamId }
$groups = @{}
foreach ($t in $teams) {
    $r = $remap[$t.CanonicalTeamId]
    if (-not $groups.ContainsKey($r)) { $groups[$r] = New-Object System.Collections.ArrayList }
    [void]$groups[$r].Add($t)
}
$provOfPair = @{}; foreach ($p in $mergePairs) { $provOfPair[(Find $p.IdA)] = $p.ProviderId }
$teamNew = @{}
foreach ($r in $groups.Keys) {
    $members = @($groups[$r])
    $aliases = @($members | ForEach-Object { $_.Aliases -split ' \| ' } | Sort-Object -Unique)
    $pid3 = ($members | ForEach-Object { $_.ProviderTeamId } | Where-Object { $_ } | Select-Object -First 1)
    if (-not $pid3 -and $provOfPair.ContainsKey($r)) { $pid3 = $provOfPair[$r] }
    # canonical display name: the provider's own name when we know the provider identity
    $name = $null
    if ($pid3 -and $prov.ContainsKey("$pid3")) { $name = $prov["$pid3"].Name }
    if (-not $name) { $name = ($members | Sort-Object @{E={ -($_.CanonicalTeamName.Length) }} | Select-Object -First 1).CanonicalTeamName }
    $name = ([regex]::Replace($name, '\s*\([A-Za-z]{3}\)\s*', ' ')).Trim()
    $teamNew[$r] = [PSCustomObject]@{ CanonicalTeamId=$r; CanonicalTeamName=$name
                                      IdentityConfidence='CONFIRMED'; AliasCount=$aliases.Count
                                      ProviderTeamId=$(if ($pid3) { $pid3 } else { '' })
                                      Evidence=(($members | ForEach-Object { $_.Evidence } | Where-Object { $_ } | Sort-Object -Unique) -join ' + ')
                                      Aliases=($aliases -join ' | ') }
}
$before = $master.Count
foreach ($m in $master) {
    $m.HomeTeamId = $remap[$m.HomeTeamId]; $m.AwayTeamId = $remap[$m.AwayTeamId]
    $m.HomeTeam   = $teamNew[$m.HomeTeamId].CanonicalTeamName
    $m.AwayTeam   = $teamNew[$m.AwayTeamId].CanonicalTeamName
}
Write-Host ("canonical teams: {0} -> {1}   master rows: {2} -> {3}" -f $teams.Count, $teamNew.Count, $before, $master.Count)

# a merge must never create a duplicate match
$dup = @($master | Group-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Competition,$_.Season,$_.Date,$_.HomeTeamId,$_.AwayTeamId } | Where-Object { $_.Count -gt 1 })
Write-Host ("duplicate canonical keys after merge: {0}" -f $dup.Count)
$dup | ForEach-Object { Write-Host "   DUP $($_.Name)" }
# a merge must never make a team play itself
$self = @($master | Where-Object { $_.HomeTeamId -eq $_.AwayTeamId })
Write-Host ("self-matches after merge: {0}" -f $self.Count)
$self | Select-Object -First 5 | Format-Table Date,Competition,HomeTeam,AwayTeam -AutoSize

$master  | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv') -NoTypeInformation -Encoding UTF8
$teamNew.Values | Sort-Object CanonicalTeamId | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv') -NoTypeInformation -Encoding UTF8
Write-Host "DONE_R6"
