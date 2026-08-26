. "$PSScriptRoot\af_common.ps1"

# ---- Stage classification. Rules derived ONLY from round strings actually returned by the API.
# Observed qualifier vocabulary: 'Preliminary Round', 'Preliminary round', 'Preliminary round 1|2',
#   'Preliminary Round - Semi-finals', 'Preliminary Round - Final', '1st|2nd|3rd Qualifying Round'
# Observed qualification play-off:  'Play-offs'   (July/August, before the group stage)
# TRAP: 'Knockout Round Play-offs' is the FEBRUARY in-tournament knockout play-off -> MAIN, not qualification.
function Get-Stage {
    param([string]$Round)
    $r = $Round.Trim().ToLowerInvariant()
    if ($r -eq 'knockout round play-offs') { return 'MAIN' }
    if ($r -like '*qualifying round*')     { return 'QUALIFIER' }
    if ($r -like 'preliminary*')           { return 'QUALIFIER' }
    if ($r -eq 'play-offs')                { return 'QUALIFICATION_PLAYOFF' }
    return 'MAIN'
}

$compMeta = @{
    2   = @{ Name = 'UEFA Champions League';          Prefix = 'CHAMPIONS_LEAGUE'  }
    3   = @{ Name = 'UEFA Europa League';             Prefix = 'EUROPA_LEAGUE'     }
    848 = @{ Name = 'UEFA Europa Conference League';  Prefix = 'CONFERENCE_LEAGUE' }
}

$targets = @()
foreach ($s in 2017..2023) { $targets += @{ id = 2;   season = $s } }
foreach ($s in 2017..2023) { $targets += @{ id = 3;   season = $s } }
foreach ($s in 2021..2023) { $targets += @{ id = 848; season = $s } }

$all        = New-Object System.Collections.ArrayList
$unresolved = New-Object System.Collections.ArrayList
$coverage   = New-Object System.Collections.ArrayList

