using System.Globalization;
using System.Windows;

namespace PomodoroTimer;

internal static class Localizer
{
    private const string DictUriBase = "pack://application:,,,/Localization/Strings.";
    private const string DictUriSuffix = ".xaml";

    /// <summary>Fallback language used when the system locale is not supported.</summary>
    internal const string DefaultLanguage = "en";

    private static readonly string[] SupportedLanguages = ["zh-Hans", "ru", "es", "en"];

    public static string Current { get; private set; } = DefaultLanguage;

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

    /// <summary>
    /// Walks up the CultureInfo parent chain to find a supported language code.
    /// Returns <see cref="DefaultLanguage"/> if nothing matches.
    /// </summary>
    internal static string DetectSystemLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;
        return SupportedLanguages.FirstOrDefault(lang =>
                   culture.Name.Equals(lang, StringComparison.OrdinalIgnoreCase))
               ?? SupportedLanguages.FirstOrDefault(lang =>
                   culture.TwoLetterISOLanguageName.Equals(lang.Split('-')[0], StringComparison.OrdinalIgnoreCase))
               ?? DefaultLanguage;
    }
}
