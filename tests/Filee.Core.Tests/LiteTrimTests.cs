// Filee Lite lists far fewer formats than the engines can convert. These tests keep the trimmed library consistent:
// no donut or preset may name a format the registry no longer knows (FormatRegistry.Get throws on it).

using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Presets;
using Filee.Core.Profiles;

namespace Filee.Core.Tests;

public class LiteTrimTests
{
    [Fact]
    public void An_edge_to_a_format_that_is_not_listed_does_not_break_the_catalog_or_the_planner()
    {
        var converter = new FakeConverter("engine",
            new ConversionEdge("png", "jpg"), new ConversionEdge("png", "no-such-format"), new ConversionEdge("no-such-format", "pdf"));
        var catalog = new ConverterCatalog([converter]);

        var planner = catalog.CreatePlanner(["engine"]);

        Assert.NotNull(planner.Plan("png", "jpg"));
        Assert.Null(planner.Plan("png", "pdf")); // only reachable through the unlisted format
        Assert.Null(planner.Plan("png", "no-such-format"));
        Assert.Single(ConverterCatalog.SupportedEdges(converter));
    }

    [Fact]
    public void Every_slice_of_every_built_in_donut_exists_and_converts_to_a_listed_format()
    {
        var presets = BuiltInData.CreatePresets().ToDictionary(p => p.Id);
        foreach (var profile in BuiltInData.CreateProfiles())
        {
            Assert.InRange(profile.PresetIds.Count, 1, ToolbarProfile.MaxSlices);
            Assert.InRange((profile.BackPresetIds ?? []).Count, 0, ToolbarProfile.MaxSlices);
            foreach (var id in profile.PresetIds.Concat(profile.BackPresetIds ?? []))
            {
                Assert.True(presets.TryGetValue(id, out var preset), $"{profile.Id}: unknown preset '{id}'");
                AssertListed(preset!, presets);
            }
            Assert.All(profile.Extensions, e => Assert.NotNull(FormatRegistry.FindByExtension(e)));
        }
    }

    [Fact]
    public void Every_built_in_preset_targets_a_listed_format()
    {
        var presets = BuiltInData.CreatePresets().ToDictionary(p => p.Id);
        Assert.All(presets.Values, preset => AssertListed(preset, presets));
    }

    [Fact]
    public void The_lite_donuts_are_the_expected_ones()
    {
        Assert.Equal(
            ["images", "pdf", "office", "spreadsheets", "presentations", "text", "video", "audio", "archives", "mixed"],
            BuiltInData.CreateProfiles().Select(p => p.Id));
    }

    private static void AssertListed(Preset preset, Dictionary<string, Preset> presets)
    {
        if (preset.IsGroup)
        {
            Assert.All(preset.SubPresetIds, id => Assert.True(presets.ContainsKey(id), $"group '{preset.Id}': unknown preset '{id}'"));
            return;
        }
        if (preset.TargetFormat == BuiltInData.SameAsSource)
            return;
        Assert.True(FormatRegistry.FindById(preset.TargetFormat) is not null, $"preset '{preset.Id}' targets unlisted '{preset.TargetFormat}'");
    }
}

public class LiteIdentityTests
{
    [Fact]
    public void Update_checks_are_off_by_default() => Assert.False(new Filee.Core.Settings.AppSettings().CheckForUpdates);

    [Fact]
    public void The_default_engine_priority_names_only_registered_engines()
    {
        var known = new HashSet<string>(["magick", "pdfium", "pdfsharp", "rhwp", "markdown", "document-markdown", "spreadsheet", "ooxml",
            "hwpx-writer", "docx-writer", "pdf-text", "pdf-slides", "pdf-tables", "vector", "archive", "ffmpeg", "vision", "libreoffice"]);
        Assert.All(new Filee.Core.Settings.AppSettings().EnginePriority, id => Assert.Contains(id, known));
    }
}
