# FORMAX Historical Master - FINAL DATA QUALITY GATE
#  * fill missing scores from targeted api-football pulls (already cached in api_raw/)
#  * resolve the 4 score conflicts with a third independent source
#  * classify every canonical team identity CONFIRMED / PROBABLE / UNRESOLVED
#  * derive ModelEligible + exclusion reason, emit the model dataset
# Master keeps every real historical record; nothing is deleted, nothing is invented.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$sha1 = [Security.Cryptography.SHA1]::Create()
function Match-Id { param([string]$s) 'FMXM' + (([BitConverter]::ToString($sha1.ComputeHash([Text.Encoding]::UTF8.GetBytes($s))) -replace '-','').Substring(0,16)) }
function NInt { param($v) if ($null -eq $v -or "$v" -eq '') { $null } else { [int]$v } }

$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff','club','de','the','afc','ssc','ogc','rc','sco','hsc','fco','sk')
function Norm-Key { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return '' }
    $s = $s -replace '\s*\([A-Z]{3}\)\s*',''
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = (-join $d).ToLowerInvariant() -replace '[^a-z0-9 ]',' '
    (@($s -split '\s+' | Where-Object { $_ -and ($noise -notcontains $_) }) -join ' ')
}

$master = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$teams  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$links  = Import-Csv (Join-Path $Out 'team_links.csv')
$srcMapOld = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_SOURCE_MAP.csv')
Write-Host "loaded master rows: $($master.Count)"

# typed working copy
foreach ($r in $master) {
    foreach ($p in 'HomeGoals','AwayGoals','RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
                   'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway','SourceCount') {
        $r.$p = NInt $r.$p
    }
    foreach ($p in 'OnPitchHomeGoals','OnPitchAwayGoals','ResultResolution','ModelEligible','ModelExclusionReason','IdentityConfidence') {
        Add-Member -InputObject $r -NotePropertyName $p -NotePropertyValue $null -Force
    }
}

# ---------------------------------------------------------------------------------------------
# 1) alias -> canonical id lookup (normalised), used to map api-football names onto the master
# ---------------------------------------------------------------------------------------------
$aliasToId = @{}
foreach ($t in $teams) {
    foreach ($a in ($t.Aliases -split ' \| ')) {
        $k = Norm-Key $a
        if ($k -and -not $aliasToId.ContainsKey($k)) { $aliasToId[$k] = $t.CanonicalTeamId }
    }
    $k = Norm-Key $t.CanonicalTeamName
    if ($k -and -not $aliasToId.ContainsKey($k)) { $aliasToId[$k] = $t.CanonicalTeamId }
}
function Resolve-Id { param([string]$name)
    $k = Norm-Key $name
    if ($aliasToId.ContainsKey($k)) { return $aliasToId[$k] }
    return $null
}

# index master by competition|season|homeId|awayId
$byPair = @{}
foreach ($r in $master) {
    $k = '{0}|{1}|{2}|{3}' -f $r.Competition,$r.Season,$r.HomeTeamId,$r.AwayTeamId
    if (-not $byPair.ContainsKey($k)) { $byPair[$k] = New-Object System.Collections.ArrayList }
    [void]$byPair[$k].Add($r)
}

$statusMap = @{ 'FT'='FT'; 'AET'='AET'; 'PEN'='PEN'; 'AWD'='AWARDED'; 'WO'='AWARDED'; 'CANC'='CANCELLED'
                'PST'='POSTPONED'; 'ABD'='ABANDONED'; 'NS'='SCHEDULED'; 'TBD'='SCHEDULED' }

# ---------------------------------------------------------------------------------------------
# 2) fill missing scores from the targeted api-football pulls
# ---------------------------------------------------------------------------------------------
$fillPlan = @(
    @{ File='fx_203_2025.json';      Competition=('S'+[char]0x00FC+'per Lig'); Season='2025/26' }
    @{ File='fx_61_2025_d.json';     Competition='Ligue 1';                    Season='2025/26' }
    @{ File='fx_88_2025_full.json';  Competition='Eredivisie';                 Season='2025/26' }
)
$missingBefore = @($master | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals })
Write-Host "missing score before: $($missingBefore.Count)"

