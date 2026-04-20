# Uninstall.ps1 — полностью удаляет PomodoroTimer из системы:
#   • MSIX-пакет
#   • Dev-сертификат (CurrentUser\TrustedPeople и CurrentUser\My)
#   • Данные приложения (%LOCALAPPDATA%\Packages\...)
#   • Toast-уведомления из центра уведомлений
#   • Записи реестра (настройки, MRU и т.п.)

$ErrorActionPreference = "Stop"

# ── Автоповышение прав администратора ────────────────────────────────────
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]"Administrator")) {
    Write-Host "Требуются права администратора. Перезапуск с повышением..." -ForegroundColor Yellow
    Start-Process pwsh.exe "-ExecutionPolicy Bypass -NoProfile -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

$packageName = "PomodoroTimer"
$certThumb   = "D17F2375F6A3FCCEC161D77813FCA801106C30E2"

try {

Write-Host "`n=== PomodoroTimer — полное удаление ===`n" -ForegroundColor Yellow

# ── 1. Удаляем MSIX-пакет ────────────────────────────────────────────────
Write-Host "[1/5] Удаление MSIX-пакета..." -ForegroundColor Cyan
$pkgs = Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue
if ($pkgs) {
    foreach ($pkg in $pkgs) {
        Write-Host "  Removing: $($pkg.PackageFullName)"
        Remove-AppxPackage -Package $pkg.PackageFullName
    }
    Write-Host "  Пакет удалён." -ForegroundColor Green
} else {
    Write-Host "  Пакет не найден — пропускаем." -ForegroundColor Gray
}

# ── 2. Удаляем сертификат из TrustedPeople ───────────────────────────────
Write-Host "[2/5] Удаление сертификата из CurrentUser\TrustedPeople..." -ForegroundColor Cyan
$certPath = "cert:\CurrentUser\TrustedPeople\$certThumb"
if (Test-Path $certPath) {
    Remove-Item $certPath -Force
    Write-Host "  Удалён из TrustedPeople." -ForegroundColor Green
} else {
    Write-Host "  Не найден в TrustedPeople — пропускаем." -ForegroundColor Gray
}

# ── 3. Удаляем сертификат из личного хранилища (My) ─────────────────────
Write-Host "[3/5] Удаление сертификата из CurrentUser\My..." -ForegroundColor Cyan
$certPathMy = "cert:\CurrentUser\My\$certThumb"
if (Test-Path $certPathMy) {
    Remove-Item $certPathMy -Force
    Write-Host "  Удалён из CurrentUser\My." -ForegroundColor Green
} else {
    Write-Host "  Не найден в CurrentUser\My — пропускаем." -ForegroundColor Gray
}

# ── 4. Удаляем папку с данными приложения ────────────────────────────────
Write-Host "[4/5] Удаление данных приложения..." -ForegroundColor Cyan
$packagesRoot = "$env:LOCALAPPDATA\Packages"
$appDataDirs  = Get-ChildItem $packagesRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like "$packageName*" }
if ($appDataDirs) {
    foreach ($dir in $appDataDirs) {
        Write-Host "  Removing: $($dir.FullName)"
        Remove-Item $dir.FullName -Recurse -Force
    }
    Write-Host "  Данные приложения удалены." -ForegroundColor Green
} else {
    Write-Host "  Папка данных не найдена — пропускаем." -ForegroundColor Gray
}

$appDataSettings = Join-Path $env:APPDATA "PomodoroTimer"
if (Test-Path $appDataSettings) {
    Remove-Item $appDataSettings -Recurse -Force
    Write-Host "  Settings folder removed." -ForegroundColor Green
}

# ── 5. Чистим реестр ─────────────────────────────────────────────────────
Write-Host "[5/5] Очистка записей реестра..." -ForegroundColor Cyan
$regPaths = @(
    # Настройки .NET-приложения (ApplicationSettings / IsolatedStorage)
    "HKCU:\Software\$packageName",
    "HKCU:\Software\Classes\AppX*$packageName*",
    # Toast-канал / WNS
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications\$packageName",
    # Записи о запуске приложения (RecentApps, MRU)
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Search\RecentApps"
)

$removed = 0
foreach ($path in $regPaths) {
    # Поддерживаем wildcards через Get-Item
    $items = Get-Item $path -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        try {
            Remove-Item $item.PSPath -Recurse -Force
            Write-Host "  Removed: $($item.PSPath)" -ForegroundColor Green
            $removed++
        } catch {
            Write-Warning "  Не удалось удалить: $($item.PSPath) — $_"
        }
    }
}

# Дополнительно — ищем записи в Uninstall (на случай frameworkless-записи)
$uninstallPaths = @(
    "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
)
foreach ($root in $uninstallPaths) {
    Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
        $displayName = ($_ | Get-ItemProperty -Name DisplayName -ErrorAction SilentlyContinue).DisplayName
        if ($displayName -like "*$packageName*") {
            Remove-Item $_.PSPath -Recurse -Force
            Write-Host "  Removed Uninstall key: $($_.PSPath)" -ForegroundColor Green
            $removed++
        }
    }
}

if ($removed -eq 0) {
    Write-Host "  Записей реестра не найдено — пропускаем." -ForegroundColor Gray
}

    Write-Host "`n=== Готово! PomodoroTimer полностью удалён из системы. ===`n" -ForegroundColor Green
} catch {
    Write-Host "`n[ОШИБКА] $_`n" -ForegroundColor Red
} finally {
    Read-Host "Нажмите Enter для закрытия"
}
