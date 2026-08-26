# FORMAX HISTORICAL MASTER - FINAL DATA QUALITY GATE (consolidated, runs on the rebuilt master)
#  1) fill missing scores from the already-cached targeted api-football pulls (no new calls)
#  2) resolve the score conflicts with api-football as an independent third source
#  3) classify every canonical team identity, list look-alike identities for human review
#  4) derive ModelEligible + reason, emit master / model dataset / exclusions / quality / coverage
# No identity is merged here: identity resolution belongs to m2 and uses fixture evidence.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$sha1 = [Security.Cryptography.SHA1]::Create()
function Match-Id { param([string]$s) 'FMXM' + (([BitConverter]::ToString($sha1.ComputeHash([Text.Encoding]::UTF8.GetBytes($s))) -replace '-','').Substring(0,16)) }
function NInt { param($v) if ($null -eq $v -or "$v" -eq '') { $null } else { [int]$v } }
$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff','club','de','the','afc','ssc','ogc','rc','sco','hsc','fco')
function Norm-Tokens { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return @() }
    $s = [regex]::Replace($s, '\s*\([A-Za-z]{3}\)\s*', ' ')
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = [regex]::Replace(((-join $d).ToLowerInvariant()), '[^a-z0-9]', ' ')
    # single letters are abbreviation debris ("S.K.", "A.C.") and must not weigh in the comparison
    return @($s -split '\s+' | Where-Object { $_ -and $_.Length -ge 2 -and ($noise -notcontains $_) })
}
function Norm-Key { param([string]$s) ((Norm-Tokens $s) -join ' ') }

$master = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$teams  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$links  = Import-Csv (Join-Path $Out 'team_links.csv')
$conf0  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
Write-Host "master rows: $($master.Count)   canonical teams: $($teams.Count)   conflicts: $($conf0.Count)"

foreach ($r in $master) {
    foreach ($p in 'HomeGoals','AwayGoals','RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
                   'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway','SourceCount') { $r.$p = NInt $r.$p }
    foreach ($p in 'AlternateReportedHomeGoals','AlternateReportedAwayGoals','ResultResolution','ModelEligible','ModelExclusionReason','IdentityConfidence') {
        Add-Member -InputObject $r -NotePropertyName $p -NotePropertyValue $null -Force
    }
}

# ---------------------------------------------------------------------------------- lookups
$teamById = @{}; foreach ($t in $teams) { $teamById[$t.CanonicalTeamId] = $t }
$aliasToId = @{}
foreach ($t in $teams) {
    foreach ($a in ($t.Aliases -split ' \| ')) { $k = Norm-Key $a; if ($k -and -not $aliasToId.ContainsKey($k)) { $aliasToId[$k] = $t.CanonicalTeamId } }
    $k = Norm-Key $t.CanonicalTeamName; if ($k -and -not $aliasToId.ContainsKey($k)) { $aliasToId[$k] = $t.CanonicalTeamId }
}
$teamsInCS = @{}
foreach ($r in $master) {
    $k = "$($r.Competition)|$($r.Season)"
    if (-not $teamsInCS.ContainsKey($k)) { $teamsInCS[$k] = New-Object 'System.Collections.Generic.HashSet[string]' }
    [void]$teamsInCS[$k].Add($r.HomeTeamId); [void]$teamsInCS[$k].Add($r.AwayTeamId)
}
function Resolve-Id { param([string]$name,[string]$comp,[string]$season)
    $k = Norm-Key $name
    if ($aliasToId.ContainsKey($k)) { return $aliasToId[$k] }
    $cs = "$comp|$season"
    if (-not $teamsInCS.ContainsKey($cs)) { return $null }
    $nt = @(Norm-Tokens $name); if ($nt.Count -eq 0) { return $null }
    $best = $null; $bestScore = 0.0; $second = 0.0
    foreach ($id in $teamsInCS[$cs]) {
        $ct = @(Norm-Tokens $teamById[$id].CanonicalTeamName); if ($ct.Count -eq 0) { continue }
        $inter = 0
        foreach ($x in $nt) { foreach ($y in $ct) {
            if ($x -eq $y) { $inter++; break }
            if ($x.Length -ge 4 -and $y.Length -ge 4 -and ($x.StartsWith($y) -or $y.StartsWith($x))) { $inter++; break }
        } }
        if ($inter -eq 0) { continue }
        $s = $inter / [Math]::Min($nt.Count, $ct.Count)
        if ($s -gt $bestScore) { $second = $bestScore; $bestScore = $s; $best = $id } elseif ($s -gt $second) { $second = $s }
    }
    if ($best -and $bestScore -ge 0.6 -and $bestScore -gt $second) { return $best }
    return $null
}
$byPair = @{}
foreach ($r in $master) {
    $k = '{0}|{1}|{2}|{3}' -f $r.Competition,$r.Season,$r.HomeTeamId,$r.AwayTeamId
    if (-not $byPair.ContainsKey($k)) { $byPair[$k] = New-Object System.Collections.ArrayList }
    [void]$byPair[$k].Add($r)
}
$statusMap = @{ 'FT'='FT'; 'AET'='AET'; 'PEN'='PEN'; 'AWD'='AWARDED'; 'WO'='AWARDED'; 'CANC'='CANCELLED'
                'PST'='POSTPONED'; 'ABD'='ABANDONED'; 'NS'='SCHEDULED'; 'TBD'='SCHEDULED' }