$filled = New-Object System.Collections.ArrayList
$apiUnmatched = New-Object System.Collections.ArrayList
$idFailures = New-Object System.Collections.ArrayList

foreach ($plan in $fillPlan) {
    $j = Get-Content (Join-Path $Raw $plan.File) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        $hid = Resolve-Id $x.teams.home.name
        $aid = Resolve-Id $x.teams.away.name
        if (-not $hid -or -not $aid) {
            [void]$idFailures.Add([PSCustomObject]@{ File=$plan.File; Date=$x.fixture.date.Substring(0,10)
                                                     Home=$x.teams.home.name; Away=$x.teams.away.name
                                                     HomeResolved=$hid; AwayResolved=$aid })
            continue
        }
        $k = '{0}|{1}|{2}|{3}' -f $plan.Competition,$plan.Season,$hid,$aid
        if (-not $byPair.ContainsKey($k)) { continue }
        $apiDate = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime()
        $target = $null
        foreach ($cand in $byPair[$k]) {
            if ($null -ne $cand.HomeGoals) { continue }                       # only fill real gaps
            $dd = [Math]::Abs(([datetime]::ParseExact($cand.Date,'yyyy-MM-dd',$CI) - $apiDate.Date).TotalDays)
            if ($dd -le 5) { $target = $cand; break }
        }
        if (-not $target) { continue }

        $st = $statusMap["$($x.fixture.status.short)"]; if (-not $st) { $st = 'UNKNOWN' }
        if ($null -eq $x.goals.home) { continue }                             # api has no result either

        $oldDate = $target.Date
        $oldId   = $target.MatchId
        $target.HomeGoals            = [int]$x.goals.home
        $target.AwayGoals            = [int]$x.goals.away
        $target.RegularTimeHomeGoals = NInt $x.score.fulltime.home
        $target.RegularTimeAwayGoals = NInt $x.score.fulltime.away
        $target.HalfTimeHomeGoals    = NInt $x.score.halftime.home
        $target.HalfTimeAwayGoals    = NInt $x.score.halftime.away
        $target.PenaltyShootoutHome  = NInt $x.score.penalty.home
        $target.PenaltyShootoutAway  = NInt $x.score.penalty.away
        # ET contract: goals scored during extra time only
        if ($st -in @('AET','PEN') -and $null -ne $target.RegularTimeHomeGoals) {
            $target.ExtraTimeHomeGoals = $target.HomeGoals - $target.RegularTimeHomeGoals
            $target.ExtraTimeAwayGoals = $target.AwayGoals - $target.RegularTimeAwayGoals
        }
        $target.MatchStatus     = $st
        $target.ProviderMatchId = $x.fixture.id
        $target.ProviderHomeTeamId = $x.teams.home.id
        $target.ProviderAwayTeamId = $x.teams.away.id
        $target.KickoffUtc      = $apiDate.ToString('yyyy-MM-ddTHH:mm:ssZ')
        $target.SourceTimeZone  = 'UTC'
        $target.SourceList      = (@(($target.SourceList -split ';') + 'api-football') | Sort-Object -Unique) -join ';'
        $target.SourceCount     = ($target.SourceList -split ';').Count
        $flags = @($target.DataQualityFlag,'SCORE_FILLED_FROM_API_FOOTBALL') | Where-Object { $_ }
        if ($apiDate.ToString('yyyy-MM-dd') -ne $oldDate) {
            $target.Date = $apiDate.ToString('yyyy-MM-dd')
            $target.MatchId = Match-Id ('{0}|{1}|{2}|{3}|{4}|{5}' -f $target.Competition,$target.Season,$target.CompetitionType,$target.Date,$target.HomeTeamId,$target.AwayTeamId)
            $flags += 'DATE_CORRECTED_FROM_API'
        }
        $target.DataQualityFlag = ($flags | Sort-Object -Unique) -join ';'
        [void]$filled.Add([PSCustomObject]@{ OldMatchId=$oldId; MatchId=$target.MatchId; Competition=$target.Competition
                                             Season=$target.Season; OldDate=$oldDate; Date=$target.Date
                                             HomeTeam=$target.HomeTeam; AwayTeam=$target.AwayTeam
                                             Score="$($target.HomeGoals)-$($target.AwayGoals)"; Status=$st
                                             ProviderMatchId=$x.fixture.id })
    }
}
Write-Host "scores filled from api-football: $($filled.Count)   api names that could not be mapped: $($idFailures.Count)"
$stillMissing = @($master | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals })
Write-Host "missing score after : $($stillMissing.Count)"
$filled       | Export-Csv (Join-Path $Work 'api_filled.csv')     -NoTypeInformation -Encoding UTF8
$idFailures   | Export-Csv (Join-Path $Work 'api_id_failures.csv') -NoTypeInformation -Encoding UTF8
$stillMissing | Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam,MatchStatus |
                Export-Csv (Join-Path $Work 'still_missing.csv')  -NoTypeInformation -Encoding UTF8

