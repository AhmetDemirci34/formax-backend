# FORMAX Historical Master — STAGE 1: parse every in-scope source into one staging table.
# Reads only. Never writes to any source location.
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
$TR = 'S' + [char]0x00FC + 'per Lig'   # avoid non-ASCII literals: PS 5.1 reads .ps1 as ANSI

$SrcDir  = 'C:\Users\dikim\Desktop\FORMAX_HISTORICAL_SOURCE'
$Backend = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$Gaps    = Join-Path $Backend 'FORMAX_HISTORICAL_GAPS'
$Inv     = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\inv'
$Work    = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'
New-Item -ItemType Directory -Force -Path $Work | Out-Null

$rows      = New-Object System.Collections.ArrayList
$srcFiles  = New-Object System.Collections.ArrayList
$parseWarn = New-Object System.Collections.ArrayList

function Add-Row {
    param($SourceKey,$SourceFile,$Competition,$CompetitionType,$Season,$Round,$Date,$Time,$TimeZone,$KickoffUtc,
          $HomeRaw,$AwayRaw,$HG,$AG,$RtH,$RtA,$HtH,$HtA,$EtH,$EtA,$PenH,$PenA,$Status,$ProviderMatchId,$ProviderHomeId,$ProviderAwayId)
    [void]$rows.Add([PSCustomObject][ordered]@{
        SourceKey=$SourceKey; SourceFile=$SourceFile; Competition=$Competition; CompetitionType=$CompetitionType
        Season=$Season; Round=$Round; Date=$Date; KickoffLocalTime=$Time; SourceTimeZone=$TimeZone; KickoffUtc=$KickoffUtc
        HomeRaw=$HomeRaw; AwayRaw=$AwayRaw; HomeGoals=$HG; AwayGoals=$AG
        RegularTimeHomeGoals=$RtH; RegularTimeAwayGoals=$RtA; HalfTimeHomeGoals=$HtH; HalfTimeAwayGoals=$HtA
        ExtraTimeHomeGoals=$EtH; ExtraTimeAwayGoals=$EtA; PenaltyShootoutHome=$PenH; PenaltyShootoutAway=$PenA
        MatchStatus=$Status; ProviderMatchId=$ProviderMatchId; ProviderHomeTeamId=$ProviderHomeId; ProviderAwayTeamId=$ProviderAwayId
    })
}
function Note-Src { param($Path,$Kind,$Count)
    [void]$srcFiles.Add([PSCustomObject]@{ File=$Path; Kind=$Kind; RowsParsed=$Count })
}
function Warn-Parse { param($File,$Line,$Why)
    [void]$parseWarn.Add([PSCustomObject]@{ File=$File; Line=$Line; Reason=$Why })
}

# Season label from a date, using the July-1 football season boundary (all 17 FORMAX comps are Jul->May).
function Season-FromDate { param([string]$d)
    $dt = [datetime]::ParseExact($d,'yyyy-MM-dd',$CI)
    $y = if ($dt.Month -ge 7) { $dt.Year } else { $dt.Year - 1 }
    return ('{0}/{1}' -f $y, (($y+1) % 100).ToString('00'))
}
function Season-FromDir { param([string]$s)   # "2017-18" -> "2017/18"
    if ($s -match '^(\d{4})-(\d{2})$') { return "$($matches[1])/$($matches[2])" }
    throw "unrecognised season dir: $s"
}

# =====================================================================================
# 1) football-data.co.uk  (Data/Historical/Matches.csv)  -- 8 domestic leagues
# =====================================================================================
$divMap = @{ 'E0'='Premier League'; 'E1'='Championship'; 'SP1'='La Liga'; 'I1'='Serie A'
             'D1'='Bundesliga'; 'F1'='Ligue 1'; 'T1'=$TR; 'N1'='Eredivisie' }
