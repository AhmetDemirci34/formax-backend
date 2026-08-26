# FORMAX gate - fix pass
#  1) Turkish-I trap: PowerShell -replace is case-insensitive and under tr-TR the class [A-Z]
#     does NOT match strings containing 'I' ("(ITA)" survived, "(GRE)" did not). Every regex here
#     uses [regex]::Replace (culture invariant, case sensitive).
#  2) re-merge canonical team identities that the trap left split, with hard disproof guards
#  3) resolve api-football names by token-subset inside the same competition+season
#  4) re-resolve the score conflicts using SOURCE FAMILIES (openfootball json+txt = one project)
#  5) recompute identity confidence, model eligibility and every derived report
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$sha1 = [Security.Cryptography.SHA1]::Create()
function Match-Id { param([string]$s) 'FMXM' + (([BitConverter]::ToString($sha1.ComputeHash([Text.Encoding]::UTF8.GetBytes($s))) -replace '-','').Substring(0,16)) }
function NInt { param($v) if ($null -eq $v -or "$v" -eq '') { $null } else { [int]$v } }

$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff',
           'club','de','the','afc','ssc','ogc','rc','sco','hsc','fco','sp','spor','kulubu','1899','1900','1901','1904','1905','1909','1893','1848','1846','04','05','07','08','09','1913','1919','1921','1927','1899')
function Norm-Tokens { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return @() }
    $s = [regex]::Replace($s, '\s*\([A-Za-z]{3}\)\s*', ' ')          # country suffix, culture-safe
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = [regex]::Replace(((-join $d).ToLower($CI)), '[^a-z0-9]', ' ')
    return @($s -split '\s+' | Where-Object { $_ -and $_.Length -gt 1 -and ($noise -notcontains $_) })
}
function Norm-Key { param([string]$s) ((Norm-Tokens $s) -join ' ') }

$master = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$teams  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$conf   = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
foreach ($r in $master) {
    foreach ($p in 'HomeGoals','AwayGoals','RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
                   'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway','SourceCount') { $r.$p = NInt $r.$p }
}
Write-Host "master rows: $($master.Count)"

# =============================================================================================
# 2) re-merge split canonical identities
# =============================================================================================
$provOf   = @{}     # canonical id -> set of api-football provider team ids
$compSeas = @{}     # canonical id -> set of "competition|season"
$faced    = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($r in $master) {
    foreach ($side in @(@($r.HomeTeamId,$r.ProviderHomeTeamId), @($r.AwayTeamId,$r.ProviderAwayTeamId))) {
        $id = $side[0]; $ptid = $side[1]
        if ($ptid -and "$ptid" -ne '') {
            if (-not $provOf.ContainsKey($id)) { $provOf[$id] = New-Object 'System.Collections.Generic.HashSet[string]' }
            [void]$provOf[$id].Add("$ptid")
        }
        if (-not $compSeas.ContainsKey($id)) { $compSeas[$id] = New-Object 'System.Collections.Generic.HashSet[string]' }
        [void]$compSeas[$id].Add("$($r.Competition)|$($r.Season)")
    }
    [void]$faced.Add("$($r.HomeTeamId)||$($r.AwayTeamId)")
    [void]$faced.Add("$($r.AwayTeamId)||$($r.HomeTeamId)")
}

$tok = @{}; foreach ($t in $teams) { $tok[$t.CanonicalTeamId] = @(Norm-Tokens $t.CanonicalTeamName) }
$parent = @{}; foreach ($t in $teams) { $parent[$t.CanonicalTeamId] = $t.CanonicalTeamId }
function Find { param($x) $r = $x; while ($parent[$r] -ne $r) { $r = $parent[$r] }; while ($parent[$x] -ne $r) { $n = $parent[$x]; $parent[$x] = $r; $x = $n }; $r }

