# Shared helpers for api-football historical gap discovery.
# API key is read from appsettings.json and NEVER printed.

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$script:Root    = 'C:\Users\dikim\Desktop\FORMAX_Backend'
$script:OutDir  = Join-Path $Root 'FORMAX_HISTORICAL_GAPS'
$script:RawDir  = Join-Path $OutDir 'raw'
$script:CallLog = Join-Path $OutDir '_api_call_log.txt'

$cfg = Get-Content (Join-Path $Root 'Formax.API\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$script:ApiKey  = $cfg.ApiFootball.ApiKey
$script:BaseUrl = $cfg.ApiFootball.BaseUrl
if ([string]::IsNullOrWhiteSpace($ApiKey)) { throw 'ApiFootball:ApiKey not found in appsettings.json' }

function Invoke-AF {
    param([string]$Path, [hashtable]$Query, [string]$SaveAs)

    $qs = ($Query.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '&'
    $url = "$BaseUrl/$Path`?$qs"

    # Cache: never re-call the same endpoint+params twice.
    $cacheFile = Join-Path $RawDir $SaveAs
    if (Test-Path $cacheFile) {
        Write-Host "CACHED  $Path`?$qs"
        return (Get-Content $cacheFile -Raw -Encoding UTF8 | ConvertFrom-Json)
    }

    $resp = Invoke-RestMethod -Uri $url -Headers @{ 'x-apisports-key' = $ApiKey } -Method Get -TimeoutSec 60
    $resp | ConvertTo-Json -Depth 30 | Out-File $cacheFile -Encoding utf8
    Add-Content $CallLog "$(Get-Date -Format s)`t$Path`?$qs`tresults=$($resp.results)"
    Write-Host "CALLED  $Path`?$qs  -> results=$($resp.results)"
    if ($resp.errors -and $resp.errors.PSObject.Properties.Count -gt 0) {
        Write-Host "  ERRORS: $($resp.errors | ConvertTo-Json -Compress)"
    }
    Start-Sleep -Milliseconds 400
    return $resp
}
