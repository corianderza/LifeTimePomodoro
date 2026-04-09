# Публикация в Windows Store через Visual Studio

## Что было сделано

Добавлен **Windows Application Packaging Project** (`PomodoroTimer.Package`) в solution для упаковки WPF приложения в MSIX пакет через Visual Studio.

### Структура проекта:
```
PomodoroTimer/
├── PomodoroTimer.csproj          # Основной WPF проект
├── PomodoroTimer.Package/         # WAP проект для создания MSIX
│   ├── PomodoroTimer.Package.wapproj
│   ├── Package.appxmanifest      # Манифест приложения
│   └── Images/                   # Иконки для Store
└── PomodoroTimer.sln             # Solution с обоими проектами
```

## Как собрать MSIX для публикации в Windows Store

### 1. Откройте solution в Visual Studio
- Откройте файл `PomodoroTimer.sln`
- Установите **PomodoroTimer.Package** как стартовый проект (правый клик → Set as StartUp Project)

### 2. Настройка перед публикацией

#### 2.1 Обновите Package.appxmanifest для Store
В проекте `PomodoroTimer.Package` откройте `Package.appxmanifest`:

**Identity** - обновите эти поля после резервирования имени в Partner Center:
```xml
<Identity
  Name="YourPublisher.PomodoroTimer"  ← замените на имя из Partner Center
  Publisher="CN=YourPublisher"        ← замените на ваш Publisher из сертификата Store
  Version="1.0.0.0" />
```

**Важно:** 
- `Name` - получите при резервировании имени приложения в Partner Center
- `Publisher` - должен совпадать с Publisher из вашего Store сертификата
- Для dev-сборки временный сертификат работает, но для Store нужен официальный

#### 2.2 Проверьте изображения
Убедитесь, что все изображения присутствуют в `PomodoroTimer.Package\Images\`:
- `Square150x150Logo.scale-200.png` (300x300 px)
- `Square44x44Logo.scale-200.png` (88x88 px)
- `StoreLogo.png` (50x50 px)
- `Wide310x150Logo.scale-200.png` (620x300 px)
- `SplashScreen.scale-200.png` (1240x600 px)
- `LargeTile.scale-200.png` (620x620 px)
- `SmallTile.scale-200.png` (142x142 px)

### 3. Создание пакета для Store

#### Способ 1: Через визард Visual Studio (рекомендуется)

1. **Правый клик на проект PomodoroTimer.Package** → **Publish** → **Create App Packages**

2. **Выберите тип распространения:**
   - Для Store: **Microsoft Store using a new app name** или **Microsoft Store as [existing app]**
   - Для тестирования: **Sideloading**

3. **Вход в Partner Center:**
   - Войдите с учетной записью Microsoft Developer
   - Выберите зарезервированное имя приложения или создайте новое

4. **Настройка пакета:**
   - **Platforms:** выберите x64, x86, ARM64 (или только те, что нужны)
   - **Include full PDB symbol files:** отметьте для отладки в Store
   - **Generate app bundle:** выберите **Always** (создаст .msixbundle для всех архитектур)

5. **Signing:**
   - Для Store Visual Studio автоматически использует Store сертификат
   - Для Sideloading - используется ваш .pfx

6. **Создание пакета:**
   - Нажмите **Create**
   - Пакет будет создан в `PomodoroTimer.Package\AppPackages\`

#### Способ 2: Прямая сборка проекта

1. **Установите конфигурацию:**
   - В Visual Studio выберите **Release** и нужную платформу (x64/x86/ARM64)

2. **Build:**
   - Build → Build PomodoroTimer.Package
   - или правый клик на PomodoroTimer.Package → Build

3. **Результат:**
   - `.msix` файл в `PomodoroTimer.Package\bin\[Platform]\Release\`
   - Можно установить локально для тестирования

### 4. Загрузка в Partner Center

1. **Откройте Partner Center:** https://partner.microsoft.com/dashboard
2. **Создайте новое приложение** или откройте существующее
3. **Submissions → New submission**
4. **Packages:**
   - Загрузите `.msixupload` файл (создается при выборе Store в визарде)
   - Или загрузите `.msixbundle` + символы вручную
5. **Заполните все обязательные секции:**
   - Properties (категория, цена)
   - Age ratings
   - Store listings (описание, скриншоты)
   - Privacy policy URL
6. **Submit for certification**

### 5. Платформы и конфигурации

Solution настроен для поддержки:
- **Debug|x86, x64, ARM64** - для разработки
- **Release|x86, x64, ARM64** - для публикации

**Рекомендация для Store:**
- Собирайте bundle со всеми архитектурами (x86, x64, ARM64)
- Store автоматически доставит правильную версию пользователю

### 6. Тестирование MSIX локально

Перед отправкой в Store протестируйте:

```powershell
# 1. Соберите пакет (x64 для примера)
dotnet build PomodoroTimer.Package/PomodoroTimer.Package.wapproj -c Release /p:Platform=x64

# 2. Установите сертификат (для dev сертификата)
$cert = Get-Item "cert:\CurrentUser\My\D17F2375F6A3FCCEC161D77813FCA801106C30E2"
$store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPeople", "CurrentUser")
$store.Open("ReadWrite")
$store.Add($cert)
$store.Close()

# 3. Установите пакет
Add-AppxPackage -Path "PomodoroTimer.Package\bin\x64\Release\PomodoroTimer.Package_1.0.0.0_x64.msix"
```

## Сравнение с Build-Msix.ps1

| Аспект | Build-Msix.ps1 (ручной) | Visual Studio WAP |
|--------|------------------------|------------------|
| Создание пакета | Ручной PowerShell скрипт | Автоматически через VS |
| Store submission | Требует ручной подготовки | Интегрировано в VS |
| Multi-architecture | Нужно несколько запусков | Bundle из одной сборки |
| Signing | Вручную через signtool | Автоматически |
| Обновление версии | Ручное редактирование | Через UI в VS |
| CI/CD integration | Легко | Требует MSBuild команды |

**Оба подхода работают!** Build-Msix.ps1 отлично подходит для CI/CD и автоматизации, а WAP проект удобен для разработки и прямой публикации в Store через Visual Studio.

## Troubleshooting

### "Package signature is invalid"
- Убедитесь, что сертификат установлен в TrustedPeople
- Для Store: используйте Store сертификат, не dev .pfx

### "Application not trusted"
- Проверьте, что Publisher в манифесте совпадает с CN в сертификате

### "This package is not compatible"
- Проверьте Platform (x86/x64/ARM64)
- MinVersion должна быть не выше версии Windows тестовой машины

### "Не вижу опцию Create App Packages"
- Убедитесь, что установлен workload "Universal Windows Platform development"
- Или установите "MSIX Packaging Tools" в .NET desktop development workload

## Полезные ссылки

- [Microsoft Partner Center](https://partner.microsoft.com/dashboard)
- [Package a desktop app in Visual Studio](https://learn.microsoft.com/windows/msix/package/packaging-uwp-apps)
- [Submit your app to the Store](https://learn.microsoft.com/windows/apps/publish/publish-your-app/overview)
- [MSIX Documentation](https://learn.microsoft.com/windows/msix/)
