# Быстрый старт: Сборка MSIX для Windows Store

## Что сделано

Добавлен проект упаковки **PomodoroTimer.Package** (.wapproj) в solution. Теперь можно собирать MSIX пакеты прямо из Visual Studio для публикации в Windows Store!

## Как собрать пакет для Store (3 шага)

### 1️⃣ Откройте проект в Visual Studio
- Откройте `PomodoroTimer.sln`
- В Solution Explorer установите **PomodoroTimer.Package** как стартовый проект (правый клик → **Set as StartUp Project**)

### 2️⃣ Создайте пакет
Правый клик на **PomodoroTimer.Package** → **Publish** → **Create App Packages**

Выберите:
- **Microsoft Store** (если уже зарегистрировали приложение в Partner Center)
- **Sideloading** (для тестирования локально)

Далее следуйте визарду:
- Выберите платформы: x64, x86, ARM64 (или только нужные)
- Включите **Generate app bundle: Always**
- Нажмите **Create**

### 3️⃣ Найдите готовый пакет
Результат в папке: `PomodoroTimer.Package\AppPackages\`

**Для Store submission:** используйте `.msixupload` файл  
**Для локального теста:** установите `.msix` или `.msixbundle`

## Как установить локально для теста

```powershell
# Перейдите в папку с пакетом
cd PomodoroTimer.Package\AppPackages\PomodoroTimer.Package_1.0.0.0_Test

# Установите
.\Add-AppDevPackage.ps1
```

Visual Studio автоматически создаст скрипт установки!

## Визуальный доступ в VS

Теперь в Visual Studio доступны все функции MSIX:

1. **Package.appxmanifest** (двойной клик в PomodoroTimer.Package):
   - Visual Designer для настройки метаданных
   - Добавление Capabilities
   - Настройка Visual Assets (иконки)

2. **Publish menu:**
   - Create App Packages (для Store или Sideloading)
   - Associate App with the Store (привязка к Partner Center)

3. **Configuration Manager:**
   - Выбор платформы (x86/x64/ARM64)
   - Debug/Release конфигурации

## Публикация в Windows Store

### Подготовка:
1. Зарегистрируйтесь в [Microsoft Partner Center](https://partner.microsoft.com/dashboard)
2. Создайте/зарезервируйте имя приложения
3. В VS: Правый клик на PomodoroTimer.Package → **Publish** → **Associate App with the Store**

### Submission:
1. Создайте пакет через **Create App Packages** → выберите **Microsoft Store**
2. Загрузите `.msixupload` в Partner Center
3. Заполните Store Listing (описание, скриншоты, цена)
4. Submit for Certification

## Что насчёт Build-Msix.ps1?

**Оба подхода работают параллельно!**

| Способ | Когда использовать |
|--------|-------------------|
| **Build-Msix.ps1** | CI/CD, автоматизация, быстрая локальная установка |
| **Visual Studio WAP** | Разработка UI, публикация в Store, multi-platform bundle |

Build-Msix.ps1 остаётся полезным для:
- Автоматических сборок
- Быстрой локальной установки во время разработки
- CI/CD пайплайнов

WAP проект даёт:
- ✅ Визуальный редактор манифеста
- ✅ Автоматическое создание bundle для x86/x64/ARM64
- ✅ Прямую интеграцию с Partner Center
- ✅ Автоматическую подпись Store сертификатом

## Настройки по умолчанию

Проект настроен с:
- **Target Platform:** Windows 10, version 2004 (10.0.22621.0)
- **Min Platform:** Windows 10, version 1809 (10.0.17763.0)
- **Platforms:** x86, x64, ARM64
- **Signing:** PomodoroTimer_TemporaryKey.pfx (для dev), Store cert (для submission)

Подробная инструкция: см. **WINDOWS_STORE_PUBLISHING.md**