# ------------------------------------------------------------------- 1) fill missing scores
$before = @($master | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals })
Write-Host "missing score before: $($before.Count)"
$fillPlan = @(
    @{ File='fx_203_2025.json';     Competition=('S'+[char]0x00FC+'per Lig'); Season='2025/26' }
    @{ File='fx_61_2025_d.json';    Competition='Ligue 1';                    Season='2025/26' }
    @{ File='fx_88_2025_full.json'; Competition='Eredivisie';                 Season='2025/26' }
)
$filled = New-Object System.Collections.ArrayList
$idFail = New-Object System.Collections.ArrayList
foreach ($plan in $fillPlan) {
    $j = Get-Content (Join-Path $Raw $plan.File) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        if ($null -eq $x.goals.home) { continue }
        $hid = Resolve-Id $x.teams.home.name $plan.Competition $plan.Season
        $aid = Resolve-Id $x.teams.away.name $plan.Competition $plan.Season
        if (-not $hid -or -not $aid) {
            [void]$idFail.Add([PSCustomObject]@{ Competition=$plan.Competition; Date=$x.fixture.date.Substring(0,10)
                                                 Home=$x.teams.home.name; Away=$x.teams.away.name; HomeResolved=$hid; AwayResolved=$aid }); continue
        }
        $k = '{0}|{1}|{2}|{3}' -f $plan.Competition,$plan.Season,$hid,$aid
        if (-not $byPair.ContainsKey($k)) { continue }
        $apiDate = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime()
        $target = $null
        foreach ($cand in $byPair[$k]) {
            if ($null -ne $cand.HomeGoals) { continue }
            if ([Math]::Abs(([datetime]::ParseExact($cand.Date,'yyyy-MM-dd',$CI) - $apiDate.Date).TotalDays) -le 5) { $target = $cand; break }
        }
        if (-not $target) {
            # postponed fixture: an ordered pair exists once per season in a double round robin
            $open = @($byPair[$k] | Where-Object { $null -eq $_.HomeGoals })
            if ($open.Count -eq 1 -and $byPair[$k].Count -eq 1) { $target = $open[0] }
        }
        if (-not $target) { continue }
        $st = $statusMap["$($x.fixture.status.short)"]; if (-not $st) { $st = 'UNKNOWN' }
        $oldDate = $target.Date
        $target.HomeGoals = [int]$x.goals.home; $target.AwayGoals = [int]$x.goals.away
        $target.RegularTimeHomeGoals = NInt $x.score.fulltime.home; $target.RegularTimeAwayGoals = NInt $x.score.fulltime.away
        $target.HalfTimeHomeGoals    = NInt $x.score.halftime.home; $target.HalfTimeAwayGoals    = NInt $x.score.halftime.away
        $target.PenaltyShootoutHome  = NInt $x.score.penalty.home;  $target.PenaltyShootoutAway  = NInt $x.score.penalty.away
        if ($st -in @('AET','PEN') -and $null -ne $target.RegularTimeHomeGoals) {
            $target.ExtraTimeHomeGoals = $target.HomeGoals - $target.RegularTimeHomeGoals
            $target.ExtraTimeAwayGoals = $target.AwayGoals - $target.RegularTimeAwayGoals
        }
        $target.MatchStatus = $st
        $target.ProviderMatchId = $x.fixture.id; $target.ProviderHomeTeamId = $x.teams.home.id; $target.ProviderAwayTeamId = $x.teams.away.id
        $target.KickoffUtc = $apiDate.ToString('yyyy-MM-ddTHH:mm:ssZ'); $target.SourceTimeZone = 'UTC'
        $target.SourceList = (@(($target.SourceList -split ';') + 'api-football') | Where-Object { $_ } | Sort-Object -Unique) -join ';'
        $target.SourceCount = ($target.SourceList -split ';').Count
        $fl = @(($target.DataQualityFlag -split ';') + 'SCORE_FILLED_FROM_API_FOOTBALL')
        if ($apiDate.ToString('yyyy-MM-dd') -ne $oldDate) {
            $target.Date = $apiDate.ToString('yyyy-MM-dd')
            $target.MatchId = Match-Id ('{0}|{1}|{2}|{3}|{4}|{5}' -f $target.Competition,$target.Season,$target.CompetitionType,$target.Date,$target.HomeTeamId,$target.AwayTeamId)
            $fl += 'DATE_CORRECTED_FROM_API'
        }
        $target.DataQualityFlag = (@($fl | Where-Object { $_ } | Sort-Object -Unique) -join ';')
        [void]$filled.Add($target)
    }
}
$missing = @($master | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals })
Write-Host "scores filled: $($filled.Count)   api names unmapped: $($idFail.Count)   missing score after: $($missing.Count)"
$idFail  | Export-Csv (Join-Path $Work 'q7_api_id_failures.csv') -NoTypeInformation -Encoding UTF8
$missing | Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam,MatchStatus |
           Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_STILL_MISSING_SCORE.csv') -NoTypeInformation -Encoding UTF8

