$ErrorActionPreference = 'Stop'
$Out = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$m = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$e = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
$x = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MODEL_EXCLUSIONS.csv')
$t = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$c = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
$lines = New-Object System.Collections.ArrayList
function W { param($s) Write-Host $s; [void]$lines.Add($s) }

W "===== ACCEPTANCE CHECKS ====="
$dupKey = @($m | Group-Object { '{0}|{1}|{2}|{3}|{4}' -f $_.Competition,$_.Season,$_.Date,$_.HomeTeamId,$_.AwayTeamId } | Where-Object { $_.Count -gt 1 })
$dupId  = @($m | Group-Object MatchId | Where-Object { $_.Count -gt 1 })
W ("duplicate canonical keys ........ {0}   (required 0)" -f $dupKey.Count)
W ("duplicate MatchIds .............. {0}   (required 0)" -f $dupId.Count)
W ("master rows ..................... {0}" -f $m.Count)
W ("model eligible rows ............. {0}" -f $e.Count)
W ("excluded rows ................... {0}   (file rows: {1})" -f @($m | Where-Object { $_.ModelEligible -eq 'False' }).Count, $x.Count)
W ("missing date (master) ........... {0}   (required 0)" -f @($m | Where-Object { $_.Date -eq '' }).Count)
W ("missing score (master) .......... {0}" -f @($m | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' }).Count)
W ("missing score (model eligible) .. {0}   (required 0)" -f @($e | Where-Object { $_.HomeGoals -eq '' -or $_.AwayGoals -eq '' }).Count)
W ("missing date  (model eligible) .. {0}   (required 0)" -f @($e | Where-Object { $_.Date -eq '' }).Count)
W ("identity not CONFIRMED (eligible) {0}   (required 0)" -f @($e | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count)
W ("competition/season blank (elig.). {0}   (required 0)" -f @($e | Where-Object { $_.Competition -eq '' -or $_.Season -eq '' -or $_.CompetitionType -eq '' }).Count)
W ("status not FT/AET/PEN (eligible)  {0}   (required 0)" -f @($e | Where-Object { $_.MatchStatus -notin @('FT','AET','PEN') }).Count)
W ("unresolved conflicts in eligible  {0}   (required 0)" -f @($e | Where-Object { $_.ResultResolution -like 'UNRESOLVED*' }).Count)
W ("teams not CONFIRMED ............. {0} / {1}" -f @($t | Where-Object { $_.IdentityConfidence -ne 'CONFIRMED' }).Count, $t.Count)

W ""
W "----- score field integrity -----"
$bad = @($e | Where-Object { $_.ExtraTimeHomeGoals -ne '' -and $_.RegularTimeHomeGoals -ne '' -and
                             ([int]$_.RegularTimeHomeGoals + [int]$_.ExtraTimeHomeGoals) -ne [int]$_.HomeGoals })
W ("RegularTime + ExtraTime <> HomeGoals ... {0}" -f $bad.Count)
W ("shootout score but status <> PEN ....... {0}" -f @($e | Where-Object { $_.PenaltyShootoutHome -ne '' -and $_.MatchStatus -ne 'PEN' }).Count)
W ("status AET/PEN without ET score ........ {0}" -f @($e | Where-Object { $_.MatchStatus -in @('AET','PEN') -and $_.ExtraTimeHomeGoals -eq '' }).Count)
W ("negative or absurd goals (>15) ......... {0}" -f @($e | Where-Object { [int]$_.HomeGoals -lt 0 -or [int]$_.AwayGoals -lt 0 -or [int]$_.HomeGoals -gt 15 -or [int]$_.AwayGoals -gt 15 }).Count)

W ""
W "----- model eligible composition -----"
$e | Group-Object CompetitionType | Sort-Object Count -Descending | ForEach-Object { W ("   {0,-28} {1,6}" -f $_.Name, $_.Count) }
W ("   distinct teams: {0}" -f @(($e | ForEach-Object { $_.HomeTeamId; $_.AwayTeamId }) | Sort-Object -Unique).Count)
W ("   date range    : {0} .. {1}" -f ($e | Sort-Object Date | Select-Object -First 1).Date, ($e | Sort-Object Date | Select-Object -Last 1).Date)
W ("   status        : {0}" -f (($e | Group-Object MatchStatus | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ' '))
W ("   provenance    : {0}" -f (($e | Group-Object SourceCount | Sort-Object Name | ForEach-Object { "$($_.Name) src=$($_.Count)" }) -join ' '))
W ("   with ProviderMatchId: {0}" -f @($e | Where-Object { $_.ProviderMatchId -ne '' }).Count)

W ""
W "----- conflicts -----"
foreach ($r in $c) { W ("   {0} {1} v {2} | A({3})={4} B({5})={6} C(api)={7} -> official {8}, on pitch {9} [{10}]" -f `
    $r.Date,$r.HomeTeam,$r.AwayTeam,$r.SourceA,$r.ScoreA,$r.SourceB,$r.ScoreB,$r.ScoreC,$r.OfficialFinalResult,$r.OnPitchResult,$r.Resolution) }

W ""
W "----- exclusions -----"
foreach ($r in $x) { W ("   {0} {1} {2} v {3} | {4}" -f $r.Date,$r.Competition,$r.HomeTeam,$r.AwayTeam,$r.Reason) }

W ""
W "----- api usage -----"
$log = Get-Content (Join-Path $Out '_api_call_log.txt')
W ("   total api-football calls in this gate: {0}" -f $log.Count)
$log | ForEach-Object { W ("     " + ($_ -split "`t")[1]) }

$lines | Out-File (Join-Path $Out '_gate_verification.txt') -Encoding utf8