$fdPath = Join-Path $Backend 'Data\Historical\Matches.csv'
$n = 0
$sr = New-Object IO.StreamReader($fdPath, [Text.Encoding]::UTF8)
[void]$sr.ReadLine()   # header
while (($line = $sr.ReadLine()) -ne $null) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $c = $line.Split(',')
    $div = $c[0]
    if (-not $divMap.ContainsKey($div)) { continue }
    $d = $c[1]
    if ($d -lt '2017-07-01') { continue }
    function Num { param($v) if ([string]::IsNullOrWhiteSpace($v)) { $null } else { [int][double]::Parse($v,$CI) } }
    $hg = Num $c[11]; $ag = Num $c[12]
    $status = if ($null -ne $hg -and $null -ne $ag) { 'FT' } else { 'UNKNOWN' }
    Add-Row 'football-data' 'Data/Historical/Matches.csv' $divMap[$div] 'DOMESTIC_LEAGUE' (Season-FromDate $d) $null `
            $d $c[2] 'Europe/London (football-data.co.uk local)' $null $c[3] $c[4] $hg $ag $hg $ag (Num $c[14]) (Num $c[15]) `
            $null $null $null $null $status $null $null $null
    $n++
}
$sr.Close()
Note-Src 'Data/Historical/Matches.csv' 'football-data csv' $n
Write-Host "football-data           : $n rows"

# =====================================================================================
# 2) openfootball JSON (football.json-master) -- 8 domestic leagues + uefa.cl
# =====================================================================================
$ofjRoot = Join-Path $Inv 'football.json-master\football.json-master'
$ofjLeague = @{ 'en.1'='Premier League'; 'en.2'='Championship'; 'es.1'='La Liga'; 'it.1'='Serie A'
                'de.1'='Bundesliga'; 'fr.1'='Ligue 1'; 'nl.1'='Eredivisie'; 'tr.1'=$TR
                'uefa.cl'='UEFA Champions League' }
