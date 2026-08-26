$ErrorActionPreference='Stop'
$CI=[Globalization.CultureInfo]::InvariantCulture
$Hist='C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$m = Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
Write-Host "=== outcome profile per CompetitionType (descriptive only, no model)"
Write-Host ("{0,-28} {1,7} {2,7} {3,7} {4,7} {5,8} {6,8} {7,8}" -f 'CompetitionType','n','home%','draw%','away%','avgGF_H','avgGF_A','avgTotal')
foreach ($g in ($m | Group-Object CompetitionType | Sort-Object Count -Descending)) {
    $n=$g.Count
    $h=@($g.Group | Where-Object { [int]$_.HomeGoals -gt [int]$_.AwayGoals }).Count
    $d=@($g.Group | Where-Object { [int]$_.HomeGoals -eq [int]$_.AwayGoals }).Count
    $a=$n-$h-$d
    $gh=($g.Group | Measure-Object -Property HomeGoals -Average).Average
    $ga=($g.Group | Measure-Object -Property AwayGoals -Average).Average
    Write-Host ("{0,-28} {1,7} {2,7} {3,7} {4,7} {5,8} {6,8} {7,8}" -f $g.Name,$n,
        [Math]::Round(100.0*$h/$n,1),[Math]::Round(100.0*$d/$n,1),[Math]::Round(100.0*$a/$n,1),
        [Math]::Round($gh,2),[Math]::Round($ga,2),[Math]::Round($gh+$ga,2))
}
Write-Host ""
Write-Host "=== two-legged ties inside UEFA (same pair, both orientations, within 30 days)"
$uefa = @($m | Where-Object { $_.CompetitionType -like 'UEFA*' })
$byPair=@{}
foreach ($r in $uefa) {
    $k = "$($r.Competition)|$($r.Season)|" + ((@($r.HomeTeamId,$r.AwayTeamId) | Sort-Object) -join '-')
    if (-not $byPair.ContainsKey($k)) { $byPair[$k]=New-Object System.Collections.ArrayList }
    [void]$byPair[$k].Add($r)
}
$two = @($byPair.Values | Where-Object { $_.Count -ge 2 })
Write-Host ("   distinct UEFA pairings: {0}   of which two or more legs: {1}  ({2}%)" -f $byPair.Count,$two.Count,[Math]::Round(100.0*$two.Count/$byPair.Count,1))
$legRows = ($two | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
Write-Host ("   UEFA rows that belong to a multi-leg tie: {0} / {1} = {2}%" -f $legRows,$uefa.Count,[Math]::Round(100.0*$legRows/$uefa.Count,1))
Write-Host ""
Write-Host "=== season span and rows per season"
$m | Group-Object Season | Sort-Object Name | ForEach-Object { Write-Host ("   {0}  {1,6}" -f $_.Name,$_.Count) }
