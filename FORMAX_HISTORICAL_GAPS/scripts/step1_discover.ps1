. "$PSScriptRoot\af_common.ps1"

$st = Invoke-AF -Path 'status' -Query @{} -SaveAs 'status.json'
Write-Host "PLAN: $($st.response.subscription.plan)  ACTIVE: $($st.response.subscription.active)  END: $($st.response.subscription.end)"
Write-Host "REQUESTS today: $($st.response.requests.current) / $($st.response.requests.limit_day)"
Write-Host ""

foreach ($id in 2,3,848) {
    $r = Invoke-AF -Path 'leagues' -Query @{ id = $id } -SaveAs "leagues_$id.json"
    $lg = $r.response[0]
    Write-Host "--- LEAGUE $id : $($lg.league.name) [$($lg.league.type)] country=$($lg.country.name)"
    $seasons = $lg.seasons | Where-Object { $_.year -ge 2016 -and $_.year -le 2025 }
    foreach ($s in $seasons) {
        Write-Host ("    season {0}  {1} .. {2}  fixtures.events={3} stats={4} standings={5}" -f $s.year, $s.start, $s.end, $s.coverage.fixtures.events, $s.coverage.fixtures.statistics_fixtures, $s.coverage.standings)
    }
    Write-Host ""
}