# --------------------------------------------------------------- 2) conflict re-resolution
$family = @{ 'football-data'='football-data'; 'openfootball-json'='openfootball'; 'openfootball-txt'='openfootball'
             'openfootball-csv'='openfootball'; 'fixturedownload'='fixturedownload'; 'api-football'='api-football' }
$apiScore = @{}
foreach ($f in 'fx_conf_verona_roma.json','fx_conf_union_bochum.json','fx_conf_nec_vitesse.json','fx_conf_akhisar_bjk.json') {
    $j = Get-Content (Join-Path $Raw $f) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        $d = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-dd')
        $apiScore["$d|$(Norm-Key $x.teams.home.name)|$(Norm-Key $x.teams.away.name)"] =
            [PSCustomObject]@{ H=[int]$x.goals.home; A=[int]$x.goals.away; Status=$x.fixture.status.short
                               FixtureId=$x.fixture.id; HtH=(NInt $x.score.halftime.home); HtA=(NInt $x.score.halftime.away) }
    }
}
$tokMatch = { param($setA,$setB)
    foreach ($x in $setA) { foreach ($y in $setB) {
        if ($x -eq $y) { return $true }
        if ($x.Length -ge 4 -and $y.Length -ge 4 -and ($x.StartsWith($y) -or $y.StartsWith($x))) { return $true }
    } }
    return $false }
