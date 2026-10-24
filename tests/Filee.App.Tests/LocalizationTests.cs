using System.Text.Json;
using System.Text.RegularExpressions;
using Filee.App.Services;

namespace Filee.App.Tests;

/// <summary>Keeps shipped languages in sync so none silently falls back to English.</summary>
public partial class LocalizationTests
{
    private static string I18nDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Filee.slnx")))
                dir = dir.Parent;
            return Path.Combine(dir!.FullName, "src", "Filee.App", "Assets", "i18n");
        }
    }

    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(I18nDirectory, code + ".json")))!;

    public static TheoryData<string> Languages => new(LocalizationService.Languages.Where(l => l.Code != "en").Select(l => l.Code));

    [Fact]
    public void Shared_language_registry_covers_every_shipped_translation_once()
    {
        var codes = LocalizationService.Languages.Select(l => l.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Directory.GetFiles(I18nDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).Order(), codes.Order());
    }

    [Fact]
    public void Filee_Lite_ships_english_and_russian_only()
    {
        Assert.Equal(["en", "ru"], LocalizationService.Languages.Select(l => l.Code));
        Assert.All(LocalizationService.Languages, l => Assert.False(SoftPalette.IsRightToLeft(l.Code)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_exactly_the_english_keys(string code)
    {
        var english = Load("en").Keys.ToHashSet();
        var other = Load(code).Keys.ToHashSet();

        Assert.Empty(english.Except(other));
        Assert.Empty(other.Except(english));
    }

    [Fact]
    public void Toolbar_tabs_and_reused_categories_exist_in_every_language()
    {
        string[] required =
        [
            "toolbar.tab.all",
            "toolbar.tab.media",
            "toolbar.tab.edit",
            "format.category.Image",
            "format.category.Document",
            "format.category.Archive"
        ];

        foreach (var language in LocalizationService.Languages)
        {
            var translations = Load(language.Code);
            foreach (var key in required)
            {
                Assert.True(translations.TryGetValue(key, out var value), $"{language.Code}:{key} is missing");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Code}:{key} is empty");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Format_placeholders_match_english(string code)
    {
        var english = Load("en");
        foreach (var (key, value) in Load(code))
            Assert.True(
                Placeholders(english[key]).SetEquals(Placeholders(value)),
                $"{code}:{key} placeholders differ from English");
    }

    [Fact]
    public void Every_key_used_in_xaml_exists()
    {
        var english = Load("en");
        var src = Path.GetFullPath(Path.Combine(I18nDirectory, "..", ".."));
        var missing = Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => DynamicResourceKey().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(k => k.Contains('.') && !k.StartsWith("Icon.", StringComparison.Ordinal))
            .Where(k => !english.ContainsKey(k))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void Russian_is_offered_under_its_own_name()
    {
        Assert.Contains(("ru", "Русский"), LocalizationService.Languages);
        Assert.Equal("ru", LocalizationService.Resolve("RU"));
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("ru-RU", "ru")]
    [InlineData("RU-ua", "ru")]
    [InlineData("rus", "en")]
    [InlineData("en-US", "en")]
    [InlineData("ko-KR", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("", "en")] // Linux without LANG: the invariant culture
    public void Auto_follows_the_language_the_user_reads_the_os_in(string os, string expected) =>
        Assert.Equal(expected, LocalizationService.Resolve("auto", () => os));

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    public void A_chosen_language_is_kept_whatever_the_os_is_in(string chosen) =>
        // The OS isn't even asked: the user picked the language in Settings.
        Assert.Equal(chosen, LocalizationService.Resolve(chosen, () => throw new InvalidOperationException("asked the OS")));

    [Fact]
    public void Auto_is_not_the_language_filee_switched_to()
    {
        // Filee sets the current UI culture to the language it shows; "auto" must still mean the user's own.
        var before = LocalizationService.Resolve("auto");
        var shown = before == "ru" ? "en" : "ru";
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(shown);
            Assert.Equal(before, LocalizationService.Resolve("auto"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = culture;
        }
    }

    [Fact]
    public void The_ui_font_chain_is_noto_sans_then_the_system_font()
    {
        Assert.Equal("fonts:Filee#Noto Sans", ThemeService.FontFamilies("en").Split(", ")[0]);
        Assert.Equal("fonts:Filee#Noto Sans", ThemeService.FontFamilies("ru").Split(", ")[0]);
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{DynamicResource ([\w.]+)\}")]
    private static partial Regex DynamicResourceKey();
}