$mergeLog  = New-Object System.Collections.ArrayList
$blockLog  = New-Object System.Collections.ArrayList
$ids = @($teams.CanonicalTeamId)
for ($i=0; $i -lt $ids.Count; $i++) {
  for ($j=$i+1; $j -lt $ids.Count; $j++) {
    $a = $ids[$i]; $b = $ids[$j]
    $ta = $tok[$a]; $tb = $tok[$b]
    if ($ta.Count -eq 0 -or $tb.Count -eq 0) { continue }
    $ka = ($ta -join ' '); $kb = ($tb -join ' ')
    $subset = ($ta.Count -le $tb.Count -and @($ta | Where-Object { $tb -notcontains $_ }).Count -eq 0) -or
              ($tb.Count -le $ta.Count -and @($tb | Where-Object { $ta -notcontains $_ }).Count -eq 0)
    if (-not ($ka -eq $kb -or $subset)) { continue }
    if ((Find $a) -eq (Find $b)) { continue }

    # --- hard disproofs: these two are definitely different clubs
    $why = $null
    if ($faced.Contains("$a||$b")) { $why = 'the two identities played against each other in a real fixture' }
    elseif ($provOf.ContainsKey($a) -and $provOf.ContainsKey($b)) {
        $inter = @($provOf[$a] | Where-Object { $provOf[$b].Contains($_) }).Count
        if ($inter -eq 0) { $why = "different api-football provider team ids ($(($provOf[$a]) -join ',') vs $(($provOf[$b]) -join ','))" }
    }
    if (-not $why) {
        $shared = @($compSeas[$a] | Where-Object { $compSeas[$b].Contains($_) })
        if ($shared.Count -gt 0) { $why = "both take part in the same competition-season ($($shared[0])) - one club cannot appear twice" }
    }
    if ($why) {
        [void]$blockLog.Add([PSCustomObject]@{ IdA=$a; NameA=($teams|Where-Object{$_.CanonicalTeamId -eq $a}).CanonicalTeamName
                                               IdB=$b; NameB=($teams|Where-Object{$_.CanonicalTeamId -eq $b}).CanonicalTeamName
                                               Decision='KEPT SEPARATE'; Evidence=$why })
        continue
    }
    $ra = Find $a; $rb = Find $b
    $parent[$rb] = $ra
    [void]$mergeLog.Add([PSCustomObject]@{ KeptId=$ra; MergedId=$rb
                                           NameA=($teams|Where-Object{$_.CanonicalTeamId -eq $a}).CanonicalTeamName
                                           NameB=($teams|Where-Object{$_.CanonicalTeamId -eq $b}).CanonicalTeamName
                                           Rule=$(if ($ka -eq $kb) { 'identical normalised name' } else { 'token subset' })
                                           Checked='no head-to-head fixture, no conflicting provider id, never in the same competition-season' })
  }
}
Write-Host "identity re-merges: $($mergeLog.Count)   kept separate on hard evidence: $($blockLog.Count)"
$mergeLog | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_MERGES.csv') -NoTypeInformation -Encoding UTF8
$blockLog | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAM_KEPT_SEPARATE.csv') -NoTypeInformation -Encoding UTF8

# rebuild team registry
$groups = @{}
foreach ($t in $teams) {
    $r = Find $t.CanonicalTeamId
    if (-not $groups.ContainsKey($r)) { $groups[$r] = New-Object System.Collections.ArrayList }
    [void]$groups[$r].Add($t)
}
$idRemap = @{}; $teamNew = @{}
foreach ($r in $groups.Keys) {
    $members = @($groups[$r])
    $aliases = @($members | ForEach-Object { $_.Aliases -split ' \| ' } | Sort-Object -Unique)
    # canonical display name: prefer one backed by a provider id, then the longest clean spelling
    $name = ($members | Sort-Object @{E={ if ($provOf.ContainsKey($_.CanonicalTeamId)) {0} else {1} }}, @{E={ -($_.CanonicalTeamName.Length) }} |
             Select-Object -First 1).CanonicalTeamName
    $name = [regex]::Replace($name, '\s*\([A-Za-z]{3}\)\s*', ' ').Trim()
    foreach ($m in $members) { $idRemap[$m.CanonicalTeamId] = $r }
    $teamNew[$r] = [PSCustomObject]@{ CanonicalTeamId=$r; CanonicalTeamName=$name; AliasCount=$aliases.Count
                                      Aliases=($aliases -join ' | ')
                                      ProviderTeamId=$(if ($provOf.ContainsKey($r)) { ($provOf[$r] | Sort-Object) -join ',' } else { '' }) }
}
foreach ($r in $master) {
    $r.HomeTeamId = $idRemap[$r.HomeTeamId]; $r.AwayTeamId = $idRemap[$r.AwayTeamId]
    $r.HomeTeam   = $teamNew[$r.HomeTeamId].CanonicalTeamName
    $r.AwayTeam   = $teamNew[$r.AwayTeamId].CanonicalTeamName
}
Write-Host "canonical teams after re-merge: $($teamNew.Count)"

