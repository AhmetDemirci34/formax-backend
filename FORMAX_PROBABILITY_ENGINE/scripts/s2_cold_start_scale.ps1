$ErrorActionPreference='Stop'
$CI=[Globalization.CultureInfo]::InvariantCulture
$Hist='C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$m = @(Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv') | Sort-Object Date, MatchId)

$domTeams = New-Object 'System.Collections.Generic.HashSet[string]'
$allTeams = New-Object 'System.Collections.Generic.HashSet[string]'
$cnt=@{}
foreach ($r in $m) {
  foreach ($id in @($r.HomeTeamId,$r.AwayTeamId)) {
    [void]$allTeams.Add($id); $cnt[$id] = $(if ($cnt.ContainsKey($id)) { $cnt[$id]+1 } else { 1 })
    if ($r.CompetitionType -eq 'DOMESTIC_LEAGUE') { [void]$domTeams.Add($id) }
  }
}
$uefaOnly = @($allTeams | Where-Object { -not $domTeams.Contains($_) })
Write-Host ("teams total: {0}   with domestic-league history: {1}   UEFA-only: {2} ({3}%)" -f $allTeams.Count,$domTeams.Count,$uefaOnly.Count,[Math]::Round(100.0*$uefaOnly.Count/$allTeams.Count,1))
$uefaOnlyMatches = ($uefaOnly | ForEach-Object { $cnt[$_] } | Measure-Object -Sum).Sum
Write-Host ("   their total match appearances in scope: {0}   median per team: {1}" -f $uefaOnlyMatches, (@($uefaOnly | ForEach-Object { $cnt[$_] } | Sort-Object)[[int]($uefaOnly.Count/2)]))

# cold start per match
$seen=@{}; $seenDom=@{}
$zero=0; $lowH=0; $noDom=0; $tot=0; $byType=@{}
foreach ($r in $m) {
  $tot++
  $h=$r.HomeTeamId; $a=$r.AwayTeamId
  $ph = $(if ($seen.ContainsKey($h)) { $seen[$h] } else { 0 }); $pa = $(if ($seen.ContainsKey($a)) { $seen[$a] } else { 0 })
  $dh = $(if ($seenDom.ContainsKey($h)) { $seenDom[$h] } else { 0 }); $da = $(if ($seenDom.ContainsKey($a)) { $seenDom[$a] } else { 0 })
  if ($ph -eq 0 -or $pa -eq 0) { $zero++ }
  if ($ph -lt 3 -or $pa -lt 3) { $lowH++ }
  if ($dh -eq 0 -or $da -eq 0) {
    $noDom++
    $k=$r.CompetitionType; $byType[$k] = $(if ($byType.ContainsKey($k)) { $byType[$k]+1 } else { 1 })
  }
  foreach ($id in @($h,$a)) {
    $seen[$id] = $(if ($seen.ContainsKey($id)) { $seen[$id]+1 } else { 1 })
    if ($r.CompetitionType -eq 'DOMESTIC_LEAGUE') { $seenDom[$id] = $(if ($seenDom.ContainsKey($id)) { $seenDom[$id]+1 } else { 1 }) }
  }
}
Write-Host ""
Write-Host ("matches where a side has ZERO prior in-scope match : {0} ({1}%)" -f $zero,[Math]::Round(100.0*$zero/$tot,2))
Write-Host ("matches where a side has <3 prior in-scope matches : {0} ({1}%)" -f $lowH,[Math]::Round(100.0*$lowH/$tot,2))
Write-Host ("matches where a side has NO prior DOMESTIC-LEAGUE match : {0} ({1}%)" -f $noDom,[Math]::Round(100.0*$noDom/$tot,2))
Write-Host "   of those, by competition type:"
$byType.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { Write-Host ("      {0,-28} {1}" -f $_.Key,$_.Value) }
