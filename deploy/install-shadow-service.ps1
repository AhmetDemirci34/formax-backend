<#
    FORMAX — SHADOW WORKER WINDOWS SERVICE KURULUMU

    NE YAPAR: yayımlanmış Formax.API'yi bir Windows Service olarak kaydeder, otomatik
    başlatmaya alır ve çökme hâlinde SCM'in kendiliğinden yeniden başlatmasını yapılandırır.

    NE YAPMAZ: model, kapı, kalibrasyon veya Prediction Contract'a dokunmaz. Kullanıcıya
    tahmin AÇMAZ — gölge satırları ShadowMode=1 ile yazılır ve kullanıcıya açılan hiçbir uç
    Predictions tablosunu okumaz.

    YÖNETİCİ (Administrator) PowerShell gerektirir.

    Örnek:
      .\install-shadow-service.ps1 -AppDir "C:\FORMAX\app" `
                                   -ConnectionString "Server=localhost\SQLEXPRESS;Database=FormaxDB;Trusted_Connection=True;TrustServerCertificate=True" `
                                   -ApiFootballKey "<anahtar>" `
                                   -JwtKey "<anahtar>"
#>
param(
    [Parameter(Mandatory = $true)][string]$AppDir,
    [Parameter(Mandatory = $true)][string]$ConnectionString,
    [Parameter(Mandatory = $true)][string]$ApiFootballKey,
    [Parameter(Mandatory = $true)][string]$JwtKey,
    [string]$ServiceName  = "FormaxShadow",
    [string]$DisplayName  = "FORMAX Shadow Prediction Worker",
    [string]$LlmApiKey    = ""
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Bu betik Yonetici olarak calistirilmalidir."
}

$exe = Join-Path $AppDir "Formax.API.exe"
if (-not (Test-Path $exe)) { throw "Bulunamadi: $exe  (once dotnet publish)" }

# Motor dosyalari AppDir'in BIR USTUNDE olmali (Predictions:EngineRoot bos birakilir).
$engineRoot = Split-Path $AppDir -Parent
foreach ($p in @(
    "FORMAX_HISTORICAL_MASTER\FORMAX_HISTORICAL_MODEL_ELIGIBLE.csv",
    "FORMAX_HISTORICAL_MASTER\FORMAX_HISTORICAL_TEAMS.csv",
    "FORMAX_PROBABILITY_ENGINE\model_validation_v2\validated_teamstrength_config.json",
    "FORMAX_PROBABILITY_ENGINE\model_validation_v2\split.config.json",
    "FORMAX_PROBABILITY_ENGINE\dixon_coles\dixoncoles.config.json",
    "FORMAX_PROBABILITY_ENGINE\prediction_gate_v1\gate.config.json")) {
    $full = Join-Path $engineRoot $p
    if (-not (Test-Path $full)) { throw "Motor dosyasi eksik: $full" }
}
Write-Host "Motor dosyalari tam. EngineRoot = $engineRoot" -ForegroundColor Green

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Mevcut servis durduruluyor ve kaldiriliyor: $ServiceName"
    sc.exe stop   $ServiceName | Out-Null
    Start-Sleep -Seconds 5
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 3
}

sc.exe create $ServiceName binPath= "`"$exe`"" DisplayName= "`"$DisplayName`"" start= auto | Out-Null
sc.exe description $ServiceName "FORMAX gölge tahmin isleyicisi (Prediction Contract V1, shadow mode). Kullaniciya tahmin GOSTERMEZ." | Out-Null

# ── OTOMATIK YENIDEN BASLATMA ────────────────────────────────────────────────
# Cokme sonrasi: 30 sn, 60 sn, sonra her seferinde 120 sn. Sayac 24 saatte sifirlanir.
sc.exe failure $ServiceName reset= 86400 actions= restart/30000/restart/60000/restart/120000 | Out-Null
# Sifir olmayan cikis kodu da "hata" sayilsin (yoksa yalniz cokme yeniden baslatir).
sc.exe failureflag $ServiceName 1 | Out-Null

# ── SIRLAR: registry'deki servis ortam degiskenlerinde, appsettings'te DEGIL ──
$key = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$envVars = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ConnectionStrings__FormaxDB=$ConnectionString",
    "ApiFootball__ApiKey=$ApiFootballKey",
    "Jwt__Key=$JwtKey"
)
if ($LlmApiKey) { $envVars += "Llm__ApiKey=$LlmApiKey" }
New-ItemProperty -Path $key -Name "Environment" -PropertyType MultiString -Value $envVars -Force | Out-Null

Write-Host "Servis kuruldu: $ServiceName" -ForegroundColor Green
Write-Host "Baslatmak icin:  sc.exe start $ServiceName"
Write-Host "Saglik kontrolu: Invoke-RestMethod http://localhost:5063/admin/shadow/health"
Write-Host "Yeniden baslatma politikasi: sc.exe qfailure $ServiceName"