$seasonDirs = 2017..2025 | ForEach-Object { '{0}-{1}' -f $_, (($_+1) % 100).ToString('00') }
$n = 0
foreach ($sd in $seasonDirs) {
    $dir = Join-Path $ofjRoot $sd
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in Get-ChildItem $dir -Filter *.json) {
        $key = $f.BaseName
        if (-not $ofjLeague.ContainsKey($key)) { continue }
        $comp = $ofjLeague[$key]
        $isUefa = $comp -like 'UEFA*'
        $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $c = 0
        foreach ($m in $j.matches) {
            # openfootball json: ft = 90 minutes, et = after extra time, p = penalty shootout
            $ft = $null; $ht = $null; $et = $null; $ps = $null
            if ($null -ne $m.score) {
                if ($m.score -is [Array]) { $ft = $m.score }
                else { $ft = $m.score.ft; $ht = $m.score.ht; $et = $m.score.et; $ps = $m.score.p }
            }
            $hg = if ($et) { [int]$et[0] } elseif ($ft) { [int]$ft[0] } else { $null }
            $ag = if ($et) { [int]$et[1] } elseif ($ft) { [int]$ft[1] } else { $null }
            # openfootball domestic play-off rounds are flagged, not dropped
            $ctype = if ($isUefa) { 'UEFA_MAIN' } else { 'DOMESTIC_LEAGUE' }
            if (-not $isUefa -and $m.round -match '(?i)play.?offs?') { $ctype = 'DOMESTIC_PLAYOFF' }
            $status = if ($ps) { 'PEN' } elseif ($et) { 'AET' } elseif ($null -ne $hg) { 'FT' } else { 'UNKNOWN' }
            $etH = $null; $etA = $null
            if ($et -and $ft) { $etH = [int]$et[0] - [int]$ft[0]; $etA = [int]$et[1] - [int]$ft[1] }
            Add-Row 'openfootball-json' ("football.json-master/$sd/$($f.Name)") $comp $ctype (Season-FromDir $sd) $m.round `
                    $m.date $m.time 'source local time (unspecified)' $null $m.team1 $m.team2 $hg $ag `
                    $(if ($ft) { [int]$ft[0] } else { $null }) $(if ($ft) { [int]$ft[1] } else { $null }) `
                    $(if ($ht) { [int]$ht[0] } else { $null }) $(if ($ht) { [int]$ht[1] } else { $null }) `
                    $etH $etA $(if ($ps) { [int]$ps[0] } else { $null }) $(if ($ps) { [int]$ps[1] } else { $null }) `
                    $status $null $null $null
            $c++; $n++
        }
        Note-Src "football.json-master/$sd/$($f.Name)" 'openfootball json' $c
    }
}
Write-Host "openfootball-json       : $n rows"

# =====================================================================================
# 3) openfootball TXT — shared parser
# =====================================================================================
function Parse-OfTxt {
    param([string]$Path,[string]$SourceFileLabel)
    $out = New-Object System.Collections.ArrayList
    $year = $null; $curDate = $null; $section = $null; $ln = 0
    $secRe = '^\s*[' + [char]0x25AA + [char]0x00BB + ']\s*(.+?)\s*$'
    foreach ($line in (Get-Content $Path -Encoding UTF8)) {
        $ln++
        if ($line -match $secRe) { $section = $matches[1]; continue }
        if ($line -match '^\s*(Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+([A-Z][a-z]{2})\s+(\d{1,2})(\s+(\d{4}))?\s*$') {
            if ($matches[5]) { $year = $matches[5] }
            $curDate = [datetime]::ParseExact("$($matches[3]) $($matches[2]) $year",'d MMM yyyy',$CI).ToString('yyyy-MM-dd')
            continue
        }
        if ($line -match '^\s+((\d{2}:\d{2})\s+)?(.+?)\s+v\s+(.+?)\s{2,}(\S.*?)\s*$') {
            if (-not $curDate) { Warn-Parse $SourceFileLabel $ln 'match line before any date line'; continue }
            $time = $matches[2]; $hName = $matches[3].Trim(); $aName = $matches[4].Trim(); $sc = $matches[5].Trim()
            $pen=$null; $aet=$null; $ft=$null; $ht=$null; $special=$null
            if     ($sc -match '^\[(cancelled|postponed|abandoned|awarded)\]') { $special = $matches[1].ToUpperInvariant() }
            elseif ($sc -match '^(\d+)-(\d+)\s+\[(awarded|cancelled|postponed|abandoned)\]') {
                $ft=@([int]$matches[1],[int]$matches[2]); $special = $matches[3].ToUpperInvariant()
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+pen\.\s+(\d+)-(\d+)\s+a\.e\.t\.\s+\((\d+)-(\d+)\)$') {
                $pen=@([int]$matches[1],[int]$matches[2]); $aet=@([int]$matches[3],[int]$matches[4]); $ft=@([int]$matches[5],[int]$matches[6])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+pen\.\s+(\d+)-(\d+)\s+a\.e\.t\.\s+\((\d+)-(\d+),\s*(\d+)-(\d+)\)') {
                $pen=@([int]$matches[1],[int]$matches[2]); $aet=@([int]$matches[3],[int]$matches[4])
                $ft=@([int]$matches[5],[int]$matches[6]);  $ht=@([int]$matches[7],[int]$matches[8])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+pen\.\s+\((\d+)-(\d+),\s*(\d+)-(\d+)\)') {
                $pen=@([int]$matches[1],[int]$matches[2]); $ft=@([int]$matches[3],[int]$matches[4]); $ht=@([int]$matches[5],[int]$matches[6])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+pen\.\s+(\d+)-(\d+)\s+a\.e\.t\.$') {
                $pen=@([int]$matches[1],[int]$matches[2]); $aet=@([int]$matches[3],[int]$matches[4])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+a\.e\.t\.\s+\((\d+)-(\d+),\s*(\d+)-(\d+)\)') {
                $aet=@([int]$matches[1],[int]$matches[2]); $ft=@([int]$matches[3],[int]$matches[4]); $ht=@([int]$matches[5],[int]$matches[6])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+a\.e\.t\.\s*\((\d+)-(\d+)\)') {
                $aet=@([int]$matches[1],[int]$matches[2]); $ft=@([int]$matches[3],[int]$matches[4])
            }
            elseif ($sc -match '^(\d+)-(\d+)\s+\((\d+)-(\d+)\)$') { $ft=@([int]$matches[1],[int]$matches[2]); $ht=@([int]$matches[3],[int]$matches[4]) }
            elseif ($sc -match '^(\d+)-(\d+)$')                   { $ft=@([int]$matches[1],[int]$matches[2]) }
            else { Warn-Parse $SourceFileLabel $ln "unparsed score '$sc'  ($hName v $aName)"; continue }

            $hg = if ($aet) { $aet[0] } elseif ($ft) { $ft[0] } else { $null }
            $ag = if ($aet) { $aet[1] } elseif ($ft) { $ft[1] } else { $null }
            $etH = $null; $etA = $null
            if ($aet -and $ft) { $etH = $aet[0]-$ft[0]; $etA = $aet[1]-$ft[1] }
            $status = if ($special) { $special } elseif ($pen) { 'PEN' } elseif ($aet) { 'AET' } elseif ($null -ne $hg) { 'FT' } else { 'UNKNOWN' }
            [void]$out.Add([PSCustomObject]@{
                Section=$section; Date=$curDate; Time=$time; Home=$hName; Away=$aName
                HG=$hg; AG=$ag; RtH=$(if($ft){$ft[0]}else{$null}); RtA=$(if($ft){$ft[1]}else{$null})
                HtH=$(if($ht){$ht[0]}else{$null}); HtA=$(if($ht){$ht[1]}else{$null})
                EtH=$etH; EtA=$etA; PenH=$(if($pen){$pen[0]}else{$null}); PenA=$(if($pen){$pen[1]}else{$null}); Status=$status })
        }
    }
    return $out
}

# 3a) europe-master (only FORMAX domestic leagues that exist there: fr1, tr1, nl1)
$euRoot = Join-Path $Inv 'europe-master\europe-master'
$euMap = @{ 'fr1'='Ligue 1'; 'tr1'=$TR; 'nl1'='Eredivisie' }
$n = 0
foreach ($f in Get-ChildItem $euRoot -Recurse -Filter *.txt) {
    if ($f.BaseName -notmatch '^(\d{4}-\d{2})_(\w+?)(-full)?$') { continue }
    $sd = $matches[1]; $lg = $matches[2]
    if (-not $euMap.ContainsKey($lg)) { continue }
    if ($sd -lt '2017-18' -or $sd -gt '2025-26') { continue }
    if ($f.BaseName -like '*-full') { continue }          # -full is a duplicate view of the same season
    $lab = "europe-master/$($f.Directory.Name)/$($f.Name)"
    $p = Parse-OfTxt $f.FullName $lab
    foreach ($m in $p) {
        Add-Row 'openfootball-txt' $lab $euMap[$lg] 'DOMESTIC_LEAGUE' (Season-FromDir $sd) $m.Section `
                $m.Date $m.Time 'source local time (unspecified)' $null $m.Home $m.Away $m.HG $m.AG $m.RtH $m.RtA `
                $m.HtH $m.HtA $m.EtH $m.EtA $m.PenH $m.PenA $m.Status $null $null $null
        $n++
    }
    Note-Src $lab 'openfootball txt' $p.Count
}
Write-Host "openfootball-txt (dom)  : $n rows"

# 3b) champions-league-master (UEFA)
$clRoot = Join-Path $Inv 'champions-league-master\champions-league-master'
$uefaComp = @{ 'cl'='UEFA Champions League'; 'clq'='UEFA Champions League'
               'el'='UEFA Europa League';    'elq'='UEFA Europa League'
               'conf'='UEFA Conference League'; 'confq'='UEFA Conference League' }
$n = 0
foreach ($f in Get-ChildItem $clRoot -Recurse -Filter *.txt) {
    $key = $f.BaseName
    if (-not $uefaComp.ContainsKey($key)) { continue }
    $sd = $f.Directory.Name
    if ($sd -lt '2017-18' -or $sd -gt '2025-26') { continue }
    $isQualFile = $key.EndsWith('q')
    $lab = "champions-league-master/$sd/$($f.Name)"
    $p = Parse-OfTxt $f.FullName $lab
    foreach ($m in $p) {
        # File-level stage: cl/el/conf files contain ONLY the main tournament (their 'Playoffs'
        # section is the FEBRUARY knockout play-off). clq/elq/confq contain ONLY the qualifying
        # phase, whose 'Playoffs' section IS the August qualification play-off.
        $ctype = if (-not $isQualFile) { 'UEFA_MAIN' }
                 elseif ($m.Section -match '(?i)play.?offs?') { 'UEFA_QUALIFICATION_PLAYOFF' }
                 else { 'UEFA_QUALIFIER' }
        Add-Row 'openfootball-txt' $lab $uefaComp[$key] $ctype (Season-FromDir $sd) $m.Section `
                $m.Date $m.Time 'source local time (unspecified)' $null $m.Home $m.Away $m.HG $m.AG $m.RtH $m.RtA `
                $m.HtH $m.HtA $m.EtH $m.EtA $m.PenH $m.PenA $m.Status $null $null $null
        $n++
    }
    Note-Src $lab 'openfootball txt' $p.Count
}
Write-Host "openfootball-txt (uefa) : $n rows"

# =====================================================================================
# 4) openfootball CSV exports (eng.2 / nl.1 / tr.1  = 2017/18)
# =====================================================================================
$ofCsv = @{ 'eng.2.csv'=@('Championship','2017/18'); 'nl.1.csv'=@('Eredivisie','2017/18'); 'tr.1.csv'=@($TR,'2017/18') }
$n = 0
foreach ($k in $ofCsv.Keys) {
    $p = Join-Path $SrcDir $k
    if (-not (Test-Path $p)) { continue }
    $c = 0
    foreach ($r in (Import-Csv $p)) {
        $d = [datetime]::ParseExact($r.Date,'ddd MMM d yyyy',$CI).ToString('yyyy-MM-dd')
        $ft = $r.FT -split '-'; $ht = if ($r.HT) { $r.HT -split '-' } else { $null }
        Add-Row 'openfootball-csv' $k $ofCsv[$k][0] 'DOMESTIC_LEAGUE' $ofCsv[$k][1] $null `
                $d $null 'source local time (unspecified)' $null $r.'Team 1' $r.'Team 2' ([int]$ft[0]) ([int]$ft[1]) ([int]$ft[0]) ([int]$ft[1]) `
                $(if($ht){[int]$ht[0]}else{$null}) $(if($ht){[int]$ht[1]}else{$null}) $null $null $null $null 'FT' $null $null $null
        $c++; $n++
    }
    Note-Src $k 'openfootball csv' $c
}
Write-Host "openfootball-csv        : $n rows"

# =====================================================================================
# 5) fixturedownload CSVs
# =====================================================================================
# label -> comp, season, competitiontype, timezone
$fdl = @{
 'champions-league-2017-CentralEuropeanStandardTime.csv' = @('UEFA Champions League','2017/18','UEFA_MAIN','Central European Standard Time')
 'champions-league-2025-UTC.csv'                         = @('UEFA Champions League','2025/26','UEFA_MAIN','UTC')
 'europa-league-2019-WEuropeStandardTime.csv'            = @('UEFA Europa League','2019/20','UEFA_MAIN','W. Europe Standard Time')
 'europa-league-2025-UTC.csv'                            = @('UEFA Europa League','2025/26','UEFA_MAIN','UTC')
 'conference-league-2025-UTC.csv'                        = @('UEFA Conference League','2025/26','UEFA_MAIN','UTC')
 'super-lig-2021-TurkeyStandardTime.csv'                 = @($TR,'2021/22','DOMESTIC_LEAGUE','Turkey Standard Time')
 'super-lig-2022-TurkeyStandardTime.csv'                 = @($TR,'2022/23','DOMESTIC_LEAGUE','Turkey Standard Time')
}
$n = 0
foreach ($k in $fdl.Keys) {
    $p = Join-Path $SrcDir $k
    if (-not (Test-Path $p)) { Write-Host "  MISSING $k"; continue }
    $meta = $fdl[$k]; $c = 0
    foreach ($r in (Import-Csv $p)) {
        $dt = [datetime]::ParseExact($r.Date,'dd/MM/yyyy HH:mm',$CI)
        $hg=$null; $ag=$null; $st='UNKNOWN'
        if ($r.Result -match '^\s*(\d+)\s*-\s*(\d+)\s*$') { $hg=[int]$matches[1]; $ag=[int]$matches[2]; $st='FT' }
        $utc = if ($meta[3] -eq 'UTC') { $dt.ToString('yyyy-MM-ddTHH:mm:ssZ') } else { $null }
        Add-Row 'fixturedownload' $k $meta[0] $meta[2] $meta[1] $r.'Round Number' `
                $dt.ToString('yyyy-MM-dd') $dt.ToString('HH:mm') $meta[3] $utc $r.'Home Team' $r.'Away Team' `
                $hg $ag $hg $ag $null $null $null $null $null $null $st $null $null $null
        $c++; $n++
    }
    Note-Src $k 'fixturedownload csv' $c
}
Write-Host "fixturedownload         : $n rows"

# =====================================================================================
# 6) api-football raw JSON (FORMAX_HISTORICAL_GAPS/raw) — no new API calls
# =====================================================================================
$afComp = @{ '2'='UEFA Champions League'; '3'='UEFA Europa League'; '848'='UEFA Conference League' }
function AF-Stage { param([string]$Round)
    $r = $Round.Trim().ToLowerInvariant()
    if ($r -eq 'knockout round play-offs') { return 'UEFA_MAIN' }
    if ($r -like '*qualifying round*')     { return 'UEFA_QUALIFIER' }
    if ($r -like 'preliminary*')           { return 'UEFA_QUALIFIER' }
    if ($r -eq 'play-offs')                { return 'UEFA_QUALIFICATION_PLAYOFF' }
    return 'UEFA_MAIN'
}
$n = 0
foreach ($f in Get-ChildItem (Join-Path $Gaps 'raw') -Filter 'fixtures_*.json') {
    if ($f.BaseName -notmatch '^fixtures_(\d+)_(\d{4})$') { continue }
    $lid = $matches[1]; $sy = [int]$matches[2]
    $season = '{0}/{1}' -f $sy, (($sy+1) % 100).ToString('00')
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $c = 0
    foreach ($m in $j.response) {
        $utc = [datetime]::Parse($m.fixture.date,$CI,[Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        Add-Row 'api-football' ("FORMAX_HISTORICAL_GAPS/raw/$($f.Name)") $afComp[$lid] (AF-Stage $m.league.round) $season $m.league.round `
                $utc.ToString('yyyy-MM-dd') $utc.ToString('HH:mm') 'UTC' $utc.ToString('yyyy-MM-ddTHH:mm:ssZ') `
                $m.teams.home.name $m.teams.away.name $m.goals.home $m.goals.away `
                $m.score.fulltime.home $m.score.fulltime.away $m.score.halftime.home $m.score.halftime.away `
                $m.score.extratime.home $m.score.extratime.away $m.score.penalty.home $m.score.penalty.away `
                $m.fixture.status.short $m.fixture.id $m.teams.home.id $m.teams.away.id
        $c++; $n++
    }
    Note-Src "FORMAX_HISTORICAL_GAPS/raw/$($f.Name)" 'api-football json' $c
}
Write-Host "api-football            : $n rows"