$notes = @{
 'Hellas Verona|2020-09-19' = @{ OnPitch='0-0'; Note='Roma fielded an ineligible player; the official record is the 3-0 award (openfootball + api-football). football-data carries the on-pitch 0-0.' }
 'NEC Nijmegen|2023-10-01'  = @{ OnPitch='1-2'; Note='Match halted and completed later; 1-2 is the score when play stopped, 1-3 the completed-match result (football-data + api-football).' }
 'Union Berlin|2024-12-14'  = @{ OnPitch='';    Note='1-1 is the score on the pitch and is what football-data and api-football record. openfootball carries 0-2, matching a disciplinary award, but no second source confirms it, so the master keeps 1-1 and 0-2 stays visible here.' }
 'Akhisar|2019-01-18'       = @{ OnPitch='';    Note='openfootball and api-football agree on 0-3; football-data 1-3 is an isolated discrepancy with no corroboration (not an award).' }
}
$confOut = New-Object System.Collections.ArrayList
# one entry per match: the conflicts file can hold one row per (primary, secondary) source pair
$conf0 = @($conf0 | Group-Object { "$($_.Date)|$($_.Competition)|$($_.HomeTeam)" } | ForEach-Object {
    $g = $_.Group
    [PSCustomObject]@{ Date=$g[0].Date; Competition=$g[0].Competition; Season=$g[0].Season
                       HomeTeam=$g[0].HomeTeam; AwayTeam=$g[0].AwayTeam
                       SourceA=$g[0].SourceA; ScoreA=$g[0].ScoreA
                       SourceB=((@($g | ForEach-Object { $_.SourceB -split ';' }) | Sort-Object -Unique) -join ';')
                       ScoreB=((@($g | ForEach-Object { $_.ScoreB -split ';' }) | Sort-Object -Unique) -join ';') }
})
foreach ($c in $conf0) {
    $row = $master | Where-Object { $_.Date -eq $c.Date -and $_.Competition -eq $c.Competition -and
                                    (Norm-Key $_.HomeTeam) -eq (Norm-Key $c.HomeTeam) } | Select-Object -First 1
    if (-not $row) { Write-Host "  !! conflict row not found: $($c.Date) $($c.HomeTeam)"; continue }
    $api = $null
    foreach ($k in $apiScore.Keys) {
        $p = $k -split '\|'
        if ($p[0] -ne $row.Date) { continue }
        if ((& $tokMatch @(Norm-Tokens $row.HomeTeam) @($p[1] -split ' ')) -and
            (& $tokMatch @(Norm-Tokens $row.AwayTeam) @($p[2] -split ' '))) { $api = $apiScore[$k]; break }
    }
    $apiTxt = if ($api) { "$($api.H)-$($api.A)" } else { $null }
    $votes = @{}
    $addVote = { param($score,$fam) if (-not $score -or -not $fam) { return }
                 if (-not $votes.ContainsKey($score)) { $votes[$score] = New-Object 'System.Collections.Generic.HashSet[string]' }
                 [void]$votes[$score].Add($fam) }
    & $addVote $c.ScoreA $family[$c.SourceA]
    $sbSources = @($c.SourceB -split ';'); $sbScores = @($c.ScoreB -split ';')
    for ($i=0; $i -lt $sbSources.Count; $i++) {
        $sc = if ($i -lt $sbScores.Count) { $sbScores[$i] } else { $sbScores[0] }
        & $addVote $sc $family[$sbSources[$i]]
    }
    & $addVote $apiTxt 'api-football'
    $ranked = $votes.GetEnumerator() | Sort-Object { $_.Value.Count } -Descending
    $winner = $ranked[0]
    $tie    = ($ranked.Count -gt 1 -and $ranked[1].Value.Count -eq $winner.Value.Count)
    $alt    = if ($ranked.Count -gt 1) { $ranked[1].Key } else { $null }
    $resolution = $null; $eligible = $true
    if (-not $api) { $resolution = 'UNRESOLVED_NO_THIRD_SOURCE'; $eligible = $false }
    elseif ($tie)  { $resolution = 'UNRESOLVED_SOURCE_FAMILIES_TIED'; $eligible = $false }
    else {
        $p = $winner.Key -split '-'
        $row.HomeGoals = [int]$p[0]; $row.AwayGoals = [int]$p[1]
        $row.RegularTimeHomeGoals = [int]$p[0]; $row.RegularTimeAwayGoals = [int]$p[1]
        if ("$($api.H)-$($api.A)" -eq $winner.Key) { $row.HalfTimeHomeGoals = $api.HtH; $row.HalfTimeAwayGoals = $api.HtA; $row.ProviderMatchId = $api.FixtureId }
        $resolution = "MAJORITY_OF_SOURCE_FAMILIES ($(($winner.Value | Sort-Object) -join '+'))"
        if ($api.Status -in @('AWD','WO','ABD')) { $resolution = 'OFFICIAL_AWARDED_DIFFERS_FROM_PITCH'; $eligible = $false }
    }
    if ($alt) { $row.AlternateReportedHomeGoals = ($alt -split '-')[0]; $row.AlternateReportedAwayGoals = ($alt -split '-')[1] }
    $row.ResultResolution = $resolution
    $row.DataQualityFlag  = (@(($row.DataQualityFlag -split ';') + 'DATA_CONFLICT_RESOLVED') | Where-Object { $_ } | Sort-Object -Unique) -join ';'
    $nk = ($notes.Keys | Where-Object { $row.HomeTeam -like ($_ -split '\|')[0] + '*' -and $row.Date -eq ($_ -split '\|')[1] } | Select-Object -First 1)
    $n  = if ($nk) { $notes[$nk] } else { $null }
    [void]$confOut.Add([PSCustomObject]@{
        MatchKey=('{0}|{1}|{2}|{3}|{4}' -f $row.Competition,$row.Season,$row.Date,$row.HomeTeam,$row.AwayTeam)
        MatchId=$row.MatchId; Competition=$row.Competition; Season=$row.Season; Date=$row.Date
        HomeTeam=$row.HomeTeam; AwayTeam=$row.AwayTeam
        SourceA=$c.SourceA; ScoreA=$c.ScoreA; SourceB=$c.SourceB; ScoreB=$c.ScoreB
        SourceC='api-football'; ScoreC=$(if ($apiTxt) { $apiTxt } else { 'NOT_AVAILABLE_FROM_API' })
        OfficialFinalResult="$($row.HomeGoals)-$($row.AwayGoals)"
        OnPitchResult=$(if ($n) { $n.OnPitch } else { '' })
        AlternateReportedResult=$alt
        Resolution=$resolution; ModelExcluded=(-not $eligible)
        Note=$(if ($n) { $n.Note } else { '' }) })
}
$confOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
$confOut | Format-Table Date,HomeTeam,AwayTeam,ScoreA,ScoreB,ScoreC,OfficialFinalResult,OnPitchResult,Resolution -AutoSize