# =============================================================================================
# 3) fill the remaining missing scores - resolve api names inside the competition+season
# =============================================================================================
$aliasToId = @{}
foreach ($t in $teamNew.Values) {
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
    $nt = @(Norm-Tokens $name)
    if ($nt.Count -eq 0) { return $null }
    $best = $null; $bestScore = 0.0; $second = 0.0
    foreach ($id in $teamsInCS[$cs]) {
        $ct = @(Norm-Tokens $teamNew[$id].CanonicalTeamName)
        if ($ct.Count -eq 0) { continue }
        $inter = @($nt | Where-Object { $ct -contains $_ }).Count
        if ($inter -eq 0) { continue }
        $s = $inter / [Math]::Max($nt.Count, $ct.Count)
        if ($s -gt $bestScore) { $second = $bestScore; $bestScore = $s; $best = $id } elseif ($s -gt $second) { $second = $s }
    }
    if ($best -and $bestScore -ge 0.5 -and $bestScore -gt $second) { return $best }
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
        $hid = Resolve-Id $x.teams.home.name $plan.Competition $plan.Season
        $aid = Resolve-Id $x.teams.away.name $plan.Competition $plan.Season
        if (-not $hid -or -not $aid) {
            [void]$idFail.Add([PSCustomObject]@{ Competition=$plan.Competition; Season=$plan.Season
                                                 Date=$x.fixture.date.Substring(0,10); Home=$x.teams.home.name; Away=$x.teams.away.name
                                                 HomeResolved=$hid; AwayResolved=$aid }); continue
        }
        $k = '{0}|{1}|{2}|{3}' -f $plan.Competition,$plan.Season,$hid,$aid
        if (-not $byPair.ContainsKey($k)) { continue }
        if ($null -eq $x.goals.home) { continue }
        $apiDate = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime()
        $target = $null
        foreach ($cand in $byPair[$k]) {
            if ($null -ne $cand.HomeGoals) { continue }
            if ([Math]::Abs(([datetime]::ParseExact($cand.Date,'yyyy-MM-dd',$CI) - $apiDate.Date).TotalDays) -le 5) { $target = $cand; break }
        }
        if (-not $target) {
            # postponed fixture: in a double round-robin an ordered pair exists exactly once per
            # season, so a single score-less row for this exact pair IS this fixture, replayed later.
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
        $target.SourceList = (@(($target.SourceList -split ';') + 'api-football') | Sort-Object -Unique) -join ';'
        $target.SourceCount = ($target.SourceList -split ';').Count
        $fl = @($target.DataQualityFlag,'SCORE_FILLED_FROM_API_FOOTBALL') | Where-Object { $_ }
        if ($apiDate.ToString('yyyy-MM-dd') -ne $oldDate) {
            $target.Date = $apiDate.ToString('yyyy-MM-dd')
            $target.MatchId = Match-Id ('{0}|{1}|{2}|{3}|{4}|{5}' -f $target.Competition,$target.Season,$target.CompetitionType,$target.Date,$target.HomeTeamId,$target.AwayTeamId)
            $fl += 'DATE_CORRECTED_FROM_API'
        }
        $target.DataQualityFlag = (@($fl | ForEach-Object { $_ -split ';' } | Where-Object { $_ } | Sort-Object -Unique) -join ';')
        [void]$filled.Add($target)
    }
}
Write-Host "extra scores filled in this pass: $($filled.Count)   api names still unmapped: $($idFail.Count)"
$idFail | Export-Csv (Join-Path $Work 'api_id_failures2.csv') -NoTypeInformation -Encoding UTF8
$missing = @($master | Where-Object { $null -eq $_.HomeGoals -or $null -eq $_.AwayGoals })
Write-Host "missing score now: $($missing.Count)"