# =====================================================================================
# 7) SEASON CORRECTION for sources that do not declare a season (football-data.co.uk).
#    The Jul-1 rule breaks for the COVID seasons that finished in Jul/Aug 2020, so the real
#    season windows are taken from the sources that DO declare their season (openfootball-json),
#    and football-data dates are assigned by containment. No guessing, no invented seasons.
# =====================================================================================
$ranges = @{}
foreach ($r in $rows) {
    if ($r.SourceKey -ne 'openfootball-json') { continue }
    if ([string]::IsNullOrWhiteSpace($r.Date)) { continue }
    $k = "$($r.Competition)|$($r.Season)"
    if (-not $ranges.ContainsKey($k)) { $ranges[$k] = [PSCustomObject]@{ Min=$r.Date; Max=$r.Date } }
    if ($r.Date -lt $ranges[$k].Min) { $ranges[$k].Min = $r.Date }
    if ($r.Date -gt $ranges[$k].Max) { $ranges[$k].Max = $r.Date }
}
$rangeByComp = @{}
foreach ($k in $ranges.Keys) {
    $p = $k.Split('|')
    if (-not $rangeByComp.ContainsKey($p[0])) { $rangeByComp[$p[0]] = New-Object System.Collections.ArrayList }
    [void]$rangeByComp[$p[0]].Add([PSCustomObject]@{ Season=$p[1]; Min=$ranges[$k].Min; Max=$ranges[$k].Max })
}
foreach ($c in @($rangeByComp.Keys)) { $rangeByComp[$c] = @($rangeByComp[$c] | Sort-Object Min) }