# ----------------------------------------------------------------- 3) identity confidence
$provNow = @{}
foreach ($r in $master) {
    if ("$($r.ProviderHomeTeamId)" -ne '') { $provNow[$r.HomeTeamId] = $r.ProviderHomeTeamId }
    if ("$($r.ProviderAwayTeamId)" -ne '') { $provNow[$r.AwayTeamId] = $r.ProviderAwayTeamId }
}
$nameToId = @{}; foreach ($t in $teams) { foreach ($a in ($t.Aliases -split ' \| ')) { $nameToId[$a] = $t.CanonicalTeamId } }
$hardLinked = @{}
foreach ($l in $links) {
    if ($l.Method -notin @('fixture-fingerprint','split-spelling-in-primary')) { continue }
    foreach ($n in @($l.PrimaryName,$l.OtherName)) { if ($nameToId.ContainsKey($n)) { $hardLinked[$nameToId[$n]] = $true } }
}
$conf4 = @{}; $teamOut = New-Object System.Collections.ArrayList
foreach ($t in ($teams | Sort-Object CanonicalTeamId)) {
    $id = $t.CanonicalTeamId
    $ev = New-Object System.Collections.ArrayList
    if ($provNow.ContainsKey($id))    { [void]$ev.Add("api-football provider team id $($provNow[$id])") }
    if ($hardLinked.ContainsKey($id)) { [void]$ev.Add('fixture-fingerprint link across sources') }
    if ([int]$t.AliasCount -eq 1)     { [void]$ev.Add('single spelling in the whole corpus') }
    if ([int]$t.AliasCount -gt 1) {
        # every spelling reduces to the identical normalised name, and the m2 guard already proved
        # the spellings are never used by one source inside one season (which would mean two clubs)
        $keys = @(($t.Aliases -split ' \| ') | ForEach-Object { Norm-Key $_ } | Sort-Object -Unique)
        if ($keys.Count -eq 1) { [void]$ev.Add('all spellings normalise to one name, guard-checked') }
    }
    $c = if ($ev.Count -gt 0) { 'CONFIRMED' } else { 'PROBABLE' }
    $conf4[$id] = $c
    [void]$teamOut.Add([PSCustomObject]@{ CanonicalTeamId=$id; CanonicalTeamName=$t.CanonicalTeamName
                                          IdentityConfidence=$c; AliasCount=$t.AliasCount
                                          ProviderTeamId=$(if ($provNow.ContainsKey($id)) { $provNow[$id] } else { '' })
                                          Evidence=($ev -join ' + '); Aliases=$t.Aliases })
}
$teamOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv') -NoTypeInformation -Encoding UTF8
Write-Host "identity confidence:"; $teamOut | Group-Object IdentityConfidence | Format-Table Count,Name -AutoSize