# ---------------------------------------------------------------------------------------------
# 3) conflict resolution with api-football as an independent third source
# ---------------------------------------------------------------------------------------------
$conf = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
$confFiles = @{
    'fx_conf_verona_roma.json'   = 'Hellas Verona'
    'fx_conf_union_bochum.json'  = 'Union Berlin'
    'fx_conf_nec_vitesse.json'   = 'NEC Nijmegen'
    'fx_conf_akhisar_bjk.json'   = 'Akhisar'
}
$apiScore = @{}     # "date|homeId|awayId" -> pscustomobject
foreach ($f in $confFiles.Keys) {
    $j = Get-Content (Join-Path $Raw $f) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        $hid = Resolve-Id $x.teams.home.name; $aid = Resolve-Id $x.teams.away.name
        if (-not $hid -or -not $aid) { continue }
        $d = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-dd')
        $apiScore["$d|$hid|$aid"] = [PSCustomObject]@{ H=[int]$x.goals.home; A=[int]$x.goals.away
                                                       Status=$x.fixture.status.short; FixtureId=$x.fixture.id; HtH=(NInt $x.score.halftime.home); HtA=(NInt $x.score.halftime.away) }
    }
}
$confOut = New-Object System.Collections.ArrayList
foreach ($g in ($conf | Group-Object MatchId)) {
    $row = $master | Where-Object { $_.MatchId -eq $g.Name } | Select-Object -First 1
    if (-not $row) { continue }
    $primary = "$($g.Group[0].ScoreA)"
    $others  = @($g.Group | ForEach-Object { $_.ScoreB } | Sort-Object -Unique)
    $api = $apiScore["$($row.Date)|$($row.HomeTeamId)|$($row.AwayTeamId)"]
    $apiTxt = if ($api) { "$($api.H)-$($api.A)" } else { $null }

    # votes: primary source + every secondary + api-football
    $votes = @{}
    foreach ($v in (@($primary) + @($g.Group | ForEach-Object { $_.ScoreB }) + @($apiTxt | Where-Object { $_ }))) {
        if (-not $votes.ContainsKey($v)) { $votes[$v] = 0 }; $votes[$v]++
    }
    $ranked   = $votes.GetEnumerator() | Sort-Object Value -Descending
    $winner   = $ranked[0]
    $isTie    = ($ranked.Count -gt 1 -and $ranked[1].Value -eq $winner.Value)
    $onPitch  = $null; $resolution = $null; $eligible = $true

    if (-not $api) {
        $resolution = 'UNRESOLVED_NO_THIRD_SOURCE'; $eligible = $false
    } elseif ($isTie) {
        $resolution = 'UNRESOLVED_SOURCES_TIED'; $eligible = $false
    } else {
        # winner has 2+ independent sources incl. api-football
        $loser = ($ranked | Where-Object { $_.Key -ne $winner.Key } | Select-Object -First 1).Key
        if ("$($row.HomeGoals)-$($row.AwayGoals)" -ne $winner.Key) {
            $p = $winner.Key -split '-'
            $row.HomeGoals = [int]$p[0]; $row.AwayGoals = [int]$p[1]
            $row.RegularTimeHomeGoals = [int]$p[0]; $row.RegularTimeAwayGoals = [int]$p[1]
            if ($api) { $row.HalfTimeHomeGoals = $api.HtH; $row.HalfTimeAwayGoals = $api.HtA }
            $row.ProviderMatchId = $api.FixtureId
        }
        $onPitch = $loser
        $resolution = 'MAJORITY_INCL_API_FOOTBALL'
        # A disciplinary/abandoned decision means the recorded result is not what happened on the
        # pitch. Such a scoreline is not a football outcome and must not train a goals model.
        if ($api.Status -in @('AWD','WO','ABD')) { $resolution = 'OFFICIAL_AWARDED_DIFFERS_FROM_PITCH'; $eligible = $false }
    }
    $row.OnPitchHomeGoals = if ($onPitch) { ($onPitch -split '-')[0] } else { $null }
    $row.OnPitchAwayGoals = if ($onPitch) { ($onPitch -split '-')[1] } else { $null }
    $row.ResultResolution = $resolution
    if (-not $eligible) { $row.ModelExclusionReason = 'RESULT_CONFLICT' }

    [void]$confOut.Add([PSCustomObject]@{
        MatchKey=('{0}|{1}|{2}|{3}|{4}' -f $row.Competition,$row.Season,$row.Date,$row.HomeTeam,$row.AwayTeam)
        MatchId=$row.MatchId; Competition=$row.Competition; Season=$row.Season; Date=$row.Date
        HomeTeam=$row.HomeTeam; AwayTeam=$row.AwayTeam
        SourceA=$g.Group[0].SourceA; ScoreA=$primary
        SourceB=(($g.Group | ForEach-Object { $_.SourceB }) -join ';'); ScoreB=($others -join ';')
        SourceC='api-football'; ScoreC=$apiTxt; ApiStatus=$(if ($api) { $api.Status } else { 'NOT_AVAILABLE_FROM_API' })
        OfficialFinalResult="$($row.HomeGoals)-$($row.AwayGoals)"
        OnPitchResult=$onPitch
        Resolution=$resolution; ModelExcluded=(-not $eligible) })
}
$confOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "--- conflict resolutions"
$confOut | Format-Table Season,Date,HomeTeam,AwayTeam,ScoreA,ScoreB,ScoreC,OfficialFinalResult,OnPitchResult,Resolution,ModelExcluded -AutoSize

