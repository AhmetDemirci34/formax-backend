# The minority value in a conflict is not always an "on pitch" score - in two of the four cases it
# is a disciplinary award or a plain source error. Label each column for what it really is.
$ErrorActionPreference = 'Stop'
$Out = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'

$notes = @{
 'Hellas Verona FC|2020-09-19' = @{ OnPitch='0-0'
   Note='Roma fielded an ineligible player; the official record is the 3-0 award (openfootball + api-football). football-data carries the on-pitch 0-0.' }
 'NEC Nijmegen|2023-10-01' = @{ OnPitch='1-2'
   Note='Match halted and completed later; 1-2 is the score when play stopped, 1-3 is the completed-match result (football-data + api-football).' }
 'Union Berlin|2024-12-14' = @{ OnPitch=''
   Note='1-1 is the score on the pitch and is what football-data and api-football record. openfootball carries 0-2, which corresponds to a disciplinary award; no second source confirms it, so the master keeps 1-1 and the 0-2 stays visible here.' }
 'Akhisar Belediyespor|2019-01-18' = @{ OnPitch=''
   Note='openfootball and api-football agree on 0-3; football-data 1-3 is an isolated discrepancy with no corroboration (not an award).' }
}

$c = Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv')
$c | ForEach-Object {
    $k = "$($_.HomeTeam)|$($_.Date)"
    $n = $notes[$k]
    [PSCustomObject]@{
        MatchKey=$_.MatchKey; MatchId=$_.MatchId; Competition=$_.Competition; Season=$_.Season; Date=$_.Date
        HomeTeam=$_.HomeTeam; AwayTeam=$_.AwayTeam
        SourceA=$_.SourceA; ScoreA=$_.ScoreA; SourceB=$_.SourceB; ScoreB=$_.ScoreB; SourceC=$_.SourceC; ScoreC=$_.ScoreC
        OfficialFinalResult=$_.OfficialFinalResult
        OnPitchResult=$(if ($n) { $n.OnPitch } else { '' })
        AlternateReportedResult=$_.OnPitchResult
        Resolution=$_.Resolution; ModelExcluded=$_.ModelExcluded
        Note=$(if ($n) { $n.Note } else { '' })
    }
} | Export-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') -NoTypeInformation -Encoding UTF8

# master: rename the two columns to what they actually hold
foreach ($f in 'FORMAX_HISTORICAL_MASTER.csv','FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv') {
    $p = Join-Path $Out $f
    $rows = Import-Csv $p
    $cols = $rows[0].PSObject.Properties.Name | ForEach-Object {
        if ($_ -eq 'OnPitchHomeGoals') { @{N='AlternateReportedHomeGoals';E=[scriptblock]::Create('$_.OnPitchHomeGoals')} }
        elseif ($_ -eq 'OnPitchAwayGoals') { @{N='AlternateReportedAwayGoals';E=[scriptblock]::Create('$_.OnPitchAwayGoals')} }
        else { $_ }
    }
    $rows | Select-Object $cols | Export-Csv $p -NoTypeInformation -Encoding UTF8
    Write-Host "relabelled columns in $f"
}
Import-Csv (Join-Path $Out 'FORMAX_HISTORICAL_CONFLICTS.csv') |
    Format-Table Date,HomeTeam,AwayTeam,ScoreA,ScoreB,ScoreC,OfficialFinalResult,OnPitchResult,AlternateReportedResult,ModelExcluded -AutoSize