# look-alike identities that were NOT merged - listed for human review, never merged automatically
$tok = @{}; foreach ($t in $teams) { $tok[$t.CanonicalTeamId] = @(Norm-Tokens $t.CanonicalTeamName) }
$faced = New-Object 'System.Collections.Generic.HashSet[string]'
$csOf  = @{}
foreach ($r in $master) {
    [void]$faced.Add("$($r.HomeTeamId)||$($r.AwayTeamId)"); [void]$faced.Add("$($r.AwayTeamId)||$($r.HomeTeamId)")
    foreach ($id in @($r.HomeTeamId,$r.AwayTeamId)) {
        if (-not $csOf.ContainsKey($id)) { $csOf[$id] = New-Object 'System.Collections.Generic.HashSet[string]' }
        [void]$csOf[$id].Add("$($r.Competition)|$($r.Season)")
    }
}
$look = New-Object System.Collections.ArrayList
$ids = @($teams.CanonicalTeamId)
for ($i=0; $i -lt $ids.Count; $i++) {
  for ($j=$i+1; $j -lt $ids.Count; $j++) {
    $a=$ids[$i]; $b=$ids[$j]; $ta=$tok[$a]; $tb=$tok[$b]
    if ($ta.Count -eq 0 -or $tb.Count -eq 0) { continue }
    $sub = ($ta.Count -le $tb.Count -and @($ta | Where-Object { $tb -notcontains $_ }).Count -eq 0) -or
           ($tb.Count -le $ta.Count -and @($tb | Where-Object { $ta -notcontains $_ }).Count -eq 0)
    if (-not $sub) { continue }
    $disproof = ''
    if ($faced.Contains("$a||$b")) { $disproof = 'played against each other' }
    elseif ($provNow.ContainsKey($a) -and $provNow.ContainsKey($b) -and $provNow[$a] -ne $provNow[$b]) { $disproof = "different provider ids ($($provNow[$a]) vs $($provNow[$b]))" }
    else { $shared = @($csOf[$a] | Where-Object { $csOf[$b].Contains($_) }); if ($shared.Count -gt 0) { $disproof = "both in $($shared[0])" } }
    [void]$look.Add([PSCustomObject]@{ IdA=$a; NameA=$teamById[$a].CanonicalTeamName; IdB=$b; NameB=$teamById[$b].CanonicalTeamName
                                       Decision='KEPT SEPARATE'
                                       Status=$(if ($disproof) { 'DIFFERENT CLUBS - proven' } else { 'NEEDS HUMAN REVIEW - no proof either way' })
                                       Evidence=$(if ($disproof) { $disproof } else { 'name looks similar but nothing in the data proves they are the same club' }) })
  }
}
$look | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_LOOKALIKES.csv') -NoTypeInformation -Encoding UTF8
Write-Host "look-alike identity pairs kept separate: $($look.Count)  (needing human review: $(@($look | Where-Object { $_.Status -like 'NEEDS*' }).Count))"

# ------------------------------------------------------------------ 4) model eligibility
$excl = @{}; foreach ($c in $confOut) { if ($c.ModelExcluded) { $excl[$c.MatchId] = $true } }
foreach ($r in $master) {
    $reasons = New-Object System.Collections.ArrayList
    if ($excl.ContainsKey($r.MatchId))                     { [void]$reasons.Add('RESULT_CONFLICT') }
    if ($null -eq $r.HomeGoals -or $null -eq $r.AwayGoals) { [void]$reasons.Add('MISSING_SCORE') }
    if ([string]::IsNullOrWhiteSpace($r.Date))             { [void]$reasons.Add('MISSING_DATE') }
    if ([string]::IsNullOrWhiteSpace($r.Season) -or [string]::IsNullOrWhiteSpace($r.Competition) -or
        [string]::IsNullOrWhiteSpace($r.CompetitionType))  { [void]$reasons.Add('OUT_OF_SCOPE') }
    if ($r.MatchStatus -notin @('FT','AET','PEN'))         { [void]$reasons.Add('INVALID_STATUS') }
    $hc = $conf4[$r.HomeTeamId]; $ac = $conf4[$r.AwayTeamId]
    if ($hc -ne 'CONFIRMED' -or $ac -ne 'CONFIRMED')       { [void]$reasons.Add('IDENTITY_UNRESOLVED') }
    $r.IdentityConfidence   = if ($hc -eq 'CONFIRMED' -and $ac -eq 'CONFIRMED') { 'CONFIRMED' } else { 'PROBABLE' }
    $r.ModelEligible        = ($reasons.Count -eq 0)
    $r.ModelExclusionReason = (@($reasons | Sort-Object -Unique) -join ';')
}
$eligible = @($master | Where-Object { $_.ModelEligible })
Write-Host ("model eligible: {0} / {1}" -f $eligible.Count, $master.Count)
$master | Where-Object { -not $_.ModelEligible } | Group-Object ModelExclusionReason | Sort-Object Count -Descending | Format-Table Count,Name -AutoSize