# ---------------------------------------------------------------------------------------------
# 4) team identity confidence
# ---------------------------------------------------------------------------------------------
$hasProvider = @{}
foreach ($r in $master) {
    if ($r.ProviderHomeTeamId -and "$($r.ProviderHomeTeamId)" -ne '') { $hasProvider[$r.HomeTeamId] = $r.ProviderHomeTeamId }
    if ($r.ProviderAwayTeamId -and "$($r.ProviderAwayTeamId)" -ne '') { $hasProvider[$r.AwayTeamId] = $r.ProviderAwayTeamId }
}
$nameToId = @{}
foreach ($t in $teams) { foreach ($a in ($t.Aliases -split ' \| ')) { $nameToId[$a] = $t.CanonicalTeamId } }
$hardLinked = @{}
foreach ($l in $links) {
    if ($l.Method -notin @('fixture-fingerprint','split-spelling-in-primary')) { continue }
    foreach ($n in @($l.PrimaryName,$l.OtherName)) { if ($nameToId.ContainsKey($n)) { $hardLinked[$nameToId[$n]] = $true } }
}
# fuzzy collision scan between canonical names (a split identity would show up here)
$tok = @{}
foreach ($t in $teams) { $tok[$t.CanonicalTeamId] = @((Norm-Key $t.CanonicalTeamName) -split ' ' | Where-Object { $_ }) }
$collision = @{}
$ids = @($teams.CanonicalTeamId)
for ($i=0; $i -lt $ids.Count; $i++) {
    for ($j=$i+1; $j -lt $ids.Count; $j++) {
        $a = $tok[$ids[$i]]; $b = $tok[$ids[$j]]
        if ($a.Count -eq 0 -or $b.Count -eq 0) { continue }
        $inter = @($a | Where-Object { $b -contains $_ }).Count
        if ($inter -eq 0) { continue }
        $jac = $inter / (@($a + $b | Sort-Object -Unique).Count)
        if ($jac -ge 0.5) {
            if (-not $collision.ContainsKey($ids[$i])) { $collision[$ids[$i]] = New-Object System.Collections.ArrayList }
            if (-not $collision.ContainsKey($ids[$j])) { $collision[$ids[$j]] = New-Object System.Collections.ArrayList }
            [void]$collision[$ids[$i]].Add($ids[$j]); [void]$collision[$ids[$j]].Add($ids[$i])
        }
    }
}
$teamOut = New-Object System.Collections.ArrayList
$conf4Team = @{}
foreach ($t in $teams) {
    $id = $t.CanonicalTeamId
    $ev = New-Object System.Collections.ArrayList
    if ($hasProvider.ContainsKey($id)) { [void]$ev.Add("api-football provider team id $($hasProvider[$id])") }
    if ($hardLinked.ContainsKey($id))  { [void]$ev.Add('fixture-fingerprint link across sources') }
    if ([int]$t.AliasCount -eq 1)      { [void]$ev.Add('single spelling, no cross-source ambiguity') }
    $col = if ($collision.ContainsKey($id)) { ($collision[$id] | Sort-Object -Unique) -join ',' } else { '' }

    $c = if ($col) { 'UNRESOLVED' }
         elseif ($hasProvider.ContainsKey($id) -or $hardLinked.ContainsKey($id)) { 'CONFIRMED' }
         elseif ([int]$t.AliasCount -eq 1) { 'CONFIRMED' }
         else { 'PROBABLE' }
    $conf4Team[$id] = $c
    [void]$teamOut.Add([PSCustomObject]@{ CanonicalTeamId=$id; CanonicalTeamName=$t.CanonicalTeamName
                                          IdentityConfidence=$c; AliasCount=$t.AliasCount; Aliases=$t.Aliases
                                          ProviderTeamId=$(if ($hasProvider.ContainsKey($id)) { $hasProvider[$id] } else { '' })
                                          Evidence=($ev -join ' + '); NameCollisionWith=$col })
}
$teamOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "--- team identity confidence"
$teamOut | Group-Object IdentityConfidence | Format-Table Count,Name -AutoSize