$fixed = 0; $fixLog = New-Object System.Collections.ArrayList
foreach ($r in $rows) {
    if ($r.SourceKey -ne 'football-data') { continue }
    $hit = $null
    $list = $rangeByComp[$r.Competition]
    if ($list) {
        foreach ($rg in $list) { if ($r.Date -ge $rg.Min -and $r.Date -le $rg.Max) { $hit = $rg.Season; break } }
        if (-not $hit) {
            # date falls in the gap between two declared seasons -> attach to the nearer season boundary
            $d    = [datetime]::ParseExact($r.Date,'yyyy-MM-dd',$CI)
            $prev = $list | Where-Object { $_.Max -lt $r.Date } | Select-Object -Last 1
            $next = $list | Where-Object { $_.Min -gt $r.Date } | Select-Object -First 1
            # Only when BOTH neighbouring seasons are declared and the gap between them is a real
            # close-season (<=120 days). Otherwise the source simply does not cover this season and
            # the Jul-1 rule is kept -- no guessing across missing seasons.
            if ($prev -and $next) {
                $pMax = [datetime]::ParseExact($prev.Max,'yyyy-MM-dd',$CI)
                $nMin = [datetime]::ParseExact($next.Min,'yyyy-MM-dd',$CI)
                if (($nMin - $pMax).TotalDays -le 120) {
                    $dp = ($d - $pMax).TotalDays; $dn = ($nMin - $d).TotalDays
                    $hit = if ($dp -le $dn) { $prev.Season } else { $next.Season }
                }
            }
        }
    }
    if ($hit -and $hit -ne $r.Season) {
        [void]$fixLog.Add([PSCustomObject]@{ Competition=$r.Competition; Date=$r.Date; From=$r.Season; To=$hit
                                             Home=$r.HomeRaw; Away=$r.AwayRaw
                                             Evidence="openfootball-json declares $($r.Competition) $hit as $($ranges["$($r.Competition)|$hit"].Min)..$($ranges["$($r.Competition)|$hit"].Max)" })
        $r.Season = $hit; $fixed++
    }
}
$fixLog | Export-Csv (Join-Path $Work 'season_corrections.csv') -NoTypeInformation -Encoding UTF8
Write-Host "season corrections (football-data, evidence-based): $fixed"