# =============================================================================================
# 4) conflict re-resolution by SOURCE FAMILY
# =============================================================================================
$family = @{ 'football-data'='football-data'; 'openfootball-json'='openfootball'; 'openfootball-txt'='openfootball'
             'openfootball-csv'='openfootball'; 'fixturedownload'='fixturedownload'; 'api-football'='api-football' }
$confFiles = 'fx_conf_verona_roma.json','fx_conf_union_bochum.json','fx_conf_nec_vitesse.json','fx_conf_akhisar_bjk.json'
$apiScore = @{}
foreach ($f in $confFiles) {
    $j = Get-Content (Join-Path $Raw $f) -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($x in $j.response) {
        $d = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-dd')
        $apiScore["$d|$(Norm-Key $x.teams.home.name)|$(Norm-Key $x.teams.away.name)"] =
            [PSCustomObject]@{ H=[int]$x.goals.home; A=[int]$x.goals.away; Status=$x.fixture.status.short
                               FixtureId=$x.fixture.id; HtH=(NInt $x.score.halftime.home); HtA=(NInt $x.score.halftime.away) }
    }
}
$confOut = New-Object System.Collections.ArrayList
foreach ($c in $conf) {
    $row = $master | Where-Object { $_.MatchId -eq $c.MatchId } | Select-Object -First 1
    if (-not $row) { $row = $master | Where-Object { $_.Date -eq $c.Date -and $_.HomeTeam -eq $c.HomeTeam } | Select-Object -First 1 }
    if (-not $row) { Write-Host "  !! conflict row not found: $($c.MatchId)"; continue }

    $api = $null
    foreach ($k in $apiScore.Keys) {
        $p = $k -split '\|'
        if ($p[0] -ne $row.Date) { continue }
        $hTok = @(Norm-Tokens $row.HomeTeam); $aTok = @(Norm-Tokens $row.AwayTeam)
        # token match, or one token being a >=4 char prefix of the other
        # ("Akhisar Belediyespor" in the master vs "Akhisarspor" at api-football)
        $tokMatch = { param($setA,$setB)
            foreach ($x in $setA) { foreach ($y in $setB) {
                if ($x -eq $y) { return $true }
                if ($x.Length -ge 4 -and $y.Length -ge 4 -and ($x.StartsWith($y) -or $y.StartsWith($x))) { return $true }
            } }
            return $false }
        $hOk = & $tokMatch $hTok @($p[1] -split ' ')
        $aOk = & $tokMatch $aTok @($p[2] -split ' ')
        if ($hOk -and $aOk) { $api = $apiScore[$k]; break }
    }
    $apiTxt = if ($api) { "$($api.H)-$($api.A)" } else { $null }

    # one vote per independent source FAMILY
    $votes = @{}
    $add = { param($score,$fam) if (-not $score) { return }
             if (-not $votes.ContainsKey($score)) { $votes[$score] = New-Object 'System.Collections.Generic.HashSet[string]' }
             [void]$votes[$score].Add($fam) }
    & $add $c.ScoreA $family[$c.SourceA]
    foreach ($sb in @($c.SourceB -split ';')) { }
    $sbSources = @($c.SourceB -split ';'); $sbScores = @($c.ScoreB -split ';')
    for ($i=0; $i -lt $sbSources.Count; $i++) {
        $sc = if ($i -lt $sbScores.Count) { $sbScores[$i] } else { $sbScores[0] }
        & $add $sc $family[$sbSources[$i]]
    }
    & $add $apiTxt 'api-football'

    $ranked = $votes.GetEnumerator() | Sort-Object { $_.Value.Count } -Descending
    $winner = $ranked[0]
    $tie    = ($ranked.Count -gt 1 -and $ranked[1].Value.Count -eq $winner.Value.Count)
    $loser  = if ($ranked.Count -gt 1) { $ranked[1].Key } else { $null }

    $resolution = $null; $eligible = $true; $onPitch = $null
    if (-not $api)      { $resolution = 'UNRESOLVED_NO_THIRD_SOURCE'; $eligible = $false }
    elseif ($tie)       { $resolution = 'UNRESOLVED_SOURCE_FAMILIES_TIED'; $eligible = $false }
    else {
        $p = $winner.Key -split '-'
        $row.HomeGoals = [int]$p[0]; $row.AwayGoals = [int]$p[1]
        $row.RegularTimeHomeGoals = [int]$p[0]; $row.RegularTimeAwayGoals = [int]$p[1]
        if ($api -and "$($api.H)-$($api.A)" -eq $winner.Key) {
            $row.HalfTimeHomeGoals = $api.HtH; $row.HalfTimeAwayGoals = $api.HtA
            $row.ProviderMatchId = $api.FixtureId
        }
        $onPitch = $loser
        $resolution = "MAJORITY_OF_SOURCE_FAMILIES ($(($winner.Value) -join '+'))"
        if ($api.Status -in @('AWD','WO','ABD')) { $resolution = 'OFFICIAL_AWARDED_DIFFERS_FROM_PITCH'; $eligible = $false }
    }
    $row.OnPitchHomeGoals = $(if ($onPitch) { ($onPitch -split '-')[0] } else { '' })
    $row.OnPitchAwayGoals = $(if ($onPitch) { ($onPitch -split '-')[1] } else { '' })
    $row.ResultResolution = $resolution
    [void]$confOut.Add([PSCustomObject]@{
        MatchKey=('{0}|{1}|{2}|{3}|{4}' -f $row.Competition,$row.Season,$row.Date,$row.HomeTeam,$row.AwayTeam)
        MatchId=$row.MatchId; Competition=$row.Competition; Season=$row.Season; Date=$row.Date
        HomeTeam=$row.HomeTeam; AwayTeam=$row.AwayTeam
        SourceA=$c.SourceA; ScoreA=$c.ScoreA; SourceB=$c.SourceB; ScoreB=$c.ScoreB
        SourceC='api-football'; ScoreC=$(if ($apiTxt) { $apiTxt } else { 'NOT_AVAILABLE_FROM_API' })
        ApiStatus=$(if ($api) { $api.Status } else { '' })
        OfficialFinalResult="$($row.HomeGoals)-$($row.AwayGoals)"; OnPitchResult=$onPitch
        Resolution=$resolution; ModelExcluded=(-not $eligible) })
}
$confOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
$confOut | Format-Table Date,HomeTeam,AwayTeam,ScoreA,ScoreB,ScoreC,OfficialFinalResult,OnPitchResult,Resolution,ModelExcluded -AutoSize

