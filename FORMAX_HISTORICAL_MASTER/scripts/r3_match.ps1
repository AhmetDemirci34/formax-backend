# Look-alike identity resolution - STEP 2: map every side to a provider team id, then list the
# provider ids whose identity detail (country / founded / venue) is still missing.
$ErrorActionPreference = 'Stop'
$CI = [Globalization.CultureInfo]::InvariantCulture
$Out  = 'C:\Users\dikim\Desktop\FORMAX_Backend\FORMAX_HISTORICAL_MASTER'
$Raw  = Join-Path $Out 'api_raw'
$Work = 'C:\Users\dikim\AppData\Local\Temp\claude\C--Users-dikim-Desktop-FORMAX-Backend\3ddccf74-70ba-4d79-a1b0-2b9119a9567a\scratchpad\master_work'

$noise = @('fc','cf','sc','ac','fk','sk','ks','kf','nk','bk','sv','tsv','pfc','cs','ss','ue','us','as','ca','if','ik','ff','club','de','the','afc','ssc','ogc','rc','sco','hsc','fco','sp','umf','jk','mfk','msk','gnk','hnk','fcb','kf')
function Norm-Tokens { param([string]$s)
    if ([string]::IsNullOrWhiteSpace($s)) { return @() }
    $s = [regex]::Replace($s, '\s*\([A-Za-z]{3}\)\s*', ' ')
    $d = $s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
         Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark }
    $s = [regex]::Replace(((-join $d).ToLowerInvariant()), '[^a-z0-9]', ' ')
    return @($s -split '\s+' | Where-Object { $_ -and $_.Length -ge 2 -and ($noise -notcontains $_) })
}
function Norm-Key { param([string]$s) ((Norm-Tokens $s) -join ' ') }

# --- provider team catalogue from the cached /teams pulls --------------------------------------
$catalog = @{}          # "leagueId|season" -> list of team records
$byProvId = @{}         # provider id -> record
foreach ($f in (Get-ChildItem $Raw -Filter 'teams_*.json')) {
    if ($f.BaseName -notmatch '^teams_(\d+)_(\d{4})$') { continue }
    $key = "$($matches[1])|$($matches[2])"
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $list = New-Object System.Collections.ArrayList
    foreach ($t in $j.response) {
        $rec = [PSCustomObject]@{ Id=$t.team.id; Name=$t.team.name; Code=$t.team.code; Country=$t.team.country
                                  Founded=$t.team.founded; Venue=$t.venue.name; City=$t.venue.city
                                  Tokens=@(Norm-Tokens $t.team.name); Key=(Norm-Key $t.team.name) }
        [void]$list.Add($rec)
        if (-not $byProvId.ContainsKey("$($t.team.id)")) { $byProvId["$($t.team.id)"] = $rec }
    }
    $catalog[$key] = $list
}
Write-Host ("catalogued team lists: {0}   distinct provider teams: {1}" -f $catalog.Count, $byProvId.Count)

# --- match each side that lacks a provider id ---------------------------------------------------
$sides = Import-Csv (Join-Path $Work 'lookalike_side_need.csv')
$resolved = New-Object System.Collections.ArrayList
foreach ($s in $sides) {
    $key = "$($s.LeagueId)|$($s.SeasonYear)"
    $list = $catalog[$key]
    if (-not $list) { [void]$resolved.Add([PSCustomObject]@{ No=$s.No; Side=$s.Side; Id=$s.Id; Name=$s.Name
                                                             ProviderId=''; ProviderName=''; Country=''; Founded=''; Venue=''
                                                             Method='NO_TEAM_LIST'; Note="no cached list for $key" }); continue }
    $aliases = @(($s.Aliases -split ' \| ') + $s.Name | Where-Object { $_ } | Sort-Object -Unique)
    $hit = $null; $method = $null
    # 1) exact normalised name match against the season squad list
    foreach ($a in $aliases) {
        $k = Norm-Key $a
        $m = @($list | Where-Object { $_.Key -eq $k })
        if ($m.Count -eq 1) { $hit = $m[0]; $method = "exact normalised name ('$a')"; break }
    }
    # 2) token containment, must be unique inside that league-season
    if (-not $hit) {
        $best = $null; $bestScore = 0.0; $second = 0.0
        foreach ($t in $list) {
            $sc = 0.0
            foreach ($a in $aliases) {
                $at = @(Norm-Tokens $a); if ($at.Count -eq 0 -or $t.Tokens.Count -eq 0) { continue }
                $inter = 0
                foreach ($x in $at) { foreach ($y in $t.Tokens) {
                    if ($x -eq $y) { $inter++; break }
                    if ($x.Length -ge 4 -and $y.Length -ge 4 -and ($x.StartsWith($y) -or $y.StartsWith($x))) { $inter++; break }
                } }
                $v = $inter / [Math]::Min($at.Count, $t.Tokens.Count)
                if ($v -gt $sc) { $sc = $v }
            }
            if ($sc -gt $bestScore) { $second = $bestScore; $bestScore = $sc; $best = $t } elseif ($sc -gt $second) { $second = $sc }
        }
        if ($best -and $bestScore -ge 1.0 -and $bestScore -gt $second) { $hit = $best; $method = 'token containment, unique in league-season' }
        elseif ($best -and $bestScore -ge 0.6 -and $bestScore -gt $second) { $hit = $best; $method = "token overlap $([Math]::Round($bestScore,2)), unique in league-season" }
    }
    [void]$resolved.Add([PSCustomObject]@{
        No=$s.No; Side=$s.Side; Id=$s.Id; Name=$s.Name
        ProviderId=$(if ($hit) { $hit.Id } else { '' }); ProviderName=$(if ($hit) { $hit.Name } else { '' })
        Country=$(if ($hit) { $hit.Country } else { '' }); Founded=$(if ($hit) { $hit.Founded } else { '' })
        Venue=$(if ($hit) { $hit.Venue } else { '' })
        Method=$(if ($method) { $method } else { 'NOT_FOUND' })
        Note="searched $($s.Competition) $($s.Season) (league $($s.LeagueId), season $($s.SeasonYear))" })
}
$resolved | Export-Csv (Join-Path $Work 'lookalike_side_resolved.csv') -NoTypeInformation -Encoding UTF8
Write-Host ("sides resolved: {0} / {1}" -f @($resolved | Where-Object { $_.ProviderId }).Count, $resolved.Count)
$resolved | Where-Object { -not $_.ProviderId } | Format-Table No,Side,Name,Method,Note -AutoSize

# --- which provider ids still lack identity detail? ----------------------------------------------
$ctx = Import-Csv (Join-Path $Work 'lookalike_context.csv')
$needDetail = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($r in $ctx) {
    foreach ($side in @('A','B')) {
        $p = $r."Prov$side"
        if (-not $p) { continue }
        foreach ($one in ($p -split ',')) { if ($one -and -not $byProvId.ContainsKey($one)) { [void]$needDetail.Add($one) } }
    }
}
Write-Host ""
Write-Host ("provider ids still needing /teams?id= detail: {0}" -f $needDetail.Count)
($needDetail | Sort-Object) -join ',' | Write-Host
$needDetail | Sort-Object | ForEach-Object { [PSCustomObject]@{ ProviderId=$_ } } |
    Export-Csv (Join-Path $Work 'lookalike_need_detail.csv') -NoTypeInformation -Encoding UTF8
