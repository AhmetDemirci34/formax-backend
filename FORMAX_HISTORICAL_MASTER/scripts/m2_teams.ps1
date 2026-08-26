# FORMAX Historical Master — STAGE 2: primary-source selection + team identity resolution.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$stage = Import-Csv (Join-Path $Work 'staging.csv')
Write-Host "staged rows: $($stage.Count)"

# ---------------- primary source priority (only applied among sources that actually have rows) ----
$prio = @{
  'DOMESTIC_LEAGUE'            = @('football-data','openfootball-json','openfootball-txt','fixturedownload','openfootball-csv')
  'DOMESTIC_PLAYOFF'           = @('openfootball-json','openfootball-txt','football-data','fixturedownload','openfootball-csv')
  'UEFA_MAIN'                  = @('api-football','openfootball-txt','fixturedownload','openfootball-json')
  'UEFA_QUALIFIER'             = @('api-football','openfootball-txt','fixturedownload','openfootball-json')
  'UEFA_QUALIFICATION_PLAYOFF' = @('api-football','openfootball-txt','fixturedownload','openfootball-json')
}

$cells = @{}
foreach ($r in $stage) {
    $k = '{0}|{1}|{2}' -f $r.Competition, $r.Season, $r.CompetitionType
    if (-not $cells.ContainsKey($k)) { $cells[$k] = @{} }
    if (-not $cells[$k].ContainsKey($r.SourceKey)) { $cells[$k][$r.SourceKey] = 0 }
    $cells[$k][$r.SourceKey]++
}
$cellPrimary = @{}
foreach ($k in $cells.Keys) {
    $ctype = $k.Split('|')[2]
    $order = $prio[$ctype]
    $pick = $null
    foreach ($s in $order) { if ($cells[$k].ContainsKey($s)) { $pick = $s; break } }
    if (-not $pick) { $pick = ($cells[$k].GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key }
    $cellPrimary[$k] = $pick
}
$cellPrimary.GetEnumerator() | Sort-Object Name | ForEach-Object {
    [PSCustomObject]@{ Cell=$_.Name; PrimarySource=$_.Value
                       Sources=(($cells[$_.Name].GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ' ') }
} | Export-Csv (Join-Path $Work 'cell_primary.csv') -NoTypeInformation -Encoding UTF8
Write-Host "cells: $($cellPrimary.Count)"

# ---------------- competition-season level primary source (for team fingerprint anchoring) --------
$csPrimary = @{}
foreach ($k in $cellPrimary.Keys) {
    $p = $k.Split('|'); $cs = "$($p[0])|$($p[1])"
    $src = $cellPrimary[$k]; $cnt = $cells[$k][$src]
    if (-not $csPrimary.ContainsKey($cs) -or $csPrimary[$cs].Count -lt $cnt) {
        $csPrimary[$cs] = [PSCustomObject]@{ Source=$src; Count=$cnt }
    }
}

# ---------------- fingerprints: team -> set of "side|gf|gs|date" over a competition-season -------
$fp = @{}     # "comp|season|source|team" -> hashset
function FP-Add { param($key,$tok)
    if (-not $fp.ContainsKey($key)) { $fp[$key] = New-Object 'System.Collections.Generic.HashSet[string]' }
    [void]$fp[$key].Add($tok)
}
foreach ($r in $stage) {
    if ([string]::IsNullOrWhiteSpace($r.HomeGoals)) { continue }
    $cs = '{0}|{1}|{2}' -f $r.Competition, $r.Season, $r.SourceKey
    FP-Add "$cs|$($r.HomeRaw)" ("H|{0}|{1}|{2}" -f $r.HomeGoals, $r.AwayGoals, $r.Date)
    FP-Add "$cs|$($r.AwayRaw)" ("A|{0}|{1}|{2}" -f $r.AwayGoals, $r.HomeGoals, $r.Date)
}
Write-Host "fingerprint keys: $($fp.Count)"

# teams per (comp, season, source)
$teamsBy = @{}
foreach ($k in $fp.Keys) {
    $p = $k.Split('|'); $grp = "$($p[0])|$($p[1])|$($p[2])"; $team = $p[3]
    if (-not $teamsBy.ContainsKey($grp)) { $teamsBy[$grp] = New-Object System.Collections.ArrayList }
    [void]$teamsBy[$grp].Add($team)
}

# ---------------- opponent guard: two names that ever faced each other are NOT the same club ------
$opponents = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($r in $stage) {
    [void]$opponents.Add("$($r.HomeRaw)||$($r.AwayRaw)")
    [void]$opponents.Add("$($r.AwayRaw)||$($r.HomeRaw)")
}
Write-Host "opponent pairs indexed: $($opponents.Count)"

# ---------------- union-find over raw team names ----------------
$parent  = @{}
$members = @{}
$rejectedMerges = New-Object System.Collections.ArrayList
function UF-Find { param([string]$x)
    if (-not $parent.ContainsKey($x)) { $parent[$x] = $x; $members[$x] = [System.Collections.ArrayList]@($x); return $x }
    $root = $x
    while ($parent[$root] -ne $root) { $root = $parent[$root] }
    while ($parent[$x] -ne $root) { $nx = $parent[$x]; $parent[$x] = $root; $x = $nx }
    return $root
}
function UF-Union { param([string]$a,[string]$b,[string]$Why)
    $ra = UF-Find $a; $rb = UF-Find $b
    if ($ra -eq $rb) { return $true }
    foreach ($m1 in $members[$ra]) {
        foreach ($m2 in $members[$rb]) {
            if ($opponents.Contains("$m1||$m2")) {
                [void]$rejectedMerges.Add([PSCustomObject]@{ Kind='MERGE_REJECTED_OPPONENT'; NameA=$a; NameB=$b
                                          Evidence="'$m1' played against '$m2' in a real fixture"; Method=$Why })
                return $false
            }
        }
    }
    $parent[$rb] = $ra
    foreach ($m in $members[$rb]) { [void]$members[$ra].Add($m) }
    $members.Remove($rb)
    return $true
}
foreach ($k in $fp.Keys) { [void](UF-Find ($k.Split('|')[3])) }
foreach ($r in $stage) { [void](UF-Find $r.HomeRaw); [void](UF-Find $r.AwayRaw) }

$aliasLinks  = New-Object System.Collections.ArrayList
$unresolved  = New-Object System.Collections.ArrayList

foreach ($cs in ($csPrimary.Keys | Sort-Object)) {
    $comp,$season = $cs.Split('|')
    $pSrc = $csPrimary[$cs].Source
    $pGrp = "$cs|$pSrc"
    if (-not $teamsBy.ContainsKey($pGrp)) { continue }
    $pTeams = $teamsBy[$pGrp]

    $otherSrcs = $teamsBy.Keys | Where-Object { $_ -like "$cs|*" -and $_ -ne $pGrp }
    foreach ($oGrp in $otherSrcs) {
        $oSrc = $oGrp.Split('|')[2]
        foreach ($ot in $teamsBy[$oGrp]) {
            $oSet = $fp["$oGrp|$ot"]
            if ($oSet.Count -eq 0) { continue }
            $best = $null; $bestScore = 0.0; $second = 0.0
            $full = New-Object System.Collections.ArrayList
            foreach ($pt in $pTeams) {
                $pSet = $fp["$pGrp|$pt"]
                $inter = 0
                foreach ($t in $oSet) { if ($pSet.Contains($t)) { $inter++ } }
                if ($inter -eq 0) { continue }
                $score = $inter / [Math]::Min($oSet.Count, $pSet.Count)
                if ($score -ge 0.999) { [void]$full.Add([PSCustomObject]@{ Name=$pt; Set=$pSet; Inter=$inter }) }
                if ($score -gt $bestScore) { $second = $bestScore; $bestScore = $score; $best = $pt }
                elseif ($score -gt $second) { $second = $score }
            }
            # A primary source can spell ONE club two ways inside a single season (e.g. football-data
            # writes both "MGladbach" and "M'gladbach"). Then several primary names are each a full
            # subset of the same team's season. If those subsets are pairwise disjoint and together
            # cover the season, they are the same club -> link them all.
            if ($full.Count -ge 2) {
                $disjoint = $true; $covered = 0
                for ($x=0; $x -lt $full.Count; $x++) {
                    $covered += $full[$x].Inter
                    for ($y=$x+1; $y -lt $full.Count; $y++) {
                        foreach ($t in $full[$y].Set) { if ($full[$x].Set.Contains($t)) { $disjoint = $false; break } }
                        if (-not $disjoint) { break }
                    }
                    if (-not $disjoint) { break }
                }
                if ($disjoint -and $covered -ge (0.9 * $oSet.Count)) {
                    $anchor = $full[0].Name
                    $allOk = $true
                    foreach ($fx in $full) { if (-not (UF-Union $anchor $fx.Name 'split-spelling-in-primary')) { $allOk = $false } }
                    if ($allOk) {
                        $best = $anchor; $bestScore = 1.0; $second = 0.0
                        foreach ($fx in $full) {
                            [void]$aliasLinks.Add([PSCustomObject]@{ Competition=$comp; Season=$season; PrimarySource=$pSrc; PrimaryName=$fx.Name
                                                                     OtherSource=$oSrc; OtherName=$ot; Overlap=1; RunnerUp=0; Method='split-spelling-in-primary' })
                        }
                    }
                }
            }
            if ($best -and $bestScore -ge 0.5 -and ($second -eq 0 -or $bestScore -ge 2*$second)) {
                $ok = UF-Union $best $ot 'fixture-fingerprint'
                if ($ok) {
                    [void]$aliasLinks.Add([PSCustomObject]@{ Competition=$comp; Season=$season; PrimarySource=$pSrc; PrimaryName=$best
                                                            OtherSource=$oSrc; OtherName=$ot; Overlap=[Math]::Round($bestScore,3); RunnerUp=[Math]::Round($second,3); Method='fixture-fingerprint' })
                } else {
                    [void]$unresolved.Add([PSCustomObject]@{ Kind='TEAM_LINK'; Competition=$comp; Season=$season; Source=$oSrc; Name=$ot
                                                            BestPrimaryCandidate=$best; Overlap=[Math]::Round($bestScore,3); RunnerUp=[Math]::Round($second,3)
                                                            Note='fingerprint link BLOCKED by opponent guard (the two names faced each other in a real fixture)' })
                }
            } else {
                [void]$unresolved.Add([PSCustomObject]@{ Kind='TEAM_LINK'; Competition=$comp; Season=$season; Source=$oSrc; Name=$ot
                                                        BestPrimaryCandidate=$best; Overlap=[Math]::Round($bestScore,3); RunnerUp=[Math]::Round($second,3)
                                                        Note='fingerprint link rejected (low overlap or ambiguous)' })
            }
        }
    }
}
Write-Host "fingerprint alias links: $($aliasLinks.Count)   rejected: $($unresolved.Count)"

# ---------------- second pass: link by normalised name key ----------------
$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff','club','de','the','afc','ssc','ac','ogc','rc','sco','hsc','fco','usl')
function Norm-Key { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return '' }
    $s = $s -replace '\s*\([A-Z]{3}\)\s*',''
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = (-join $d).ToLowerInvariant() -replace '[^a-z0-9 ]',' '
    $t = @($s -split '\s+' | Where-Object { $_ -and ($noise -notcontains $_) })
    return ($t -join ' ')
}
$byNorm = @{}
foreach ($name in @($parent.Keys)) {
    $nk = Norm-Key $name
    if (-not $nk) { continue }
    if (-not $byNorm.ContainsKey($nk)) { $byNorm[$nk] = New-Object System.Collections.ArrayList }
    [void]$byNorm[$nk].Add($name)
}
$normLinks = 0
foreach ($nk in $byNorm.Keys) {
    $g = $byNorm[$nk]
    if ($g.Count -lt 2) { continue }
    for ($i=1; $i -lt $g.Count; $i++) { if ((UF-Find $g[0]) -ne (UF-Find $g[$i])) { if (UF-Union $g[0] $g[$i] 'normalised-name') { $normLinks++ } } }
}
Write-Host "normalised-name links: $normLinks   merges rejected by opponent guard: $($rejectedMerges.Count)"

# ---------------- canonical registry ----------------
$srcRank = @{ 'api-football'=1; 'openfootball-json'=2; 'openfootball-txt'=3; 'fixturedownload'=4; 'openfootball-csv'=5; 'football-data'=6 }
$nameSrc = @{}   # name -> best (rank) source seen
foreach ($r in $stage) {
    foreach ($nm in @($r.HomeRaw,$r.AwayRaw)) {
        $rk = $srcRank[$r.SourceKey]
        if (-not $nameSrc.ContainsKey($nm) -or $nameSrc[$nm].Rank -gt $rk) {
            $nameSrc[$nm] = [PSCustomObject]@{ Rank=$rk; Source=$r.SourceKey }
        }
    }
}
$groups = @{}
foreach ($name in @($parent.Keys)) {
    $root = UF-Find $name
    if (-not $groups.ContainsKey($root)) { $groups[$root] = New-Object System.Collections.ArrayList }
    [void]$groups[$root].Add($name)
}
$teamRows = New-Object System.Collections.ArrayList
$nameToId = @{}
$i = 0
foreach ($root in ($groups.Keys | Sort-Object)) {
    $members = @($groups[$root] | Sort-Object)
    # canonical display name: best-ranked source, then longest name (most complete)
    $canon = ($members | Sort-Object @{E={ if($nameSrc.ContainsKey($_)){$nameSrc[$_].Rank}else{9} }}, @{E={ -$_.Length }} | Select-Object -First 1)
    $canon = ($canon -replace '\s*\([A-Z]{3}\)\s*','').Trim()
    $i++
    $id = 'FMXT{0:d4}' -f $i
    foreach ($m in $members) { $nameToId[$m] = $id }
    [void]$teamRows.Add([PSCustomObject]@{ CanonicalTeamId=$id; CanonicalTeamName=$canon; AliasCount=$members.Count
                                           Aliases=($members -join ' | ') })
}
Write-Host "canonical teams: $($teamRows.Count)"

$teamRows   | Export-Csv (Join-Path $Work 'teams.csv')        -NoTypeInformation -Encoding UTF8
$aliasLinks | Export-Csv (Join-Path $Work 'team_links.csv')   -NoTypeInformation -Encoding UTF8
$unresolved     | Export-Csv (Join-Path $Work 'team_unresolved.csv') -NoTypeInformation -Encoding UTF8
$rejectedMerges | Export-Csv (Join-Path $Work 'team_merge_rejected.csv') -NoTypeInformation -Encoding UTF8
$nameToId.GetEnumerator() | ForEach-Object { [PSCustomObject]@{ RawName=$_.Key; CanonicalTeamId=$_.Value } } |
    Export-Csv (Join-Path $Work 'team_name_map.csv') -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "biggest alias groups:"
$teamRows | Sort-Object AliasCount -Descending | Select-Object -First 12 | Format-Table CanonicalTeamId,CanonicalTeamName,AliasCount,@{N='Aliases';E={$_.Aliases.Substring(0,[Math]::Min(90,$_.Aliases.Length))}} -AutoSize
