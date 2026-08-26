$ErrorActionPreference = 'Stop'
$Out = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$m = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$lines = New-Object System.Collections.ArrayList
function W { param($s) Write-Host $s; [void]$lines.Add($s) }

W ("master rows                : {0}" -f $m.Count)
W ("distinct canonical teams   : {0}" -f (@(($m | ForEach-Object { $_.HomeTeamId; $_.AwayTeamId }) | Sort-Object -Unique).Count))
W ("date range                 : {0} .. {1}" -f ($m | Sort-Object Date | Select-Object -First 1).Date, ($m | Sort-Object Date | Select-Object -Last 1).Date)
W ""
W "status:"
$m | Group-Object MatchStatus | Sort-Object Count -Descending | ForEach-Object { W ("   {0,-12} {1,6}" -f $_.Name, $_.Count) }
W "primary source of master row:"
$m | Group-Object Source | Sort-Object Count -Descending | ForEach-Object { W ("   {0,-20} {1,6}" -f $_.Name, $_.Count) }
W "SourceCount (how many independent sources carry the match):"
$m | Group-Object SourceCount | Sort-Object Name | ForEach-Object { W ("   {0} source(s): {1,6}" -f $_.Name, $_.Count) }
W "flags:"
$m | Where-Object { $_.DataQualityFlag -ne '' } | Group-Object DataQualityFlag | Sort-Object Count -Descending | ForEach-Object { W ("   {0,-70} {1,6}" -f $_.Name, $_.Count) }
W "missing:"
W ("   score  : {0}" -f @($m | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' }).Count)
W ("   date   : {0}" -f @($m | Where-Object { $_.Date -eq '' }).Count)
W ("   round  : {0}" -f @($m | Where-Object { $_.Round -eq '' }).Count)
W ("   halftime: {0}" -f @($m | Where-Object { $_.HalfTimeHomeGoals -eq '' }).Count)
W ("   kickoff time: {0}" -f @($m | Where-Object { $_.KickoffLocalTime -eq '' }).Count)
W "extra time / shootout:"
W ("   AET rows: {0}   PEN rows: {1}" -f @($m | Where-Object { $_.MatchStatus -eq 'AET' }).Count, @($m | Where-Object { $_.MatchStatus -eq 'PEN' }).Count)
W "missing score by competition/season:"
$m | Where-Object { $_.HomeGoals -eq '' } | Group-Object Competition,Season | Sort-Object Count -Descending | ForEach-Object { W ("   {0,-34} {1,5}" -f $_.Name, $_.Count) }
$lines | Out-File (Join-Path $Out '_final_stats.txt') -Encoding utf8
