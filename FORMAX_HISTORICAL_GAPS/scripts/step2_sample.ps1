. "$PSScriptRoot\af_common.ps1"

# 2a) Is there a SEPARATE league id for qualifying/play-off? Discover, do not assume.
$q = Invoke-AF -Path 'leagues' -Query @{ search = 'Qualif' } -SaveAs 'leagues_search_qualif.json'
Write-Host "leagues?search=Qualif -> $($q.results) result(s)"
$q.response | ForEach-Object { Write-Host ("    id={0} name='{1}' type={2} country={3}" -f $_.league.id, $_.league.name, $_.league.type, $_.country.name) }
Write-Host ""

# 2b) Sample tests demanded by the task: CL 2017, EL 2017, Conf 2021
foreach ($t in @(@{id=2;season=2017;label='Champions League 2017/18'}, @{id=3;season=2017;label='Europa League 2017/18'}, @{id=848;season=2021;label='Conference League 2021/22'})) {
    $r = Invoke-AF -Path 'fixtures' -Query @{ league = $t.id; season = $t.season } -SaveAs ("fixtures_{0}_{1}.json" -f $t.id, $t.season)
    Write-Host "=== $($t.label)  league=$($t.id) season=$($t.season)  fixtures=$($r.results)"
    $r.response | Group-Object { $_.league.round } | Sort-Object { ($_.Group[0].fixture.date) } | ForEach-Object {
        $first = ($_.Group | Sort-Object { $_.fixture.date })[0]
        Write-Host ("    round='{0}'  n={1}  first={2}" -f $_.Name, $_.Count, $first.fixture.date.Substring(0,10))
    }
    Write-Host ""
}
