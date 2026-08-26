# Cross-source duplicate check: exported gap CSVs vs EVERY existing local UEFA source.
# Existing sources are only READ, never modified.
$ErrorActionPreference = 'Stop'
$OutDir = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_GAPS'
$Inv    = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\inv\champions-league-master\champions-league-master'
$Vs     = 'C:\Users\dikim\Desktop\veri setleri'

$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','ss','if','ik','ff','fk','club','de','the','1893','03')

function Norm {
    param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return @() }
    $s = $s -replace '\([A-Z]{3}\)', ''
    $f = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = (-join $f).ToLowerInvariant() -replace '[^a-z0-9 ]', ' '
    return @($s -split '\s+' | Where-Object { $_.Length -ge 3 -and $noise -notcontains $_ })
}

# ---------- parse existing openfootball .txt sources ----------
$existing = New-Object System.Collections.ArrayList
function Add-Existing { param($Family,$Date,$HomeName,$AwayName,$Src)
    [void]$existing.Add([PSCustomObject]@{ Family=$Family; Date=$Date; H=(Norm $HomeName); A=(Norm $AwayName); Src=$Src })
}

$famOf = @{ 'cl'='CL'; 'clq'='CL'; 'el'='EL'; 'elq'='EL'; 'conf'='CONF'; 'confq'='CONF' }
foreach ($f in Get-ChildItem $Inv -Recurse -Filter *.txt) {
    $fam = $famOf[$f.BaseName]; if (-not $fam) { continue }
    $year = $null; $curDate = $null
    foreach ($line in (Get-Content $f.FullName -Encoding UTF8)) {
        if ($line -match '^\s*(Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+([A-Z][a-z]{2})\s+(\d{1,2})(\s+(\d{4}))?\s*$') {
            if ($matches[5]) { $year = $matches[5] }
            $curDate = [datetime]::ParseExact("$($matches[3]) $($matches[2]) $year", 'd MMM yyyy', [Globalization.CultureInfo]::InvariantCulture).ToString('yyyy-MM-dd')
            continue
        }
        if ($curDate -and $line -match '^\s+(\d{2}:\d{2}\s+)?(.+?)\s+v\s+(.+?)\s{2,}(\d+.*)$') {
            Add-Existing $fam $curDate $matches[2] $matches[3] $f.FullName
        }
    }
}

# ---------- parse existing fixture-download CSVs ----------
$csvMap = @{
    'champions-league-2017-CentralEuropeanStandardTime.csv' = 'CL'
    'champions-league-2025-UTC.csv'                         = 'CL'
    'europa-league-2019-WEuropeStandardTime.csv'            = 'EL'
    'europa-league-2025-UTC.csv'                            = 'EL'
    'conference-league-2025-UTC.csv'                        = 'CONF'
}
foreach ($k in $csvMap.Keys) {
    $p = Join-Path $Vs $k
    if (-not (Test-Path $p)) { Write-Host "  (missing $k)"; continue }
    foreach ($row in (Import-Csv $p)) {
        $d = [datetime]::ParseExact($row.Date.Split(' ')[0], 'dd/MM/yyyy', [Globalization.CultureInfo]::InvariantCulture).ToString('yyyy-MM-dd')
        Add-Existing $csvMap[$k] $d $row.'Home Team' $row.'Away Team' $p
    }
}

Write-Host "EXISTING local UEFA match rows indexed: $($existing.Count)"
$existing | Group-Object Family | ForEach-Object { Write-Host "   $($_.Name): $($_.Count)" }

$byKey = @{}
foreach ($e in $existing) {
    $k = "$($e.Family)|$($e.Date)"
    if (-not $byKey.ContainsKey($k)) { $byKey[$k] = New-Object System.Collections.ArrayList }
    [void]$byKey[$k].Add($e)
}

# ---------- compare ----------
$famOfComp = @{ '2'='CL'; '3'='EL'; '848'='CONF' }
$dupRows   = New-Object System.Collections.ArrayList
$dateHits  = 0; $total = 0

foreach ($file in (Get-ChildItem $OutDir -Filter *.csv | Where-Object { $_.Name -notlike '_*' })) {
    $rows = Import-Csv $file.FullName
    $fileDup = 0; $fileDateHit = 0
    foreach ($r in $rows) {
        $total++
        $k = "$($famOfComp[$r.CompetitionId])|$($r.MatchDate)"
        if (-not $byKey.ContainsKey($k)) { continue }
        $fileDateHit++; $dateHits++
        $h = Norm $r.HomeTeam; $a = Norm $r.AwayTeam
        foreach ($e in $byKey[$k]) {
            $hm = @($h | Where-Object { $e.H -contains $_ }).Count -gt 0
            $am = @($a | Where-Object { $e.A -contains $_ }).Count -gt 0
            if ($hm -and $am) {
                $fileDup++
                [void]$dupRows.Add([PSCustomObject]@{ File=$file.Name; Date=$r.MatchDate; Home=$r.HomeTeam; Away=$r.AwayTeam; Round=$r.Round; ExistingSource=$e.Src })
                break
            }
        }
    }
    Write-Host ("{0,-46} rows={1,5}  same-date-as-existing={2,4}  DUPLICATE={3}" -f $file.Name, $rows.Count, $fileDateHit, $fileDup)
}

Write-Host ""
Write-Host "TOTAL exported rows checked: $total | same-date collisions: $dateHits | real duplicates: $($dupRows.Count)"
if ($dupRows.Count -gt 0) {
    $dupRows | Export-Csv (Join-Path $OutDir '_duplicates_vs_existing.csv') -NoTypeInformation -Encoding UTF8
    $dupRows | Select-Object -First 20 | Format-Table -AutoSize
}