# =====================================================================================
# 8) LEAGUE vs PROMOTION/EUROPEAN PLAY-OFF SEPARATION.
#    All eight FORMAX domestic leagues are pure double round-robins: an ordered pair
#    (home,away) can occur at most ONCE per season. A repeat of the same ordered pair is
#    therefore not a league fixture but a play-off leg (England promotion play-offs,
#    Eredivisie European play-offs). Date order decides which one is the league fixture.
# =====================================================================================
$seen = @{}
$poFound = 0
foreach ($r in ($rows | Where-Object { $_.CompetitionType -eq 'DOMESTIC_LEAGUE' } | Sort-Object Date)) {
    $k = '{0}|{1}|{2}|{3}|{4}' -f $r.SourceKey,$r.Competition,$r.Season,$r.HomeRaw,$r.AwayRaw
    if ($seen.ContainsKey($k)) { $r.CompetitionType = 'DOMESTIC_PLAYOFF'; $poFound++ }
    else { $seen[$k] = 1 }
}
Write-Host "league rows re-classified as DOMESTIC_PLAYOFF (repeated ordered pair): $poFound"

$rows      | Export-Csv (Join-Path $Work 'staging.csv')      -NoTypeInformation -Encoding UTF8
$srcFiles  | Export-Csv (Join-Path $Work 'source_files.csv') -NoTypeInformation -Encoding UTF8
$parseWarn | Export-Csv (Join-Path $Work 'parse_warnings.csv') -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "TOTAL staged rows : $($rows.Count)   source files: $($srcFiles.Count)   parse warnings: $($parseWarn.Count)"
$rows | Group-Object SourceKey | Sort-Object Name | ForEach-Object { Write-Host ("   {0,-20} {1,6}" -f $_.Name, $_.Count) }
if ($parseWarn.Count -gt 0) { $parseWarn | Select-Object -First 15 | Format-Table -AutoSize }
