using System.Globalization;
using System.Windows;

namespace PomodoroTimer;

internal static class Localizer
{
    private const string DictUriBase = "pack://application:,,,/Localization/Strings.";
    private const string DictUriSuffix = ".xaml";

    /// <summary>Fallback language used when the system locale is not supported.</summary>
    internal const string DefaultLanguage = "en";

    private static readonly string[] SupportedLanguages = ["zh-Hans", "zh-Hant", "ru", "es", "en"];

    public static string Current { get; private set; } = DefaultLanguage;

    public static void Apply(string langCode)
    {
        var merged  = Application.Current.Resources.MergedDictionaries;
        var baseUri = $"{DictUriBase}{DefaultLanguage}{DictUriSuffix}";

        // Remove ONLY the override dict — keep the base (fallback) dict intact.
        // Any key missing from a translation will still fall through to English.
        var existing = merged.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Localization/Strings.") == true &&
            !d.Source.OriginalString.Equals(baseUri, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            merged.Remove(existing);

        // For the base language no override is needed — the fallback dict is enough.
        if (!langCode.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri($"{DictUriBase}{langCode}{DictUriSuffix}");
            merged.Add(new ResourceDictionary { Source = uri });
        }

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
        // Walk the CultureInfo parent chain so that:
        //   zh-TW → parent: zh-Hant → match ✓
        //   zh-CN → parent: zh-Hans → match ✓
        //   zh-SG → parent: zh-Hans → match ✓
        //   zh-HK → parent: zh-Hant → match ✓
        for (var c = CultureInfo.CurrentUICulture;
             c != CultureInfo.InvariantCulture;
             c = c.Parent)
        {
            var match = SupportedLanguages.FirstOrDefault(
                lang => c.Name.Equals(lang, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }
        return DefaultLanguage;
    }
}
