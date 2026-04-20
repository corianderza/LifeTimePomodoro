# Localization Guide

## Current languages

| Code | Name | File |
|---|---|---|
| `en` | English (base / fallback) | `Localization/Strings.en.xaml` |
| `ru` | Русский | `Localization/Strings.ru.xaml` |
| `es` | Español | `Localization/Strings.es.xaml` |
| `zh-Hans` | 中文 (简体) | `Localization/Strings.zh-Hans.xaml` |
| `zh-Hant` | 中文 (繁體) | `Localization/Strings.zh-Hant.xaml` |

> **Fallback:** `Strings.en.xaml` is always loaded first. If a translation is missing a key, English is shown automatically.

> **Chinese detection:** the system language is resolved by walking the `CultureInfo.Parent` chain, so `zh-TW` / `zh-HK` → `zh-Hant` and `zh-CN` / `zh-SG` → `zh-Hans` work correctly out of the box.

---

## How to add a new language

### Step 1 — Create the dictionary file

Copy `Localization/Strings.en.xaml` and name it `Localization/Strings.{langCode}.xaml`,  
e.g. `Strings.fr.xaml` for French.

Translate all `<sys:String>` values. **Do not change the `x:Key` attributes.**

```xml
<ResourceDictionary xmlns="..."
                    xmlns:x="..."
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="BtnStartLabel">Démarrer</sys:String>
    <!-- ... all other keys ... -->
</ResourceDictionary>
```

### Step 2 — Register in `Localizer.cs`

Add the language code to `SupportedLanguages` in `Localizer.cs`:

```csharp
private static readonly string[] SupportedLanguages =
    ["zh-Hans", "zh-Hant", "ru", "es", "en", "fr"]; // ← add here
```

### Step 3 — Add to the Settings ComboBox

In `SettingsWindow.xaml.cs`, inside `LoadLanguages()`:

```csharp
new("fr", "Français"),   // ← add here
```

### Step 4 — Add to both manifests

In **both** `Package.appxmanifest` and `PomodoroTimer.Package/Package.appxmanifest`:

```xml
<Resources>
    ...
    <Resource Language="fr-FR" />   <!-- ← add here -->
</Resources>
```

### Step 5 — Build & test

```powershell
dotnet build PomodoroTimer.csproj
dotnet run --project PomodoroTimer.csproj
```

Open Settings → select the new language → click OK → verify the UI switches correctly.

---

## Architecture notes

- **`Localizer.Apply(langCode)`** — swaps the override `ResourceDictionary` in `App.Resources.MergedDictionaries` while keeping the base `en` dict intact.  
- **`Localizer.GetString(key)`** — reads a string at runtime (for code-behind like tray menu items).  
- **`DynamicResource`** — used in XAML so all bound controls update instantly on language switch without reopening windows.  
- **`AppSettings.Language`** — persisted to `%AppData%\PomodoroTimer\settings.json`.  
- The tray menu items are updated explicitly via stored `MenuItem` references in `App.xaml.cs` (`UpdateTrayLocalization()`), because code-created controls cannot use `DynamicResource`.