# =============================================================================================
# 5) identity confidence + model eligibility + outputs
# =============================================================================================
$links = Import-Csv (Join-Path $Out 'team_links.csv')
$nameToId = @{}
foreach ($t in $teamNew.Values) { foreach ($a in ($t.Aliases -split ' \| ')) { $nameToId[$a] = $t.CanonicalTeamId } }
$hardLinked = @{}
foreach ($l in $links) {
    if ($l.Method -notin @('fixture-fingerprint','split-spelling-in-primary')) { continue }
    foreach ($n in @($l.PrimaryName,$l.OtherName)) { if ($nameToId.ContainsKey($n)) { $hardLinked[$nameToId[$n]] = $true } }
}
# after the re-merge every remaining same-name pair was disproved by hard evidence, so an identity
# is ambiguous only if it is neither provider-backed, nor fingerprint-linked, nor uniquely spelled
$provNow = @{}
foreach ($r in $master) {
    if ($r.ProviderHomeTeamId -and "$($r.ProviderHomeTeamId)" -ne '') { $provNow[$r.HomeTeamId] = $r.ProviderHomeTeamId }
    if ($r.ProviderAwayTeamId -and "$($r.ProviderAwayTeamId)" -ne '') { $provNow[$r.AwayTeamId] = $r.ProviderAwayTeamId }
}
$conf4 = @{}; $teamOut = New-Object System.Collections.ArrayList
foreach ($t in ($teamNew.Values | Sort-Object CanonicalTeamId)) {
    $id = $t.CanonicalTeamId
    $ev = New-Object System.Collections.ArrayList
    if ($provNow.ContainsKey($id))    { [void]$ev.Add("api-football provider team id $($provNow[$id])") }
    if ($hardLinked.ContainsKey($id)) { [void]$ev.Add('fixture-fingerprint link across sources') }
    if ([int]$t.AliasCount -eq 1)     { [void]$ev.Add('single spelling in the whole corpus') }
    $c = if ($ev.Count -gt 0) { 'CONFIRMED' } else { 'PROBABLE' }
    $conf4[$id] = $c
    [void]$teamOut.Add([PSCustomObject]@{ CanonicalTeamId=$id; CanonicalTeamName=$t.CanonicalTeamName
                                          IdentityConfidence=$c; AliasCount=$t.AliasCount
                                          ProviderTeamId=$t.ProviderTeamId; Evidence=($ev -join ' + '); Aliases=$t.Aliases })
}
$teamOut | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "team identity confidence:"
$teamOut | Group-Object IdentityConfidence | Format-Table Count,Name -AutoSize

