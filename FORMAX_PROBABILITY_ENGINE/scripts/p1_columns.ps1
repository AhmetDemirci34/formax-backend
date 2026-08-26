# Probability Engine prep - STEP 1: real column inventory of the model dataset.
# Measures only. Writes nothing into the historical dataset.
$ErrorActionPreference = 'Stop'
$CI   = [Globalization.CultureInfo]::InvariantCulture
$Hist = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\prob_work'
New-Item -ItemType Directory -Force -Path $Work | Out-Null

$rows = Import-Csv (Join-Path $Hist 'FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv')
Write-Host ("model-eligible rows: {0}" -f $rows.Count)
$cols = $rows[0].PSObject.Properties.Name
Write-Host ("columns: {0}" -f $cols.Count)

$numericCols = @('HomeGoals','AwayGoals','RegularTimeHomeGoals','RegularTimeAwayGoals','HalfTimeHomeGoals','HalfTimeAwayGoals',
                 'ExtraTimeHomeGoals','ExtraTimeAwayGoals','PenaltyShootoutHome','PenaltyShootoutAway',
                 'AlternateReportedHomeGoals','AlternateReportedAwayGoals','SourceCount')

$inv = New-Object System.Collections.ArrayList
foreach ($c in $cols) {
    $vals = @($rows | ForEach-Object { $_.$c })
    $nonNull = @($vals | Where-Object { $_ -ne '' -and $null -ne $_ })
    $uniq = @($nonNull | Sort-Object -Unique)
    $min = ''; $max = ''
    if ($numericCols -contains $c -and $nonNull.Count -gt 0) {
        $nums = @($nonNull | ForEach-Object { [int]$_ })
        $min = ($nums | Measure-Object -Minimum).Minimum
        $max = ($nums | Measure-Object -Maximum).Maximum
        $type = 'integer'
    } elseif ($c -in @('Date')) {
        $min = ($nonNull | Sort-Object | Select-Object -First 1); $max = ($nonNull | Sort-Object | Select-Object -Last 1); $type = 'date (yyyy-MM-dd)'
    } elseif ($c -eq 'KickoffUtc') {
        $min = ($nonNull | Sort-Object | Select-Object -First 1); $max = ($nonNull | Sort-Object | Select-Object -Last 1); $type = 'timestamp (ISO ...Z)'
    } elseif ($c -eq 'ModelEligible') { $type = 'boolean' }
    else { $type = 'string' }

    [void]$inv.Add([PSCustomObject]@{
        Column        = $c
        Type          = $type
        NonNullCount  = $nonNull.Count
        NullCount     = ($rows.Count - $nonNull.Count)
        NullPct       = [Math]::Round(100.0 * ($rows.Count - $nonNull.Count) / $rows.Count, 2)
        UniqueCount   = $uniq.Count
        Min           = "$min"
        Max           = "$max"
        SampleValues  = (($uniq | Select-Object -First 4) -join ' | ')
    })
    Write-Host ("  {0,-28} null {1,6} ({2,5}%)  uniq {3,6}" -f $c, ($rows.Count - $nonNull.Count), [Math]::Round(100.0*($rows.Count-$nonNull.Count)/$rows.Count,2), $uniq.Count)
}
$inv | Export-Csv (Join-Path $Work 'column_inventory.csv') -NoTypeInformation -Encoding UTF8

# Round availability by competition type - Round is the one field with a real gap
Write-Host ""
Write-Host "--- Round coverage by CompetitionType"
foreach ($g in ($rows | Group-Object CompetitionType)) {
    $have = @($g.Group | Where-Object { $_.Round -ne '' }).Count
    Write-Host ("  {0,-28} {1,6}/{2,-6} = {3,6}%" -f $g.Name, $have, $g.Count, [Math]::Round(100.0*$have/$g.Count,2))
}
Write-Host ""
Write-Host "--- Round coverage by Competition x Season where it is NOT complete"
foreach ($g in ($rows | Group-Object Competition,Season)) {
    $have = @($g.Group | Where-Object { $_.Round -ne '' }).Count
    if ($have -eq $g.Count) { continue }
    Write-Host ("  {0,-40} {1,5}/{2,-5}" -f $g.Name, $have, $g.Count)
}
Write-Host ""
Write-Host "--- kickoff time availability"
Write-Host ("  KickoffLocalTime present: {0}" -f @($rows | Where-Object { $_.KickoffLocalTime -ne '' }).Count)
Write-Host ("  KickoffUtc present      : {0}" -f @($rows | Where-Object { $_.KickoffUtc -ne '' }).Count)
Write-Host ("  ProviderMatchId present : {0}" -f @($rows | Where-Object { $_.ProviderMatchId -ne '' }).Count)
Write-Host ("  HalfTime score present  : {0}" -f @($rows | Where-Object { $_.HalfTimeHomeGoals -ne '' }).Count)
