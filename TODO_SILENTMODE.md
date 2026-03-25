# TODO: Silent Mode — MSIX Packaging for Toast `scenario="alarm"`

## Цель

Упаковать WPF-приложение PomodoroTimer в **MSIX-пакет**, чтобы Windows доверял `scenario="alarm"` в Toast-уведомлениях и показывал их поверх режима "Не беспокоить". Это также подготавливает приложение к публикации в **Microsoft Store**.

---

## Контекст

- Проект: WPF, .NET 8, `net8.0-windows10.0.17763.0`
- Solution: `PomodoroTimer.sln` (содержит только `PomodoroTimer.csproj`)
- IDE для этой задачи: **Visual Studio** (`.sln` уже есть)
- Уже реализовано: Silent Mode (FlashWindowEx + DropShadowEffect glow + WinRT Toast с `scenario="alarm"`)
- Проблема: Toast с `scenario="alarm"` **не пробивает DND** для unpackaged (`exe`) приложений — только для MSIX-упакованных

---

## Шаги

### Phase 1: Создание Windows Application Packaging Project

1. **Открыть `PomodoroTimer.sln` в Visual Studio**

2. **Добавить новый проект в solution:**
   - Right-click Solution → Add → New Project
   - Шаблон: **"Windows Application Packaging Project"** (WAP)
   - Имя: `PomodoroTimer.Package`
   - Target Version: `Windows 10, version 1809 (10.0; Build 17763)` или выше
   - Min Version: `Windows 10, version 1809 (10.0; Build 17763)`

3. **Добавить ссылку на WPF-проект:**
   - В проекте `PomodoroTimer.Package` → раздел `Applications` (Dependencies) → Right-click → Add Reference
   - Выбрать `PomodoroTimer`
   - Установить его как **Entry Point**

### Phase 2: Настройка манифеста `Package.appxmanifest`

4. **Открыть `Package.appxmanifest`** (в визуальном редакторе или как XML)

5. **Вкладка Application:**
   - Display Name: `Pomodoro Timer`
   - Description: `Таймер Помодоро с Silent Mode`
   - Entry Point: `PomodoroTimer.exe`

6. **Вкладка Visual Assets:**
   - Сгенерировать иконки из `Assets/ico/Gemini_Generated_Image_ie835.png` (самая большая чистая иконка 835×835)
   - Visual Studio может автоматически сгенерировать все нужные размеры (44×44, 150×150, Store logo etc.)
   - Или использовать уже имеющиеся: `Pom200x200.png`, `Pom300x300.png`, `Pom400x400.png` — для тайлов

7. **Вкладка Capabilities:**
   - Не нужны специальные capabilities для Toast

8. **XML: Добавить namespace и Toast capability** (открыть `Package.appxmanifest` как XML):

   В корневом элементе `<Package>` добавить namespaces:
   ```xml
   xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
   xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
   ```

   В секции `<Applications>` → `<Application>` → добавить `<Extensions>` с активатором для Toast:
   ```xml
   <Extensions>
     <desktop:Extension Category="windows.toastNotificationActivation">
       <desktop:ToastNotificationActivation
         ToastActivatorCLSID="PUT-A-NEW-GUID-HERE" />
     </desktop:Extension>
     <com:Extension Category="windows.comServer">
       <com:ComServer>
         <com:ExeServer Executable="PomodoroTimer.exe"
                        Arguments="----AppNotificationActivated:"
                        DisplayName="Pomodoro Timer Notification">
           <com:Class Id="PUT-SAME-GUID-HERE" />
         </com:ExeServer>
       </com:ComServer>
     </com:Extension>
   </Extensions>
   ```

   **Примечание:** Сгенерировать новый GUID для CLSID (Tools → Create GUID в Visual Studio). Использовать один и тот же GUID в обоих местах.

### Phase 3: Упрощение кода в App.xaml.cs

9. **Удалить `RegisterAumid()` и связанный код** из `App.xaml.cs`:
   - Удалить метод `RegisterAumid()` целиком
   - Удалить P/Invoke `SetCurrentProcessExplicitAppUserModelID` и `[DllImport("shell32.dll"...)]`
   - Удалить `using System.Runtime.InteropServices;` (если больше нигде не используется в этом файле)
   - Удалить вызов `RegisterAumid()` из `OnStartup()`
   - MSIX-пакет автоматически регистрирует AUMID при установке