# review of the 89 previously unresolved fingerprint links: where did each name end up?
$unres = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_UNRESOLVED.csv')
$review = New-Object System.Collections.ArrayList
foreach ($u in ($unres | Where-Object { $_.Kind -eq 'TEAM_LINK_UNRESOLVED' })) {
    $d = $u.Detail | ConvertFrom-Json
    $id = if ($nameToId.ContainsKey($d.Name)) { $nameToId[$d.Name] } else { $null }
    [void]$review.Add([PSCustomObject]@{
        SourceTeamName=$d.Name; Source=$d.Source; Competition=$d.Competition; Season=$d.Season
        CanonicalTeamId=$id; CanonicalTeamName=$(if ($id) { ($teamOut | Where-Object { $_.CanonicalTeamId -eq $id }).CanonicalTeamName } else { '' })
        IdentityConfidence=$(if ($id) { $conf4Team[$id] } else { 'UNRESOLVED' })
        Evidence=$(if ($id) { ($teamOut | Where-Object { $_.CanonicalTeamId -eq $id }).Evidence } else { 'no canonical id' })
        WhyFingerprintFailed=$d.Note; BestCandidateAtTheTime=$d.BestPrimaryCandidate; Overlap=$d.Overlap; RunnerUp=$d.RunnerUp })
}
$review | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_IDENTITY_REVIEW.csv') -NoTypeInformation -Encoding UTF8
Write-Host "--- 89 unresolved fingerprint links, final identity confidence"
$review | Group-Object IdentityConfidence | Format-Table Count,Name -AutoSize