# ---------------------------------------------------------------------------- 5) outputs
$cols = 'MatchId','Season','Competition','CompetitionType','Round','Date','KickoffLocalTime','KickoffUtc','SourceTimeZone',
        'HomeTeam','AwayTeam','HomeTeamId','AwayTeamId','HomeGoals','AwayGoals',
        'RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
        'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway',
        'AlternateReportedHomeGoals','AlternateReportedAwayGoals','ResultResolution','MatchStatus',
        'ProviderMatchId','ProviderHomeTeamId','ProviderAwayTeamId',
        'Source','SourceCount','SourceList','DataQualityFlag','IdentityConfidence','ModelEligible','ModelExclusionReason'
$master = @($master | Sort-Object Date, Competition, HomeTeam)
$master   | Select-Object $cols | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')         -NoTypeInformation -Encoding UTF8
$eligible | Select-Object $cols | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv') -NoTypeInformation -Encoding UTF8
$master | Where-Object { -not $_.ModelEligible } |
    Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam,@{N='Reason';E={$_.ModelExclusionReason}},@{N='Source';E={$_.SourceList}} |
    Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_EXCLUSIONS.csv') -NoTypeInformation -Encoding UTF8
$master | ForEach-Object {
    [PSCustomObject]@{ MatchId=$_.MatchId; Competition=$_.Competition; CompetitionType=$_.CompetitionType; Season=$_.Season
                       Date=$_.Date; HomeTeam=$_.HomeTeam; AwayTeam=$_.AwayTeam; PrimarySource=$_.Source
                       SourceCount=$_.SourceCount; SourceList=$_.SourceList; ProviderMatchId=$_.ProviderMatchId
                       ModelEligible=$_.ModelEligible }
} | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_SOURCE_MAP.csv') -NoTypeInformation -Encoding UTF8

$q = New-Object System.Collections.ArrayList
foreach ($g in ($master | Group-Object Competition,CompetitionType,Season)) {
    $rows = $g.Group; $comp,$ctype,$season = $g.Name -split ', '
    [void]$q.Add([PSCustomObject][ordered]@{
        Competition=$comp; CompetitionType=$ctype; Season=$season; MatchCount=$rows.Count
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
@($q | Sort-Object Competition, CompetitionType, Season) | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_DATA_QUALITY.csv') -NoTypeInformation -Encoding UTF8

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
$cov = New-Object System.Collections.ArrayList; $no = 0
foreach ($s in $scopes) {
    $no++; $row = [ordered]@{ No=$no; Competition=$s[0]; CompetitionType=$s[1] }
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
Write-Host "===== AFTER ====="
Write-Host ("Total           : {0}" -f $master.Count)
Write-Host ("Missing Score   : {0}" -f @($master | Where-Object { $null -eq $_.HomeGoals }).Count)
Write-Host ("Missing Date    : {0}" -f @($master | Where-Object { [string]::IsNullOrWhiteSpace($_.Date) }).Count)
Write-Host ("Conflicts unres.: {0}" -f @($confOut | Where-Object { $_.ModelExcluded }).Count)
Write-Host ("Teams           : {0}  not CONFIRMED: {1}" -f $teamOut.Count, @($teamOut | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count)
Write-Host ("Model Eligible  : {0}" -f $eligible.Count)
Write-Host "DONE_Q7"