foreach ($t in $targets) {
    $file = Join-Path $RawDir ("fixtures_{0}_{1}.json" -f $t.id, $t.season)
    if (-not (Test-Path $file)) { throw "missing raw file: $file" }
    $resp = (Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json)

    $meta        = $compMeta[$t.id]
    $seasonLabel = '{0}/{1}' -f $t.season, (($t.season + 1) % 100).ToString('00')

    $rows = foreach ($f in $resp.response) {
        $stage = Get-Stage $f.league.round
        $utc   = [datetime]::Parse($f.fixture.date, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        $ctype = if ($stage -eq 'MAIN') { $meta.Prefix } else { "$($meta.Prefix)_$stage" }

        [PSCustomObject][ordered]@{
            FixtureId           = $f.fixture.id
            Season              = $t.season
            SeasonLabel         = $seasonLabel
            Competition         = $meta.Name
            CompetitionId       = $t.id
            CompetitionType     = $ctype
            Stage               = $stage
            Round               = $f.league.round
            Date                = $utc.ToString('yyyy-MM-ddTHH:mm:ssZ')
            MatchDate           = $utc.ToString('yyyy-MM-dd')
            HomeTeam            = $f.teams.home.name
            AwayTeam            = $f.teams.away.name
            HomeTeamId          = $f.teams.home.id
            AwayTeamId          = $f.teams.away.id
            HomeGoals           = $f.goals.home
            AwayGoals           = $f.goals.away
            HomeGoalsHalfTime   = $f.score.halftime.home
            AwayGoalsHalfTime   = $f.score.halftime.away
            HomeGoalsFullTime   = $f.score.fulltime.home
            AwayGoalsFullTime   = $f.score.fulltime.away
            HomeGoalsExtraTime  = $f.score.extratime.home
            AwayGoalsExtraTime  = $f.score.extratime.away
            HomeGoalsPenalty    = $f.score.penalty.home
            AwayGoalsPenalty    = $f.score.penalty.away
            Status              = $f.fixture.status.long
            StatusShort         = $f.fixture.status.short
            Venue               = $f.fixture.venue.name
            City                = $f.fixture.venue.city
            ProviderFixtureId   = $f.fixture.id
            DuplicateKey        = ('{0}|{1}|{2}|{3}|{4}' -f $meta.Name, $seasonLabel, $utc.ToString('yyyy-MM-dd'), $f.teams.home.name, $f.teams.away.name)
        }
    }

    # sanity: a QUALIFICATION_PLAYOFF must fall BEFORE the first main-stage match of that season
    $firstMain = ($rows | Where-Object { $_.Stage -eq 'MAIN' } | Sort-Object Date | Select-Object -First 1).Date
    $badPo = @($rows | Where-Object { $_.Stage -eq 'QUALIFICATION_PLAYOFF' -and $_.Date -ge $firstMain })
    if ($badPo.Count -gt 0) { Write-Host ("  !! {0}/{1}: {2} play-off rows fall AFTER first main-stage match" -f $t.id, $t.season, $badPo.Count) }

    foreach ($r in $rows) { [void]$all.Add($r) }

    foreach ($st in 'QUALIFIER','QUALIFICATION_PLAYOFF','MAIN') {
        $sub = @($rows | Where-Object { $_.Stage -eq $st })
        if ($sub.Count -eq 0) { continue }
        [void]$coverage.Add([PSCustomObject][ordered]@{
            Competition   = $meta.Name
            SeasonLabel   = $seasonLabel
            Stage         = $st
            ApiLeagueId   = $t.id
            ApiSeason     = $t.season
            Rounds        = (($sub | Select-Object -ExpandProperty Round -Unique | Sort-Object) -join ' ; ')
            FixtureCount  = $sub.Count
            Finished      = @($sub | Where-Object { $_.StatusShort -in @('FT','AET','PEN') }).Count
            WithPenalties = @($sub | Where-Object { $null -ne $_.HomeGoalsPenalty }).Count
            WithExtraTime = @($sub | Where-Object { $null -ne $_.HomeGoalsExtraTime }).Count
            FirstDate     = ($sub | Sort-Object Date | Select-Object -First 1).MatchDate
            LastDate      = ($sub | Sort-Object Date | Select-Object -Last 1).MatchDate
        })
    }

    # anything without a usable result or team identity
    foreach ($r in $rows) {
        if ($null -eq $r.HomeGoals -or $null -eq $r.AwayGoals -or $null -eq $r.HomeTeamId -or $null -eq $r.AwayTeamId) {
            [void]$unresolved.Add($r)
        }
    }
}

# ---- internal duplicate check
$dupKeys = $all | Group-Object DuplicateKey | Where-Object { $_.Count -gt 1 }
$dupIds  = $all | Group-Object FixtureId    | Where-Object { $_.Count -gt 1 }
Write-Host ("INTERNAL DUPLICATES: duplicateKey={0} fixtureId={1}" -f $dupKeys.Count, $dupIds.Count)
if ($dupKeys.Count -gt 0) { $dupKeys | ForEach-Object { Write-Host "   DUP: $($_.Name)" } }

# ---- write CSVs
function Write-Csv {
    param($Rows, [string]$Name)
    $path = Join-Path $OutDir $Name
    $arr = @($Rows | Sort-Object Date)
    if ($arr.Count -eq 0) { Write-Host "SKIP (0 rows) $Name"; return }
    $arr | Export-Csv -Path $path -NoTypeInformation -Encoding UTF8
    Write-Host ("WROTE {0,-46} {1,5} rows" -f $Name, $arr.Count)
}

Write-Csv ($all | Where-Object { $_.CompetitionId -eq 2   -and $_.Stage -eq 'QUALIFIER' })             'champions_league_qualifiers.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 2   -and $_.Stage -eq 'QUALIFICATION_PLAYOFF' }) 'champions_league_qualification_playoffs.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 3   -and $_.Stage -eq 'QUALIFIER' })             'europa_league_qualifiers.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 3   -and $_.Stage -eq 'QUALIFICATION_PLAYOFF' }) 'europa_league_qualification_playoffs.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 848 -and $_.Stage -eq 'QUALIFIER' })             'conference_league_qualifiers.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 848 -and $_.Stage -eq 'QUALIFICATION_PLAYOFF' }) 'conference_league_qualification_playoffs.csv'

# Europa League MAIN tournament — the two seasons missing from every existing local source
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 3 -and $_.Season -eq 2017 -and $_.Stage -eq 'MAIN' }) 'europa_league_2017_18.csv'
Write-Csv ($all | Where-Object { $_.CompetitionId -eq 3 -and $_.Season -eq 2018 -and $_.Stage -eq 'MAIN' }) 'europa_league_2018_19.csv'

Write-Csv $unresolved '_unresolved.csv'
$coverage | Sort-Object Competition, SeasonLabel, Stage | Export-Csv -Path (Join-Path $OutDir '_coverage_report.csv') -NoTypeInformation -Encoding UTF8
Write-Host ("WROTE {0,-46} {1,5} rows" -f '_coverage_report.csv', $coverage.Count)

Write-Host ""
Write-Host "UNRESOLVED / NO-RESULT rows: $($unresolved.Count)"
$unresolved | Group-Object StatusShort | ForEach-Object { Write-Host "   status=$($_.Name) n=$($_.Count)" }