# ---------------------------------------------------------------------------------------------
# 5) model eligibility
# ---------------------------------------------------------------------------------------------
$playedStatus = @('FT','AET','PEN')
foreach ($r in $master) {
    $reasons = New-Object System.Collections.ArrayList
    if ($r.ModelExclusionReason) { [void]$reasons.Add($r.ModelExclusionReason) }
    if ($null -eq $r.HomeGoals -or $null -eq $r.AwayGoals) { [void]$reasons.Add('MISSING_SCORE') }
    if ([string]::IsNullOrWhiteSpace($r.Date))              { [void]$reasons.Add('MISSING_DATE') }
    if ([string]::IsNullOrWhiteSpace($r.Season) -or [string]::IsNullOrWhiteSpace($r.Competition) -or
        [string]::IsNullOrWhiteSpace($r.CompetitionType))   { [void]$reasons.Add('OUT_OF_SCOPE') }
    if ($r.MatchStatus -notin $playedStatus)                { [void]$reasons.Add('INVALID_STATUS') }
    $hc = $conf4Team[$r.HomeTeamId]; $ac = $conf4Team[$r.AwayTeamId]
    if ($hc -ne 'CONFIRMED' -or $ac -ne 'CONFIRMED')        { [void]$reasons.Add('IDENTITY_UNRESOLVED') }
    $r.IdentityConfidence = if ($hc -eq 'CONFIRMED' -and $ac -eq 'CONFIRMED') { 'CONFIRMED' }
                            elseif ($hc -eq 'UNRESOLVED' -or $ac -eq 'UNRESOLVED') { 'UNRESOLVED' } else { 'PROBABLE' }
    $r.ModelEligible        = ($reasons.Count -eq 0)
    $r.ModelExclusionReason = (@($reasons | Sort-Object -Unique) -join ';')
}
$eligible = @($master | Where-Object { $_.ModelEligible })
Write-Host ""
Write-Host ("model eligible: {0} / {1}" -f $eligible.Count, $master.Count)
$master | Where-Object { -not $_.ModelEligible } | Group-Object ModelExclusionReason | Sort-Object Count -Descending |
    Format-Table Count,Name -AutoSize

# ---------------------------------------------------------------------------------------------
# 6) outputs
# ---------------------------------------------------------------------------------------------
$cols = 'MatchId','Season','Competition','CompetitionType','Round','Date','KickoffLocalTime','KickoffUtc','SourceTimeZone',
        'HomeTeam','AwayTeam','HomeTeamId','AwayTeamId','HomeGoals','AwayGoals',
        'RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
        'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway',
        'OnPitchHomeGoals','OnPitchAwayGoals','ResultResolution','MatchStatus',
        'ProviderMatchId','ProviderHomeTeamId','ProviderAwayTeamId',
        'Source','SourceCount','SourceList','DataQualityFlag','IdentityConfidence','ModelEligible','ModelExclusionReason'

$master = @($master | Sort-Object Date, Competition, HomeTeam)
$master   | Select-Object $cols | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')         -NoTypeInformation -Encoding UTF8
$eligible | Select-Object $cols | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv') -NoTypeInformation -Encoding UTF8

$master | Where-Object { -not $_.ModelEligible } |
    Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam,
                  @{N='Reason';E={$_.ModelExclusionReason}},@{N='Source';E={$_.SourceList}} |
    Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_EXCLUSIONS.csv') -NoTypeInformation -Encoding UTF8

# source map rebuilt (match ids may have changed for date-corrected rows)
$oldFile = @{}; foreach ($s in $srcMapOld) { $oldFile[$s.MatchId] = $s.PrimarySourceFile }
$master | ForEach-Object {
    [PSCustomObject]@{ MatchId=$_.MatchId; Competition=$_.Competition; CompetitionType=$_.CompetitionType; Season=$_.Season
                       Date=$_.Date; HomeTeam=$_.HomeTeam; AwayTeam=$_.AwayTeam
                       PrimarySource=$_.Source; PrimarySourceFile=$(if ($oldFile.ContainsKey($_.MatchId)) { $oldFile[$_.MatchId] } else { '' })
                       SourceCount=$_.SourceCount; SourceList=$_.SourceList; ProviderMatchId=$_.ProviderMatchId
                       ModelEligible=$_.ModelEligible }
} | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_SOURCE_MAP.csv') -NoTypeInformation -Encoding UTF8