10. **Упростить `ShowTrayNotification()`** — убрать явный `aumid` из `CreateToastNotifier()`:
    ```csharp
    internal void ShowTrayNotification()
    {
        try
        {
            var xml = new XmlDocument();
            xml.LoadXml("""
                <toast scenario="alarm">
                  <visual>
                    <binding template="ToastGeneric">
                      <text>Pomodoro Timer</text>
                      <text>Таймер завершён!</text>
                    </binding>
                  </visual>
                  <audio silent="true"/>
                </toast>
                """);
            var notifier = ToastNotificationManager.CreateToastNotifier();
            notifier.Show(new ToastNotification(xml));
        }
        catch { /* ignore */ }
    }
    ```

11. **Заменить `ApplyAutostart()`** — реестровый способ (`HKCU\...\Run`) **не работает в MSIX**.

    В манифесте (`Package.appxmanifest`) добавить startupTask extension в тот же блок `<Extensions>`:
    ```xml
    <desktop:Extension Category="windows.startupTask"
                       EntryPoint="Windows.FullTrustApplication">
      <desktop:StartupTask TaskId="PomodoroTimerStartup"
                           Enabled="false"
                           DisplayName="Pomodoro Timer" />
    </desktop:Extension>
    ```

    В `App.xaml.cs` заменить метод `ApplyAutostart`:
    ```csharp
    internal static async void ApplyAutostart(bool enable)
    {
        try
        {
            var startupTask = await Windows.ApplicationModel.StartupTask.GetAsync("PomodoroTimerStartup");
            if (enable)
                await startupTask.RequestEnableAsync();
            else
                startupTask.Disable();
        }
        catch { /* ignore */ }
    }
    ```

### Phase 4: Сборка и тестирование

12. **Установить `PomodoroTimer.Package` как стартовый проект:**
    - Right-click → Set as Startup Project

13. **Создать временный сертификат для отладки:**
    - В `Package.appxmanifest` → вкладка Packaging → Choose Certificate → Create...
    - Publisher Name: любое (например `CN=PomodoroTimerDev`)

14. **Собрать и запустить (F5):**
    - Visual Studio автоматически соберёт MSIX, установит и запустит
    - При каждом новом билде — переустановка автоматическая

15. **Тестирование Silent Mode:**
    - [ ] Включить Silent Mode в настройках
    - [ ] Включить "Не беспокоить" в Windows
    - [ ] Скрыть окно в трей (✕)
    - [ ] Запустить таймер на 1 минуту
    - [ ] **Ожидание:** Toast появляется несмотря на DND
    - [ ] Свечение контура окна работает
    - [ ] Иконка в taskbar мигает

16. **Тестирование обычного режима:**
    - [ ] Выключить Silent Mode → звук при завершении

17. **Тестирование автозапуска:**
    - [ ] Включить автозапуск в настройках → перезагрузить → проверить что приложение стартует

---

## Критические файлы

| Файл | Что менять |
|------|-----------|
| `PomodoroTimer.sln` | Добавить проект `PomodoroTimer.Package` |
| `PomodoroTimer.Package/Package.appxmanifest` | Identity, Visual Assets, Toast Extension, StartupTask |
| `App.xaml.cs` | Удалить `RegisterAumid()`, упростить `ShowTrayNotification()`, заменить `ApplyAutostart()` |
| `Assets/` | Сгенерировать Store-иконки из `Gemini_Generated_Image_ie835.png` |

## Имеющиеся ассеты для иконок

- `Assets/ico/Gemini_Generated_Image_ie835.png` — чистый исходник, использовать для генерации Store-иконок
- `Assets/ico/16.ico`, `32.ico`, `48.ico`, `256.ico` — существующие .ico
- `Assets/bkg/Pom200x200.png`, `Pom300x300.png`, `Pom400x400.png`, `Pom500x500.png`

## Важные ограничения

- **MSIX Debug:** Visual Studio переустанавливает приложение при каждом F5 — это нормально
- **Autostart:** Реестровый способ (`HKCU\...\Run`) НЕ работает в MSIX — обязателен переход на `StartupTask` API
- **Settings (`AppSettings.cs`):** `Environment.SpecialFolder.ApplicationData` работает в MSIX (перенаправляется в виртуализированную папку, `settings.json` сохраняется), менять не нужно
- **Tray icon:** `Hardcodet.NotifyIcon.Wpf` работает в MSIX без изменений
- **ShowBalloonTip в catch:** Оставлен как fallback на случай ошибки WinRT Toast