$excludedByConflict = @{}
foreach ($c in $confOut) { if ($c.ModelExcluded) { $excludedByConflict[$c.MatchId] = $true } }
$playedStatus = @('FT','AET','PEN')
foreach ($r in $master) {
    $reasons = New-Object System.Collections.ArrayList
    if ($excludedByConflict.ContainsKey($r.MatchId))          { [void]$reasons.Add('RESULT_CONFLICT') }
    if ($null -eq $r.HomeGoals -or $null -eq $r.AwayGoals)    { [void]$reasons.Add('MISSING_SCORE') }
    if ([string]::IsNullOrWhiteSpace($r.Date))                { [void]$reasons.Add('MISSING_DATE') }
    if ([string]::IsNullOrWhiteSpace($r.Season) -or [string]::IsNullOrWhiteSpace($r.Competition) -or
        [string]::IsNullOrWhiteSpace($r.CompetitionType))     { [void]$reasons.Add('OUT_OF_SCOPE') }
    if ($r.MatchStatus -notin $playedStatus)                  { [void]$reasons.Add('INVALID_STATUS') }
    $hc = $conf4[$r.HomeTeamId]; $ac = $conf4[$r.AwayTeamId]
    if ($hc -ne 'CONFIRMED' -or $ac -ne 'CONFIRMED')          { [void]$reasons.Add('IDENTITY_UNRESOLVED') }
    $r.IdentityConfidence   = if ($hc -eq 'CONFIRMED' -and $ac -eq 'CONFIRMED') { 'CONFIRMED' } else { 'PROBABLE' }
    $r.ModelEligible        = ($reasons.Count -eq 0)
    $r.ModelExclusionReason = (@($reasons | Sort-Object -Unique) -join ';')
}
$eligible = @($master | Where-Object { $_.ModelEligible })
Write-Host ("model eligible: {0} / {1}" -f $eligible.Count, $master.Count)
$master | Where-Object { -not $_.ModelEligible } | Group-Object ModelExclusionReason | Sort-Object Count -Descending | Format-Table Count,Name -AutoSize

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
$cov | Format-Table -AutoSize

Write-Host ""
Write-Host "===== AFTER ====="
Write-Host ("Total           : {0}" -f $master.Count)
Write-Host ("Missing Score   : {0}" -f @($master | Where-Object { $null -eq $_.HomeGoals }).Count)
Write-Host ("Missing Date    : {0}" -f @($master | Where-Object { [string]::IsNullOrWhiteSpace($_.Date) }).Count)
Write-Host ("Conflicts unres.: {0}" -f @($confOut | Where-Object { $_.ModelExcluded }).Count)
Write-Host ("Teams not CONFIRMED: {0}" -f @($teamOut | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count)
Write-Host ("Model Eligible  : {0}" -f $eligible.Count)
Write-Host "DONE_Q4"