# data quality table
$q = New-Object System.Collections.ArrayList
foreach ($g in ($master | Group-Object Competition,CompetitionType,Season)) {
    $rows = $g.Group
    $comp,$ctype,$season = $g.Name -split ', '
    [void]$q.Add([PSCustomObject][ordered]@{
        Competition=$comp; CompetitionType=$ctype; Season=$season
        MatchCount=$rows.Count
        ModelEligibleCount=@($rows | Where-Object { $_.ModelEligible }).Count
        ExcludedCount=@($rows | Where-Object { -not $_.ModelEligible }).Count
        DistinctTeams=@(($rows | ForEach-Object { $_.HomeTeamId; $_.AwayTeamId }) | Sort-Object -Unique).Count
        DuplicateCount=0
        ConflictCount=@($rows | Where-Object { $_.ResultResolution }).Count
        UnresolvedIdentityCount=@($rows | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count
        MissingScoreCount=@($rows | Where-Object { $null -eq $_.HomeGoals }).Count
        MissingDateCount=@($rows | Where-Object { [string]::IsNullOrWhiteSpace($_.Date) }).Count
        MultiSourceVerified=@($rows | Where-Object { [int]$_.SourceCount -gt 1 }).Count
        PrimarySource=$rows[0].Source
        Status=$(if (@($rows | Where-Object { -not $_.ModelEligible }).Count -eq 0) { 'OK' } else { 'HAS_EXCLUSIONS' }) })
}
@($q | Sort-Object Competition, CompetitionType, Season) |
    Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_DATA_QUALITY.csv') -NoTypeInformation -Encoding UTF8

# coverage matrix: total + model eligible
$scopes = @(
 @('Premier League','DOMESTIC_LEAGUE'), @('La Liga','DOMESTIC_LEAGUE'), @('Serie A','DOMESTIC_LEAGUE'),
 @('Bundesliga','DOMESTIC_LEAGUE'), @('Ligue 1','DOMESTIC_LEAGUE'), @(('S'+[char]0x00FC+'per Lig'),'DOMESTIC_LEAGUE'),
 @('Championship','DOMESTIC_LEAGUE'), @('Eredivisie','DOMESTIC_LEAGUE'),
 @('UEFA Champions League','UEFA_MAIN'), @('UEFA Europa League','UEFA_MAIN'), @('UEFA Conference League','UEFA_MAIN'),
 @('UEFA Champions League','UEFA_QUALIFIER'), @('UEFA Champions League','UEFA_QUALIFICATION_PLAYOFF'),
 @('UEFA Europa League','UEFA_QUALIFIER'), @('UEFA Europa League','UEFA_QUALIFICATION_PLAYOFF'),
 @('UEFA Conference League','UEFA_QUALIFIER'), @('UEFA Conference League','UEFA_QUALIFICATION_PLAYOFF'),
 @('Championship','DOMESTIC_PLAYOFF'))
$seasons = 2017..2025 | ForEach-Object { '{0}/{1}' -f $_, (($_+1) % 100).ToString('00') }
$idxT = @{}; $idxE = @{}
foreach ($m in $master) {
    $k = "$($m.Competition)|$($m.CompetitionType)|$($m.Season)"
    if (-not $idxT.ContainsKey($k)) { $idxT[$k]=0; $idxE[$k]=0 }
    $idxT[$k]++; if ($m.ModelEligible) { $idxE[$k]++ }
}
$cov = New-Object System.Collections.ArrayList
$no = 0
foreach ($s in $scopes) {
    $no++
    $row = [ordered]@{ No=$no; Competition=$s[0]; CompetitionType=$s[1] }
    foreach ($se in $seasons) {
        $k = "$($s[0])|$($s[1])|$se"
        $t = if ($idxT.ContainsKey($k)) { $idxT[$k] } else { 0 }
        $e = if ($idxE.ContainsKey($k)) { $idxE[$k] } else { 0 }
        $row[$se] = if ($t -eq 0) { '0' } elseif ($t -eq $e) { "$t" } else { "$t ($e)" }
    }
    [void]$cov.Add([PSCustomObject]$row)
}
$cov | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_COVERAGE_MATRIX.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "coverage matrix  -  'total (model eligible)' when they differ"
$cov | Format-Table -AutoSize

Write-Host ""
Write-Host "===== AFTER ====="
Write-Host ("Total              : {0}" -f $master.Count)
Write-Host ("Missing Score      : {0}" -f @($master | Where-Object { $null -eq $_.HomeGoals }).Count)
Write-Host ("Missing Date       : {0}" -f @($master | Where-Object { [string]::IsNullOrWhiteSpace($_.Date) }).Count)
Write-Host ("Conflict (unresolved / model-excluded) : {0}" -f @($confOut | Where-Object { $_.ModelExcluded }).Count)
Write-Host ("Identity not CONFIRMED (teams)         : {0}" -f @($teamOut | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count)
Write-Host ("Model Eligible     : {0}" -f $eligible.Count)
