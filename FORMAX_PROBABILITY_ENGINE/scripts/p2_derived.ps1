# Probability Engine prep - STEP 2: can the derived features actually be computed, and for how
# many matches? Everything is measured with a strict "only matches strictly BEFORE this one" rule,
# which is also the leakage rule. No model, no coefficients.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Hist = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\prob_work'

$rows = Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
$rows = @($rows | Sort-Object Date, MatchId)
Write-Host ("rows: {0}   {1} .. {2}" -f $rows.Count, $rows[0].Date, $rows[-1].Date)

$hist      = @{}   # teamId -> ArrayList of [datetime]
$histComp  = @{}   # teamId|competition -> count
$histSeas  = @{}   # teamId|competition|season -> count
$h2h       = @{}   # sorted pair -> count
$out = New-Object System.Collections.ArrayList

foreach ($r in $rows) {
    $d = [datetime]::ParseExact($r.Date,'yyyy-MM-dd',$CI)
    $rec = [ordered]@{}
    foreach ($side in @(@('H',$r.HomeTeamId), @('A',$r.AwayTeamId))) {
        $tag = $side[0]; $id = $side[1]
        $lst = $hist[$id]
        $n   = if ($lst) { $lst.Count } else { 0 }
        $rec["${tag}Prior"]      = $n
        $rec["${tag}PriorComp"]  = $(if ($histComp.ContainsKey("$id|$($r.Competition)")) { $histComp["$id|$($r.Competition)"] } else { 0 })
        $rec["${tag}PriorSeason"]= $(if ($histSeas.ContainsKey("$id|$($r.Competition)|$($r.Season)")) { $histSeas["$id|$($r.Competition)|$($r.Season)"] } else { 0 })
        if ($n -gt 0) {
            $last = $lst[$lst.Count-1]
            $rec["${tag}DaysRest"] = [int]($d - $last).TotalDays
            $c7=0; $c14=0; $c21=0
            for ($i = $lst.Count-1; $i -ge 0; $i--) {
                $diff = ($d - $lst[$i]).TotalDays
                if ($diff -le 7)  { $c7++ }
                if ($diff -le 14) { $c14++ }
                if ($diff -le 21) { $c21++ } else { break }
            }
            $rec["${tag}In7"] = $c7; $rec["${tag}In14"] = $c14; $rec["${tag}In21"] = $c21
        } else { $rec["${tag}DaysRest"] = ''; $rec["${tag}In7"]=''; $rec["${tag}In14"]=''; $rec["${tag}In21"]='' }
    }
    $pk = (@($r.HomeTeamId,$r.AwayTeamId) | Sort-Object) -join '|'
    $rec['H2HPrior'] = $(if ($h2h.ContainsKey($pk)) { $h2h[$pk] } else { 0 })

    [void]$out.Add([PSCustomObject]@{
        MatchId=$r.MatchId; Date=$r.Date; Competition=$r.Competition; CompetitionType=$r.CompetitionType; Season=$r.Season
        HPrior=$rec['HPrior']; APrior=$rec['APrior']
        HPriorComp=$rec['HPriorComp']; APriorComp=$rec['APriorComp']
        HPriorSeason=$rec['HPriorSeason']; APriorSeason=$rec['APriorSeason']
        HDaysRest=$rec['HDaysRest']; ADaysRest=$rec['ADaysRest']
        HIn7=$rec['HIn7']; AIn7=$rec['AIn7']; HIn14=$rec['HIn14']; AIn14=$rec['AIn14']; HIn21=$rec['HIn21']; AIn21=$rec['AIn21']
        H2HPrior=$rec['H2HPrior'] })

    # advance history AFTER the row is scored - this is the leakage-safe order
    foreach ($id in @($r.HomeTeamId,$r.AwayTeamId)) {
        if (-not $hist.ContainsKey($id)) { $hist[$id] = New-Object System.Collections.ArrayList }
        [void]$hist[$id].Add($d)
        $k1 = "$id|$($r.Competition)";                       $histComp[$k1] = $(if ($histComp.ContainsKey($k1)) { $histComp[$k1]+1 } else { 1 })
        $k2 = "$id|$($r.Competition)|$($r.Season)";          $histSeas[$k2] = $(if ($histSeas.ContainsKey($k2)) { $histSeas[$k2]+1 } else { 1 })
    }
    $h2h[$pk] = $(if ($h2h.ContainsKey($pk)) { $h2h[$pk]+1 } else { 1 })
}
$out | Export-Csv (Join-Path $Work 'prior_history.csv') -NoTypeInformation -Encoding UTF8

