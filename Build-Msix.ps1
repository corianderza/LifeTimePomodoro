# Build-Msix.ps1 — собирает WPF-проект, упаковывает в MSIX, подписывает и устанавливает.
# Запускать от имени Администратора (нужно для добавления сертификата в TrustedPeople).

$ErrorActionPreference = "Stop"
$Configuration = "Debug"

$projectDir  = $PSScriptRoot
$sdkTools    = "D:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.26100.1742\bin\10.0.26100.0\x64"
$makeappx    = "$sdkTools\makeappx.exe"
$makepri     = "$sdkTools\makepri.exe"
$signtool    = "$sdkTools\signtool.exe"
$stagingDir  = "$projectDir\AppxStaging"
$outDir      = "$projectDir\bin\MsixPackage"
$msixFile    = "$outDir\PomodoroTimer.msix"
$pfxFile     = "$projectDir\PomodoroTimer_TemporaryKey.pfx"
$certThumb   = "D17F2375F6A3FCCEC161D77813FCA801106C30E2"
$buildOutput = "$projectDir\bin\$Configuration\net8.0-windows10.0.17763.0"

# ── 0. Доверяем сертификату (CurrentUser — не требует admin) ──────────────
Write-Host "`n[0/7] Trusting dev certificate..." -ForegroundColor Cyan
$cert = Get-Item "cert:\CurrentUser\My\$certThumb" -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Host "  Certificate not in CurrentUser\My, importing from PFX..."
    certutil -user -f -importpfx "$pfxFile"
    $cert = Get-Item "cert:\CurrentUser\My\$certThumb"
}
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "CurrentUser")
$store.Open("ReadWrite")
$exists = $store.Certificates | Where-Object { $_.Thumbprint -eq $certThumb }
if (-not $exists) {
    $store.Add($cert)
    Write-Host "  Added to CurrentUser\TrustedPeople" -ForegroundColor Green
} else {
    Write-Host "  Already trusted" -ForegroundColor Gray
}
$store.Close()

# ── 1. Build ──────────────────────────────────────────────────────────────
Write-Host "[1/7] Building project..." -ForegroundColor Cyan
Push-Location $projectDir
dotnet build "PomodoroTimer.csproj" -c $Configuration --no-incremental
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Pop-Location

# ── 2. Staging ────────────────────────────────────────────────────────────
Write-Host "[2/7] Staging output files..." -ForegroundColor Cyan
Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item $stagingDir -ItemType Directory | Out-Null
New-Item $outDir -ItemType Directory -Force | Out-Null

Copy-Item "$buildOutput\*" $stagingDir -Recurse -Force
Write-Host "  Staged from: $buildOutput"

# ── 3. AppxManifest.xml ───────────────────────────────────────────────────
Write-Host "[3/7] Preparing AppxManifest.xml..." -ForegroundColor Cyan
$manifest = Get-Content "$projectDir\Package.appxmanifest" -Raw
$manifest = $manifest -replace '\$targetnametoken\$', 'PomodoroTimer'
[System.IO.File]::WriteAllText("$stagingDir\AppxManifest.xml", $manifest, [System.Text.Encoding]::UTF8)

# ── 4. Neutral image copies + resources.pri ──────────────────────────────
Write-Host "[4/7] Generating resources.pri..." -ForegroundColor Cyan
# Windows requires a neutral (unqualified) fallback for each scale-qualified image
Get-ChildItem "$stagingDir\Images" -Filter "*.scale-200.png" -ErrorAction SilentlyContinue | ForEach-Object {
    $plain = $_.FullName -replace '\.scale-200\.png$', '.png'
    Copy-Item $_.FullName $plain -Force
}
$priConfig = "$stagingDir\priconfig.xml"
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o 2>&1 | Out-Null
& $makepri new /pr $stagingDir /cf $priConfig /mn "$stagingDir\AppxManifest.xml" /of "$stagingDir\resources.pri" /o 2>&1 | Out-Null
Write-Host "  resources.pri created" -ForegroundColor Green

# ── 5. Pack ───────────────────────────────────────────────────────────────
Write-Host "[5/7] Packing MSIX..." -ForegroundColor Cyan
& $makeappx pack /d $stagingDir /p $msixFile /o /l
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }
Write-Host "  Created: $msixFile" -ForegroundColor Green

# ── 6. Sign ───────────────────────────────────────────────────────────────
Write-Host "[6/7] Signing..." -ForegroundColor Cyan
& $signtool sign /fd SHA256 /sha1 $certThumb $msixFile
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed" }
Write-Host "  Signed OK" -ForegroundColor Green

# ── 7. Install & Launch ───────────────────────────────────────────────────
Write-Host "[7/7] Installing MSIX..." -ForegroundColor Cyan

$existing = Get-AppxPackage -Name "PomodoroTimer" -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "  Removing old version..."
    Remove-AppxPackage -Package $existing.PackageFullName
}

Add-AppxPackage -Path $msixFile

$pkg = Get-AppxPackage -Name "PomodoroTimer" -ErrorAction SilentlyContinue
if ($pkg) {
    Write-Host "`n  Installed: $($pkg.PackageFamilyName)" -ForegroundColor Green
    Write-Host "  Launching..." -ForegroundColor Yellow
    Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!App"
    Write-Host "`n  Done! App running as MSIX package." -ForegroundColor Green
    Write-Host "  scenario=alarm Toast notifications will now bypass DND.`n" -ForegroundColor Green
} else {
    Write-Warning "Package not found after install. Try running manually from Start Menu."
}
