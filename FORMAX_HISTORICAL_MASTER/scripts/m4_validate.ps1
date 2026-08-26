$ErrorActionPreference = 'Stop'
$Out = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$m = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
Write-Host ("master rows: {0}" -f $m.Count)

$dup = @($m | Group-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Competition,$_.Season,$_.Date,$_.HomeTeamId,$_.AwayTeamId } | Where-Object { $_.Count -gt 1 })
Write-Host ("duplicate canonical keys (Competition|Season|Date|Home|Away): {0}" -f $dup.Count)
$dup | Select-Object -First 10 | ForEach-Object { Write-Host ("   DUP {0}" -f $_.Name) }

$dupId = @($m | Group-Object MatchId | Where-Object { $_.Count -gt 1 })
Write-Host ("duplicate MatchIds: {0}" -f $dupId.Count)

Write-Host ""
Write-Host "--- domestic league round-robin test  n teams => n*(n-1) matches"
foreach ($g in ($m | Where-Object { $_.CompetitionType -eq 'DOMESTIC_LEAGUE' } | Group-Object Competition,Season)) {
    $ids = @(($g.Group | ForEach-Object { $_.HomeTeamId; $_.AwayTeamId }) | Sort-Object -Unique)
    $n = $ids.Count; $exp = $n*($n-1)
    if ($exp -ne $g.Count) { Write-Host ("   MISMATCH {0,-34} teams={1,3} matches={2,4} expected={3,4}" -f $g.Name,$n,$g.Count,$exp) }
}

Write-Host ""
Write-Host "--- canonical teams sharing a normalised name (possible split identity)"
$t = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$split = @($t | Group-Object { ($_.CanonicalTeamName -replace '[^A-Za-z0-9]','').ToLowerInvariant() } | Where-Object { $_.Count -gt 1 })
Write-Host ("   count: {0}" -f $split.Count)
$split | ForEach-Object { Write-Host ("   {0}: {1}" -f $_.Name, (($_.Group | ForEach-Object { $_.CanonicalTeamId + '=' + $_.CanonicalTeamName }) -join ' / ')) }

Write-Host ""
Write-Host "--- score-field integrity"
$bad = @($m | Where-Object {
    $_.ExtraTimeHomeGoals -ne '' -and $_.RegularTimeHomeGoals -ne '' -and $_.HomeGoals -ne '' -and
    ([int]$_.RegularTimeHomeGoals + [int]$_.ExtraTimeHomeGoals) -ne [int]$_.HomeGoals })
Write-Host ("   rows where RegularTime + ExtraTime <> HomeGoals: {0}" -f $bad.Count)
$bad | Select-Object -First 5 | Format-Table Date,Competition,HomeTeam,AwayTeam,HomeGoals,RegularTimeHomeGoals,ExtraTimeHomeGoals,MatchStatus -AutoSize

$penNoStatus = @($m | Where-Object { $_.PenaltyShootoutHome -ne '' -and $_.MatchStatus -ne 'PEN' })
Write-Host ("   rows with a shootout score but status <> PEN: {0}" -f $penNoStatus.Count)
$aetNoEt = @($m | Where-Object { $_.MatchStatus -eq 'AET' -and $_.ExtraTimeHomeGoals -eq '' })
Write-Host ("   rows with status AET but no extra-time score: {0}" -f $aetNoEt.Count)

Write-Host ""
Write-Host "--- status distribution"
$m | Group-Object MatchStatus | Sort-Object Count -Descending | Format-Table Count,Name -AutoSize
Write-Host "--- source distribution (primary source of the master row)"
$m | Group-Object Source | Sort-Object Count -Descending | Format-Table Count,Name -AutoSize
Write-Host "--- SourceCount distribution"
$m | Group-Object SourceCount | Sort-Object Name | Format-Table Count,Name -AutoSize
Write-Host "--- flags"
$m | Where-Object { $_.DataQualityFlag -ne '' } | Group-Object DataQualityFlag | Format-Table Count,Name -AutoSize
Write-Host "--- missing values"
Write-Host ("   missing score : {0}" -f @($m | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' }).Count)
Write-Host ("   missing date  : {0}" -f @($m | Where-Object { $_.Date -eq '' }).Count)
Write-Host ("   missing round : {0}" -f @($m | Where-Object { $_.Round -eq '' }).Count)
Write-Host ("   missing HT    : {0}" -f @($m | Where-Object { $_.HalfTimeHomeGoals -eq '' }).Count)