function Pct { param($n,$d) if ($d -eq 0) { 0 } else { [Math]::Round(100.0*$n/$d,2) } }
$tot = $out.Count
Write-Host ""
Write-Host "=== how many matches have enough PRIOR history for both teams (any competition in scope)"
foreach ($k in 1,3,5,10,20) {
    $n = @($out | Where-Object { [int]$_.HPrior -ge $k -and [int]$_.APrior -ge $k }).Count
    Write-Host ("   both teams >= {0,2} prior matches : {1,6} / {2}  = {3,6}%" -f $k, $n, $tot, (Pct $n $tot))
}
Write-Host ""
Write-Host "=== same, split by CompetitionType"
foreach ($g in ($out | Group-Object CompetitionType)) {
    $line = "   {0,-28}" -f $g.Name
    foreach ($k in 1,5,10) {
        $n = @($g.Group | Where-Object { [int]$_.HPrior -ge $k -and [int]$_.APrior -ge $k }).Count
        $line += ("  >={0,-2} {1,6}%" -f $k, (Pct $n $g.Count))
    }
    Write-Host $line
}
Write-Host ""
Write-Host "=== prior history INSIDE the same competition (both teams)"
foreach ($g in ($out | Group-Object CompetitionType)) {
    $n5 = @($g.Group | Where-Object { [int]$_.HPriorComp -ge 5 -and [int]$_.APriorComp -ge 5 }).Count
    Write-Host ("   {0,-28} both >=5 in-competition : {1,6}%" -f $g.Name, (Pct $n5 $g.Count))
}
Write-Host ""
Write-Host "=== season-to-date history (league table style features)"
foreach ($g in ($out | Group-Object CompetitionType)) {
    $n3 = @($g.Group | Where-Object { [int]$_.HPriorSeason -ge 3 -and [int]$_.APriorSeason -ge 3 }).Count
    Write-Host ("   {0,-28} both >=3 matches this season : {1,6}%" -f $g.Name, (Pct $n3 $g.Count))
}
Write-Host ""
Write-Host "=== rest / congestion"
$rest = @($out | Where-Object { $_.HDaysRest -ne '' -and $_.ADaysRest -ne '' })
Write-Host ("   both teams have a previous match in scope : {0,6}%" -f (Pct $rest.Count $tot))
$g20 = @($rest | Where-Object { [int]$_.HDaysRest -gt 20 -or [int]$_.ADaysRest -gt 20 }).Count
Write-Host ("   of those, rest gap > 20 days for a side   : {0,6}%   <- our scope has no domestic cups/internationals, so gaps can be artificial" -f (Pct $g20 $rest.Count))
$med = @($rest | ForEach-Object { [int]$_.HDaysRest } | Sort-Object)
Write-Host ("   median home rest days: {0}" -f $med[[int]($med.Count/2)])
Write-Host ""
Write-Host "=== head to head"
foreach ($k in 1,2,4) {
    $n = @($out | Where-Object { [int]$_.H2HPrior -ge $k }).Count
    Write-Host ("   >= {0} previous meeting(s) : {1,6}%" -f $k, (Pct $n $tot))
}
Write-Host ""
Write-Host "=== matches per team in the whole dataset (team strength sample size)"
$cnt = @{}
foreach ($r in $rows) { foreach ($id in @($r.HomeTeamId,$r.AwayTeamId)) { $cnt[$id] = $(if ($cnt.ContainsKey($id)) { $cnt[$id]+1 } else { 1 }) } }
$vals = @($cnt.Values | Sort-Object)
Write-Host ("   teams: {0}   min {1}   median {2}   max {3}" -f $cnt.Count, $vals[0], $vals[[int]($vals.Count/2)], $vals[-1])
foreach ($b in 2,5,10,20,50) {
    $n = @($cnt.Values | Where-Object { $_ -lt $b }).Count
    Write-Host ("   teams with fewer than {0,2} matches in scope : {1,4}  ({2}%)" -f $b, $n, (Pct $n $cnt.Count))
}
