# Pair #42: "Vikingur" vs "Vikingur Gota". Two fixtures only, and both ended with the same
# scoreline pattern, so the score fingerprint ties. Decide on the OPPONENTS instead - those are
# unambiguous, and the two candidate clubs (Faroese Vikingur Gota / Icelandic Vikingur Reykjavik)
# played completely different opponents.
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$master = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_MASTER.csv')
$teams  = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_TEAMS.csv')
$ctx    = Import-Csv (Join-Path $Work 'lookalike_context.csv')
$row    = $ctx | Where-Object { $_.No -eq '42' }
$idA = $row.IdA; $idB = $row.IdB

Write-Host "A = $idA '$($row.NameA)'   aliases: $($row.AliasesA)"
Write-Host "B = $idB '$($row.NameB)'   aliases: $($row.AliasesB)"
Write-Host ""
Write-Host "--- every master fixture of A"
$master | Where-Object { $_.HomeTeamId -eq $idA -or $_.AwayTeamId -eq $idA } |
    Sort-Object Date | Format-Table Date,Competition,CompetitionType,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Source -AutoSize
Write-Host "--- every master fixture of B"
$master | Where-Object { $_.HomeTeamId -eq $idB -or $_.AwayTeamId -eq $idB } |
    Sort-Object Date | Format-Table Date,Competition,CompetitionType,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Source -AutoSize

$dates = @($master | Where-Object { $_.HomeTeamId -eq $idA -or $_.AwayTeamId -eq $idA } | ForEach-Object { $_.Date } | Sort-Object -Unique)
Write-Host "--- api-football Conference League 2025/26 fixtures on the same dates, for any club named Vikingur"
$j = Get-Content (Join-Path $Raw 'fx_848_2025.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($x in $j.response) {
    $d = ([datetime]::Parse($x.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind)).ToUniversalTime().ToString('yyyy-MM-dd')
    if ($x.teams.home.name -notlike '*ikingur*' -and $x.teams.away.name -notlike '*ikingur*') { continue }
    $mark = if ($dates -contains $d) { '  <== same date as A' } else { '' }
    Write-Host ("   {0} {1} (id {2}) v {3} (id {4})  {5}-{6}{7}" -f $d,$x.teams.home.name,$x.teams.home.id,$x.teams.away.name,$x.teams.away.id,$x.goals.home,$x.goals.away,$mark)
}
