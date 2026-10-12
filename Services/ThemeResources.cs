using Minecraft_Server_Manager.Models;
using System.Windows;
using System.Windows.Media;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Minecraft_Server_Manager.Services;

public static class ThemeResources
{
    public static readonly IReadOnlyDictionary<string, (string Primary, string Secondary)> Themes = new Dictionary<string, (string, string)>
    {
        ["Dark Blue"] = ("#2c3e50", "#34495e"), ["Midnight Black"] = ("#000000", "#1a1a1a"),
        ["Forest Green"] = ("#1e392a", "#27ae60"), ["Deep Purple"] = ("#4a235a", "#8e44ad"),
        ["Ocean Blue"] = ("#154360", "#2980b9")
    };
    public static void ApplySaved() => Apply(ConfigManager.Settings.SelectedTheme);
    public static void Apply(string? theme)
    {
        var colors = Themes.TryGetValue(theme ?? "Dark Blue", out var value) ? value : Themes["Dark Blue"];
        System.Windows.Application.Current.Resources["PrimaryBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(colors.Primary));
        System.Windows.Application.Current.Resources["SecondaryBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(colors.Secondary));
    }
    public static void ApplyLanguage(string language)
    {
        if (language is not ("fr-FR" or "en-US" or "de-DE" or "es-ES")) language = "fr-FR";
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        string assembly = typeof(ThemeResources).Assembly.GetName().Name!;
        var replacement = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{assembly};component/Resources/Lang/{language}.xaml") };
        foreach (var old in dictionaries.Where(d => d.Source?.OriginalString.Contains("/Resources/Lang/") == true).ToArray()) dictionaries.Remove(old);
        dictionaries.Add(replacement);
    }
}
