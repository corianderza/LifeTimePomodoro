using System.Windows;

namespace PomodoroTimer;

internal static class Localizer
{
    private const string DictUriBase = "pack://application:,,,/Localization/Strings.";
    private const string DictUriSuffix = ".xaml";

    public static string Current { get; private set; } = "ru";

    public static void Apply(string langCode)
    {
        var uri    = new Uri($"{DictUriBase}{langCode}{DictUriSuffix}");
        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove any previously loaded localization dictionary
        var existing = merged.FirstOrDefault(
            d => d.Source?.OriginalString.Contains("/Localization/Strings.") == true);
        if (existing != null)
            merged.Remove(existing);

        merged.Add(new ResourceDictionary { Source = uri });
        Current = langCode;
    }

    public static string GetString(string key)
        => Application.Current.Resources[key] as string ?? key;
}
