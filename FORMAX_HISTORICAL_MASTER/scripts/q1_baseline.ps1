$ErrorActionPreference = 'Stop'
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$m = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$c = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
$u = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_UNRESOLVED.csv')

Write-Host "===== BASELINE (before this gate) ====="
Write-Host ("Total matches      : {0}" -f $m.Count)
Write-Host ("Missing score      : {0}" -f @($m | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' }).Count)
Write-Host ("Missing date       : {0}" -f @($m | Where-Object { $_.Date -eq '' }).Count)
Write-Host ("Conflict rows      : {0}  (distinct matches: {1})" -f $c.Count, (@($c | Select-Object -ExpandProperty MatchId -Unique).Count))
Write-Host ("Unresolved team lnk: {0}" -f @($u | Where-Object { $_.Kind -eq 'TEAM_LINK_UNRESOLVED' }).Count)
Write-Host ("Merge rejected     : {0}" -f @($u | Where-Object { $_.Kind -eq 'TEAM_MERGE_REJECTED' }).Count)
Write-Host ("Duplicate keys     : {0}" -f @($m | Group-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Competition,$_.Season,$_.Date,$_.HomeTeamId,$_.AwayTeamId } | Where-Object { $_.Count -gt 1 }).Count)
Write-Host ("ModelEligible col  : {0}" -f $(if ($m[0].PSObject.Properties.Name -contains 'ModelEligible') { 'exists' } else { 'NOT PRESENT YET' }))

$miss = @($m | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' })
Write-Host ""
Write-Host "===== MISSING SCORE - real list from master ====="
$miss | Group-Object Competition,Season | Sort-Object Count -Descending | ForEach-Object { Write-Host ("   {0,-34} {1,4}" -f $_.Name, $_.Count) }
Write-Host ("   date range of missing: {0} .. {1}" -f ($miss | Sort-Object Date | Select-Object -First 1).Date, ($miss | Sort-Object Date | Select-Object -Last 1).Date)
$miss | Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam,HomeTeamId,AwayTeamId,MatchStatus,Source |
    Export-Csv (Join-Path $Work 'missing_score_list.csv') -NoTypeInformation -Encoding UTF8
Write-Host ("   written: missing_score_list.csv ({0} rows)" -f $miss.Count)

Write-Host ""
Write-Host "===== CONFLICTS (distinct matches) ====="
$c | Group-Object MatchId | ForEach-Object {
    $g = $_.Group[0]
    Write-Host ("   {0} {1} {2} | {3} v {4} | A={5} {6} | B(s)={7}" -f $g.MatchId,$g.Season,$g.Date,$g.HomeTeam,$g.AwayTeam,$g.SourceA,$g.ScoreA,
        (($_.Group | ForEach-Object { "$($_.SourceB)=$($_.ScoreB)" }) -join ' '))
}
$c | Group-Object MatchId | ForEach-Object { $_.Group[0] } |
    Select-Object MatchId,Competition,Season,Date,HomeTeam,AwayTeam |
    Export-Csv (Join-Path $Work 'conflict_list.csv') -NoTypeInformation -Encoding UTF8
