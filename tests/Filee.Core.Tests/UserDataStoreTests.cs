using Filee.Core.Presets;
using Filee.Core.Profiles;
using Filee.Core.Settings;

namespace Filee.Core.Tests;

public class UserDataStoreTests
{
    [Fact]
    public void Groups_keep_only_conversions_once_and_at_most_six()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "other", Name = "Other", Kind = PresetKind.Group, SubPresetIds = ["to-pdf"] });
        store.Presets.Add(new Preset
        {
            Id = "mine",
            Name = "Mine",
            Kind = PresetKind.Group,
            SubPresetIds = ["to-png", "gone", "to-png", "other", "to-jpg", "to-webp", "to-bmp", "to-gif", "to-ico", "to-tiff"],
        });
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        // A deleted preset and a group in the group are dropped, a repeat counts once, the rest is cut at six.
        var group = reloaded.FindPreset("mine")!;
        Assert.True(group.IsGroup);
        Assert.Equal(["to-png", "to-jpg", "to-webp", "to-bmp", "to-gif", "to-ico"], group.SubPresetIds);
        // No target format a Filee from before groups (which reads it as a conversion) could run it with.
        Assert.Equal(Preset.GroupTarget, group.TargetFormat);
        Assert.Null(Filee.Core.Formats.FormatRegistry.FindById(Preset.GroupTarget));
    }

    [Fact]
    public void Only_a_conversion_is_found_to_run()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "web", Name = "Web", Kind = PresetKind.Group, SubPresetIds = ["to-png"] });

        Assert.NotNull(store.FindPreset("web")); // to edit it
        Assert.Null(store.FindConversion("web")); // never to run it (a watch folder pointing at it converts nothing)
        Assert.Equal("to-png", store.FindConversion("to-png")!.Id);
    }

    [Fact]
    public void A_kind_from_a_newer_Filee_reads_as_a_group_and_the_other_presets_still_load()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "future", Name = "Future", Kind = PresetKind.Group, SubPresetIds = ["to-png"] });
        store.SaveLibrary();
        var path = Path.Combine(dir.Path, "presets.json");
        var json = File.ReadAllText(path);
        Assert.Contains("\"kind\": \"Group\"", json);
        File.WriteAllText(path, json.Replace("\"kind\": \"Group\"", "\"kind\": \"Chain\""));

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        Assert.True(reloaded.FindPreset("future")!.IsGroup); // never run as a conversion
        Assert.NotNull(reloaded.FindPreset("to-png")); // and not every preset reset to the defaults
        Assert.False(File.Exists(path + ".broken"));
    }

    [Fact]
    public void A_group_saved_by_a_Filee_from_before_groups_is_still_a_group()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "old-group", Name = "Rotate", Kind = PresetKind.Group, SubPresetIds = ["to-png"] });
        store.SaveLibrary();

        // An older Filee knows neither "kind" nor "subPresetIds" and leaves them out when it saves.
        var path = Path.Combine(dir.Path, "presets.json");
        var presets = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var saved = presets.Single(p => (string?)p!["id"] == "old-group")!.AsObject();
        Assert.True(saved.Remove("kind"));
        Assert.True(saved.Remove("subPresetIds"));
        File.WriteAllText(path, presets.ToJsonString());

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        var group = reloaded.FindPreset("old-group")!;
        Assert.True(group.IsGroup); // not a conversion to "group" in the preset list, watch folders and the CLI
        Assert.Empty(group.SubPresetIds);
        Assert.Null(reloaded.FindConversion("old-group"));
    }

    [Fact]
    public void Round_trips_settings_presets_and_profiles()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Settings.Language = "ko";
        store.Settings.Theme.Accent = "#1D9E75";
        store.Settings.Triggers[0].Modifiers = ModifierKeys.Alt | ModifierKeys.Shift;
        store.Presets.Add(new Preset { Id = "mine", Name = "Mine", TargetFormat = "webp", Image = { Quality = 42 } });
        store.Profiles[0].PresetIds.Insert(0, "mine");
        store.SaveSettings();
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        Assert.Equal("ko", reloaded.Settings.Language);
        Assert.Equal("#1D9E75", reloaded.Settings.Theme.Accent);
        Assert.Equal(ModifierKeys.Alt | ModifierKeys.Shift, reloaded.Settings.Triggers[0].Modifiers);
        Assert.Equal(42, reloaded.FindPreset("mine")!.Image.Quality);
        Assert.Equal("mine", reloaded.Profiles[0].PresetIds[0]);
    }

    [Fact]
    public void Dock_and_menu_bar_preferences_round_trip_independently()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.True(store.Settings.ShowInDock);
        Assert.True(store.Settings.ShowMenuBarIcon);

        store.Settings.ShowInDock = false;
        store.SaveSettings();
        var dockOff = new UserDataStore(dir.Path);
        dockOff.Load();
        Assert.False(dockOff.Settings.ShowInDock);
        Assert.True(dockOff.Settings.ShowMenuBarIcon);

        dockOff.Settings.ShowInDock = true;
        dockOff.Settings.ShowMenuBarIcon = false;
        dockOff.SaveSettings();
        var menuBarOff = new UserDataStore(dir.Path);
        menuBarOff.Load();
        Assert.True(menuBarOff.Settings.ShowInDock);
        Assert.False(menuBarOff.Settings.ShowMenuBarIcon);

        menuBarOff.Settings.ShowInDock = false;
        menuBarOff.SaveSettings();
        var bothOff = new UserDataStore(dir.Path);
        bothOff.Load();
        Assert.False(bothOff.Settings.ShowInDock);
        Assert.False(bothOff.Settings.ShowMenuBarIcon);
    }

    [Fact]
    public void Version_20_settings_upgrade_preserves_other_values_and_icon_defaults()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """{ "schemaVersion": 20, "language": "ko" }""");

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
        Assert.Equal("ko", store.Settings.Language);
        Assert.True(store.Settings.ShowInDock);
        Assert.True(store.Settings.ShowMenuBarIcon);
        var rewritten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(AppSettings.CurrentSchemaVersion, (int?)rewritten["schemaVersion"]);
        Assert.True((bool?)rewritten["showInDock"]);
        Assert.True((bool?)rewritten["showMenuBarIcon"]);
        Assert.Equal("ko", (string?)rewritten["language"]);

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(AppSettings.CurrentSchemaVersion, reloaded.Settings.SchemaVersion);
        Assert.Equal("ko", reloaded.Settings.Language);
        Assert.True(reloaded.Settings.ShowInDock);
        Assert.True(reloaded.Settings.ShowMenuBarIcon);
    }

    [Fact]
    public void Older_Filee_round_trip_restores_missing_icon_defaults()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Settings.Language = "ko";
        store.Settings.ShowInDock = false;
        store.Settings.ShowMenuBarIcon = false;
        store.SaveSettings();

        var path = Path.Combine(dir.Path, "settings.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json["schemaVersion"] = 20;
        Assert.True(json.Remove("showInDock"));
        Assert.True(json.Remove("showMenuBarIcon"));
        File.WriteAllText(path, json.ToJsonString());

        var migrated = new UserDataStore(dir.Path);
        migrated.Load();

        Assert.Equal(AppSettings.CurrentSchemaVersion, migrated.Settings.SchemaVersion);
        Assert.True(migrated.Settings.ShowInDock);
        Assert.True(migrated.Settings.ShowMenuBarIcon);
        Assert.Equal("ko", migrated.Settings.Language);
        Assert.False(File.Exists(path + ".broken"));

        var rewritten = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(AppSettings.CurrentSchemaVersion, (int?)rewritten["schemaVersion"]);
        Assert.True((bool?)rewritten["showInDock"]);
        Assert.True((bool?)rewritten["showMenuBarIcon"]);
        Assert.Equal("ko", (string?)rewritten["language"]);

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(AppSettings.CurrentSchemaVersion, reloaded.Settings.SchemaVersion);
        Assert.Equal("ko", reloaded.Settings.Language);
        Assert.True(reloaded.Settings.ShowInDock);
        Assert.True(reloaded.Settings.ShowMenuBarIcon);
    }

    [Theory]
    [InlineData("showInDock", "null")]
    [InlineData("showInDock", "\"future\"")]
    [InlineData("showInDock", "42")]
    [InlineData("showMenuBarIcon", "null")]
    [InlineData("showMenuBarIcon", "\"future\"")]
    [InlineData("showMenuBarIcon", "42")]
    public void Unexpected_icon_preference_values_default_on_without_losing_settings(string field, string value)
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Settings.Language = "ko";
        store.Settings.ShowInDock = field == "showInDock";
        store.Settings.ShowMenuBarIcon = field == "showMenuBarIcon";
        store.SaveSettings();

        var path = Path.Combine(dir.Path, "settings.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        json[field] = System.Text.Json.Nodes.JsonNode.Parse(value);
        File.WriteAllText(path, json.ToJsonString());

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        Assert.Equal("ko", reloaded.Settings.Language);
        Assert.True(field == "showInDock" ? reloaded.Settings.ShowInDock : reloaded.Settings.ShowMenuBarIcon);
        Assert.False(field == "showInDock" ? reloaded.Settings.ShowMenuBarIcon : reloaded.Settings.ShowInDock);
        Assert.False(File.Exists(path + ".broken"));
    }

    [Fact]
    public void Hardware_encoding_is_saved_and_stays_off_for_presets_from_before_it()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.All(store.Presets, p => Assert.False(p.Media.HardwareEncoding)); // no built-in preset turns it on
        store.Presets.Add(new Preset { Id = "fast", Name = "Fast", TargetFormat = "mp4", Media = { HardwareEncoding = true, VideoCodec = VideoCodec.Hevc } });
        store.Presets.Add(new Preset { Id = "old", Name = "Old", TargetFormat = "mp4" });
        store.SaveLibrary();

        // A Filee from before the option saved no "hardwareEncoding".
        var path = Path.Combine(dir.Path, "presets.json");
        var presets = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        Assert.True((bool)presets.Single(p => (string?)p!["id"] == "fast")!["media"]!["hardwareEncoding"]!);
        Assert.True(presets.Single(p => (string?)p!["id"] == "old")!["media"]!.AsObject().Remove("hardwareEncoding"));
        File.WriteAllText(path, presets.ToJsonString());

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        var fast = reloaded.FindPreset("fast")!;
        Assert.True(fast.Media.HardwareEncoding);
        Assert.Equal(VideoCodec.Hevc, fast.Media.VideoCodec);
        Assert.True(fast.Clone().Media.HardwareEncoding);
        Assert.False(reloaded.FindPreset("old")!.Media.HardwareEncoding);
    }

    [Fact]
    public void Animation_speed_is_saved_and_is_the_original_speed_for_presets_from_before_it()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.All(store.Presets, p => Assert.Equal(1, p.Image.AnimationSpeed)); // no built-in preset changes it
        store.Presets.Add(new Preset { Id = "slow", Name = "Slow", TargetFormat = "gif", Image = { AnimationSpeed = 0.5 } });
        store.Presets.Add(new Preset { Id = "old", Name = "Old", TargetFormat = "gif" });
        store.Presets.Add(new Preset { Id = "odd", Name = "Odd", TargetFormat = "gif" });
        store.SaveLibrary();

        // A Filee from before the option saved no "animationSpeed"; a hand-edited file may hold anything.
        var path = Path.Combine(dir.Path, "presets.json");
        var presets = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        Assert.Equal(0.5, (double)presets.Single(p => (string?)p!["id"] == "slow")!["image"]!["animationSpeed"]!);
        Assert.True(presets.Single(p => (string?)p!["id"] == "old")!["image"]!.AsObject().Remove("animationSpeed"));
        presets.Single(p => (string?)p!["id"] == "odd")!["image"]!["animationSpeed"] = -3;
        File.WriteAllText(path, presets.ToJsonString());

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        var slow = reloaded.FindPreset("slow")!;
        Assert.Equal(0.5, slow.Image.AnimationSpeed);
        Assert.Equal(0.5, slow.Clone().Image.AnimationSpeed);
        Assert.True(slow.Image.ChangesSpeed);
        Assert.Equal(1, reloaded.FindPreset("old")!.Image.AnimationSpeed);
        Assert.False(reloaded.FindPreset("old")!.Image.ChangesSpeed);
        Assert.Equal(1, reloaded.FindPreset("odd")!.Image.PlaybackSpeed); // a speed that makes no sense plays as made
    }

    [Fact]
    public void History_is_kept_until_turned_off_and_can_be_cleared()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();

        store.AddHistory(new Filee.Core.History.HistoryEntry { JobId = "a" });
        Assert.Single(store.History);

        store.Settings.KeepHistory = false;
        store.AddHistory(new Filee.Core.History.HistoryEntry { JobId = "b" });
        Assert.Single(store.History); // not recorded

        store.ClearHistory();
        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Empty(reloaded.History);
    }

    [Fact]
    public void History_keeps_the_newest_entries_up_to_the_limit()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.Equal(200, store.Settings.HistoryLimit);

        store.Settings.HistoryLimit = 60;
        for (var i = 0; i < 70; i++)
            store.AddHistory(new Filee.Core.History.HistoryEntry { JobId = $"{i}" });
        Assert.Equal(60, store.History.Count);
        Assert.Equal("69", store.History[0].JobId);
        Assert.Equal("10", store.History[^1].JobId);

        // Lowered below the smallest limit: the oldest go with the next conversion, down to that smallest limit.
        store.Settings.HistoryLimit = 0;
        store.SaveSettings();
        store.AddHistory(new Filee.Core.History.HistoryEntry { JobId = "70" });
        Assert.Equal(AppSettings.MinHistoryLimit, store.History.Count);

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(0, reloaded.Settings.HistoryLimit);
        Assert.Equal(AppSettings.MinHistoryLimit, reloaded.Settings.EffectiveHistoryLimit);
        Assert.Equal(AppSettings.MinHistoryLimit, reloaded.History.Count);
    }

    [Fact]
    public void Version_1_settings_are_upgraded_to_the_current_engine_list()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"),
            """{ "schemaVersion": 1, "enginePriority": ["magick", "word", "hancom", "libreoffice"] }""");

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal(["magick", "markdown", "spreadsheet", "ooxml", "hwpx-writer", "docx-writer", "pdf-text", "pdf-slides", "email", "data", "vector", "icns", "font", "cad", "model3d", "geo", "archive", "ebook", "subtitle", "wic", "ffmpeg", "pandoc", "ghostscript", "calibre", "libreoffice"], store.Settings.EnginePriority);
        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
    }

    [Fact]
    public void Version_18_libraries_get_the_extract_frames_presets_after_the_pdf_presets_on_no_donut()
    {
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id is "extract-frames-png" or "extract-frames-jpg");
        var profiles = BuiltInData.CreateProfiles();

        Assert.True(SettingsMigrations.ApplyToLibrary(18, presets, profiles));

        var pngPreset = presets.Single(p => p.Id == "extract-frames-png");
        Assert.True(pngPreset.Media.SaveEveryFrame);
        Assert.Equal("png", pngPreset.TargetFormat);

        var jpgPreset = presets.Single(p => p.Id == "extract-frames-jpg");
        Assert.True(jpgPreset.Media.SaveEveryFrame);
        Assert.Equal("jpg", jpgPreset.TargetFormat);

        // Where a new library has them.
        Assert.Equal(BuiltInData.CreatePresets().Select(p => p.Id), presets.Select(p => p.Id));
        var slides = presets.FindIndex(p => p.Id == "pdf-slides");
        Assert.Equal(["extract-frames-png", "extract-frames-jpg"], presets.Skip(slides + 1).Take(2).Select(p => p.Id));

        Assert.DoesNotContain(profiles, p => p.PresetIds.Contains("extract-frames-png"));
        Assert.DoesNotContain(profiles, p => p.PresetIds.Contains("extract-frames-jpg"));
        Assert.False(SettingsMigrations.ApplyToLibrary(18, presets, profiles)); // once only
    }

    [Fact]
    public void Version_24_settings_get_the_geo_engine_after_the_3d_models_engine()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"),
            """{ "schemaVersion": 24, "enginePriority": ["magick", "cad", "model3d", "archive", "pandoc", "libreoffice"] }""");

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal(["magick", "cad", "model3d", "geo", "archive", "pandoc", "libreoffice"], store.Settings.EnginePriority);
        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
    }

    [Fact]
    public void Version_27_libraries_get_one_zip_on_the_archives_donut_after_extract()
    {
        var presets = BuiltInData.CreatePresets();
        var profiles = BuiltInData.CreateProfiles();
        var archives = profiles.Single(p => p.Id == "archives");
        archives.PresetIds.Remove("zip-all");

        Assert.True(SettingsMigrations.ApplyToLibrary(27, presets, profiles));
        Assert.Equal(["extract", "zip-all", "to-zip", "to-7z", "to-tgz", "to-tar"], archives.PresetIds);
        Assert.False(SettingsMigrations.ApplyToLibrary(27, presets, profiles)); // once only

        // A library without the preset (the user deleted it) doesn't get it back, and a full donut stays full.
        presets.RemoveAll(p => p.Id == "zip-all");
        archives.PresetIds.Remove("zip-all");
        Assert.False(SettingsMigrations.ApplyToLibrary(27, presets, profiles));
        Assert.DoesNotContain("zip-all", archives.PresetIds);

        presets = BuiltInData.CreatePresets();
        archives.PresetIds = [.. presets.Where(p => p.Id != "zip-all").Take(ToolbarProfile.MaxSlices).Select(p => p.Id)];
        Assert.False(SettingsMigrations.ApplyToLibrary(27, presets, profiles));
        Assert.DoesNotContain("zip-all", archives.PresetIds);
    }

    [Fact]
    public void Version_30_libraries_get_the_live_photo_presets_after_extract_live_video_and_behind_the_video_donut()
    {
        // As written with schema 30: no Live / Motion Photo presets, and the video donut's back face without them.
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id is "live-photo-iphone" or "motion-photo-android");
        var profiles = BuiltInData.CreateProfiles();
        var video = profiles.Single(p => p.Id == "video");
        video.BackPresetIds!.RemoveAll(id => id is "live-photo-iphone" or "motion-photo-android");

        Assert.True(SettingsMigrations.ApplyToLibrary(30, presets, profiles));

        var iphone = presets.Single(p => p.Id == "live-photo-iphone");
        var android = presets.Single(p => p.Id == "motion-photo-android");
        Assert.Equal((LivePhotoKind.Apple, "jpg"), (iphone.Media.LivePhoto, iphone.TargetFormat));
        Assert.Equal((LivePhotoKind.Android, "jpg"), (android.Media.LivePhoto, android.TargetFormat));
        // Where a new library has them, on no donut.
        Assert.Equal(BuiltInData.CreatePresets().Select(p => p.Id), presets.Select(p => p.Id));
        Assert.Equal("extract-motion", presets[presets.IndexOf(iphone) - 1].Id);
        Assert.DoesNotContain(profiles, p => p.PresetIds.Contains("live-photo-iphone") || p.PresetIds.Contains("motion-photo-android"));
        // Behind the video donut as in a new one, after "Extract frames".
        Assert.Equal(BuiltInData.CreateProfiles().Single(p => p.Id == "video").BackPresetIds, video.BackPresetIds);
        Assert.False(SettingsMigrations.ApplyToLibrary(30, presets, profiles)); // once only

        // A back face the user filled up stays as it is.
        var full = BuiltInData.CreateProfiles();
        var fullBack = full.Single(p => p.Id == "video").BackPresetIds!;
        fullBack.RemoveAll(id => id is "live-photo-iphone" or "motion-photo-android");
        while (fullBack.Count < ToolbarProfile.MaxSlices)
            fullBack.Add("to-mp3");
        var before = fullBack.ToList();
        SettingsMigrations.ApplyToLibrary(30, BuiltInData.CreatePresets(), full);
        Assert.Equal(before, fullBack);

        // Back from an older Filee, which dropped the option it didn't know: the preset makes Live Photos again.
        iphone.Media.LivePhoto = LivePhotoKind.None;
        iphone.Name = "My Live Photo";
        Assert.True(SettingsMigrations.ApplyToLibrary(30, presets, profiles));
        Assert.Equal(LivePhotoKind.Apple, iphone.Media.LivePhoto);
        Assert.Equal("My Live Photo", iphone.Name);
        Assert.Single(presets, p => p.Id == "live-photo-iphone");
    }

    [Fact]
    public void Live_photo_options_survive_saving_and_unknown_kinds_read_as_none()
    {
        var preset = new Preset
        {
            TargetFormat = "heic",
            Media = { LivePhoto = LivePhotoKind.Apple, LivePhotoStart = 2.5, LivePhotoLength = 2, LivePhotoCover = 0.5 },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(preset, FileeJsonContext.Default.Preset);
        Assert.Contains("\"livePhoto\": \"Apple\"", json);
        var read = System.Text.Json.JsonSerializer.Deserialize(json, FileeJsonContext.Default.Preset)!;
        Assert.Equal((LivePhotoKind.Apple, 2.5, 2.0, 0.5),
            (read.Media.LivePhoto, read.Media.LivePhotoStart, read.Media.LivePhotoLength, read.Media.LivePhotoCover));

        // A kind of a newer Filee: still one preset that loads, as a plain conversion.
        var future = System.Text.Json.JsonSerializer.Deserialize(json.Replace("\"Apple\"", "\"Hologram\""), FileeJsonContext.Default.Preset)!;
        Assert.Equal(LivePhotoKind.None, future.Media.LivePhoto);
    }

    [Fact]
    public void Version_15_libraries_get_the_tracing_presets_on_the_images_donut_after_pdf()
    {
        // As written before tracing: no trace presets, and the images donut without them.
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id is "trace-svg" or "trace-line-art");
        var profiles = BuiltInData.CreateProfiles();
        var images = profiles.Single(p => p.Id == "images");
        images.PresetIds.RemoveAll(id => id is "trace-svg" or "trace-line-art");

        Assert.True(SettingsMigrations.ApplyToLibrary(15, presets, profiles));

        Assert.Equal(TracingMode.Color, presets.Single(p => p.Id == "trace-svg").Trace.Mode);
        Assert.Equal(TracingMode.BlackAndWhite, presets.Single(p => p.Id == "trace-line-art").Trace.Mode);
        Assert.All(presets.Where(p => p.Id.StartsWith("trace-", StringComparison.Ordinal)), p => Assert.Equal("svg", p.TargetFormat));
        Assert.Equal(BuiltInData.CreateProfiles().Single(p => p.Id == "images").PresetIds, images.PresetIds);
        Assert.False(SettingsMigrations.ApplyToLibrary(15, presets, profiles)); // once only
    }

    [Fact]
    public void Trace_options_survive_a_save_and_load()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        var preset = store.Presets.Single(p => p.Id == "trace-svg");
        preset.Trace.Colors = 6;
        preset.Trace.Detail = 80;
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        var trace = reloaded.Presets.Single(p => p.Id == "trace-svg").Trace;
        Assert.Equal((TracingMode.Color, 6, 80), (trace.Mode, trace.Colors, trace.Detail));
        Assert.Equal(TracingMode.BlackAndWhite, reloaded.Presets.Single(p => p.Id == "trace-line-art").Trace.Mode);
    }

    [Fact]
    public void Version_16_libraries_get_rotate_and_flip_presets_and_groups_on_no_donut()
    {
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id is "rotate-left" or "rotate-right" or "rotate-180" or "flip-horizontal" or "flip-vertical" or "rotate" or "flip");
        var profiles = BuiltInData.CreateProfiles();

        Assert.True(SettingsMigrations.ApplyToLibrary(16, presets, profiles));

        var rotate = presets.Single(p => p.Id == "rotate");
        Assert.True(rotate.IsGroup);
        Assert.Equal(["rotate-left", "rotate-right", "rotate-180"], rotate.SubPresetIds);
        Assert.Equal(ImageRotation.Left90, presets.Single(p => p.Id == "rotate-left").Image.Rotation);
        Assert.Equal(ImageFlip.Horizontal, presets.Single(p => p.Id == "flip-horizontal").Image.Flip);
        Assert.DoesNotContain(profiles, p => p.PresetIds.Any(id => id.StartsWith("rotate") || id.StartsWith("flip")));
        Assert.False(SettingsMigrations.ApplyToLibrary(16, presets, profiles)); // once only
        // Where a new library has them: after "Remove background".
        var background = presets.FindIndex(p => p.Id == "remove-background");
        Assert.Equal("rotate-left", presets[background + 1].Id);
    }

    [Fact]
    public void The_close_action_is_saved_and_one_from_a_newer_Filee_reads_as_ask()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        Assert.Equal(CloseAction.Ask, store.Settings.CloseAction);
        store.Settings.CloseAction = CloseAction.Quit;
        store.SaveSettings();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(CloseAction.Quit, reloaded.Settings.CloseAction);

        var path = Path.Combine(dir.Path, "settings.json");
        var json = File.ReadAllText(path);
        Assert.Contains("\"closeAction\": \"Quit\"", json);
        File.WriteAllText(path, json.Replace("\"closeAction\": \"Quit\"", "\"closeAction\": \"Minimize\""));
        var newer = new UserDataStore(dir.Path);
        newer.Load();
        Assert.Equal(CloseAction.Ask, newer.Settings.CloseAction);
        Assert.False(File.Exists(path + ".broken"));
    }

    [Fact]
    public void A_rotation_from_a_newer_Filee_reads_as_none_and_the_other_presets_still_load()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.SaveLibrary();
        var path = Path.Combine(dir.Path, "presets.json");
        var json = File.ReadAllText(path);
        Assert.Contains("\"rotation\": \"Left90\"", json);
        File.WriteAllText(path, json.Replace("\"rotation\": \"Left90\"", "\"rotation\": \"Transpose\"")
            .Replace("\"flip\": \"Vertical\"", "\"flip\": 7"));

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();

        Assert.Equal(ImageRotation.None, reloaded.FindPreset("rotate-left")!.Image.Rotation);
        Assert.Equal(ImageFlip.None, reloaded.FindPreset("flip-vertical")!.Image.Flip);
        Assert.Equal(ImageRotation.Right90, reloaded.FindPreset("rotate-right")!.Image.Rotation); // the rest as saved
        Assert.False(File.Exists(path + ".broken"));
    }

    [Fact]
    public void Back_from_an_older_Filee_rotate_and_flip_work_again_and_keep_the_users_changes()
    {
        // What a Filee before 17 leaves after saving: kind, members and rotation dropped, the rest as it was.
        var presets = BuiltInData.CreatePresets();
        var left = presets.Single(p => p.Id == "rotate-left");
        left.Image.Rotation = ImageRotation.None;
        left.Name = "Turn left";
        var rotate = presets.Single(p => p.Id == "rotate");
        rotate.Kind = PresetKind.Convert;
        rotate.SubPresetIds = [];
        var profiles = BuiltInData.CreateProfiles();

        Assert.True(SettingsMigrations.ApplyToLibrary(16, presets, profiles));

        Assert.Equal(ImageRotation.Left90, left.Image.Rotation);
        Assert.Equal("Turn left", left.Name);
        Assert.True(rotate.IsGroup);
        Assert.Equal(["rotate-left", "rotate-right", "rotate-180"], rotate.SubPresetIds);
        Assert.Equal(Preset.GroupTarget, rotate.TargetFormat);
        Assert.Single(presets, p => p.Id == "rotate-left"); // repaired, not added twice
    }

    [Fact]
    public void Version_25_libraries_get_crop_tool_preset_after_remove_background_on_no_donut()
    {
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id == "tool-crop");
        var profiles = BuiltInData.CreateProfiles();

        Assert.True(SettingsMigrations.ApplyToLibrary(25, presets, profiles));

        var crop = presets.Single(p => p.Id == "tool-crop");
        Assert.Equal(PresetKind.Tool, crop.Kind);
        Assert.Equal("remove-background", presets[presets.IndexOf(crop) - 1].Id);
        Assert.DoesNotContain(profiles, p => p.PresetIds.Contains("tool-crop"));
        Assert.False(SettingsMigrations.ApplyToLibrary(25, presets, profiles)); // once only
    }

    [Fact]
    public void Version_41_libraries_get_the_spreadsheet_print_studio_after_crop_and_on_the_back()
    {
        var presets = BuiltInData.CreatePresets();
        presets.RemoveAll(p => p.Id == "tool-sheet-studio");
        var profiles = BuiltInData.CreateProfiles();
        profiles.Single(p => p.Id == "spreadsheets").BackPresetIds!.Remove("tool-sheet-studio");

        Assert.True(SettingsMigrations.ApplyToLibrary(41, presets, profiles));

        var studio = presets.Single(p => p.Id == "tool-sheet-studio");
        Assert.Equal((PresetKind.Tool, "pdf"), (studio.Kind, studio.TargetFormat));
        Assert.Equal(presets.FindIndex(p => p.Id == "tool-crop") + 1, presets.IndexOf(studio)); // as in a new library
        Assert.Equal("tool-sheet-studio", profiles.Single(p => p.Id == "spreadsheets").BackPresetIds![0]);
        Assert.False(SettingsMigrations.ApplyToLibrary(41, presets, profiles));
    }

    [Fact]
    public void Version_41_libraries_keep_the_print_studio_off_the_back_when_the_front_has_it()
    {
        var presets = BuiltInData.CreatePresets();
        var profiles = BuiltInData.CreateProfiles();
        var spreadsheets = profiles.Single(p => p.Id == "spreadsheets");
        spreadsheets.BackPresetIds!.Remove("tool-sheet-studio");
        spreadsheets.PresetIds.Add("tool-sheet-studio"); // the user put it on the front

        SettingsMigrations.ApplyToLibrary(41, presets, profiles);

        Assert.DoesNotContain("tool-sheet-studio", spreadsheets.BackPresetIds);
        Assert.Single(presets, p => p.Id == "tool-sheet-studio");
    }

    [Fact]
    public void Back_from_an_older_Filee_tool_crop_works_again_and_keeps_the_users_changes()
    {
        // What a Filee before 26 leaves after saving: kind read as Group, TargetFormat dropped, SubPresetIds empty, the rest as it was.
        var presets = BuiltInData.CreatePresets();
        var crop = presets.Single(p => p.Id == "tool-crop");
        crop.Kind = PresetKind.Group;
        crop.TargetFormat = Preset.GroupTarget;
        crop.SubPresetIds = [];
        crop.Name = "Trim";

        Assert.True(SettingsMigrations.ApplyToLibrary(25, presets, BuiltInData.CreateProfiles()));

        Assert.Equal(PresetKind.Tool, crop.Kind);
        Assert.Equal(BuiltInData.SameAsSource, crop.TargetFormat);
        Assert.Empty(crop.SubPresetIds);
        Assert.Equal("Trim", crop.Name);
        Assert.Single(presets, p => p.Id == "tool-crop");
    }

    [Fact]
    public void Back_from_an_older_Filee_compress_pdf_compresses_again_and_keeps_the_users_changes()
    {
        // What a Filee before 20 leaves after saving: the compression level dropped, the rest as it was.
        var presets = BuiltInData.CreatePresets();
        var compress = presets.Single(p => p.Id == "compress-pdf");
        compress.Pdf.Compression = PdfCompression.None;
        compress.Name = "Shrink";

        Assert.True(SettingsMigrations.ApplyToLibrary(17, presets, BuiltInData.CreateProfiles()));

        Assert.Equal(PdfCompression.Ebook, compress.Pdf.Compression);
        Assert.Equal("Shrink", compress.Name);
        Assert.Single(presets, p => p.Id == "compress-pdf");
    }

    [Fact]
    public void Pdf_compression_survives_a_save_and_load_and_a_newer_level_reads_as_none()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        var preset = store.Presets.Single(p => p.Id == "compress-pdf");
        preset.Pdf.Compression = PdfCompression.Print;
        preset.Pdf.CompressWithGhostscript = true;
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        var pdf = reloaded.Presets.Single(p => p.Id == "compress-pdf").Pdf;
        Assert.Equal((PdfCompression.Print, true), (pdf.Compression, pdf.CompressWithGhostscript));

        var path = Path.Combine(dir.Path, "presets.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"compression\": \"Print\"", "\"compression\": \"Extreme\""));
        var newer = new UserDataStore(dir.Path);
        newer.Load();
        Assert.Equal(PdfCompression.None, newer.FindPreset("compress-pdf")!.Pdf.Compression);
        Assert.False(File.Exists(path + ".broken"));
    }

    [Fact]
    public void Several_mixed_profiles_are_kept_and_one_is_added_when_none_is_left()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Profiles.Add(new ToolbarProfile { Id = "scans", Name = "Scans", IsFallback = true, Extensions = ["jpg", "pdf"] });
        store.SaveLibrary();

        var reloaded = new UserDataStore(dir.Path);
        reloaded.Load();
        Assert.Equal(["mixed", "scans"], reloaded.Profiles.Where(p => p.IsFallback).Select(p => p.Id));

        reloaded.Profiles.RemoveAll(p => p.IsFallback);
        reloaded.SaveLibrary();
        Assert.Equal("mixed", Assert.Single(reloaded.Profiles, p => p.IsFallback).Id);
    }

    [Fact]
    public void Imported_mixed_profiles_stay_mixed()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Profiles.Add(new ToolbarProfile { Id = "scans", Name = "Scans", IsFallback = true, Extensions = ["jpg", "pdf"] });
        var bundle = Path.Combine(dir.Path, "bundle.json");
        store.Export(bundle);

        using var other = new TempDir();
        var target = new UserDataStore(other.Path);
        target.Load();
        target.Import(bundle);

        Assert.True(target.Profiles.Single(p => p.Id == "scans").IsFallback);
        Assert.Single(target.Profiles, p => p.Id == "mixed");
        Assert.Equal("scans", ProfileSelector.Select(target.Profiles, ["a.jpg", "b.pdf"])!.Id);
        Assert.Equal("mixed", ProfileSelector.Select(target.Profiles, ["a.jpg", "b.docx"])!.Id);
    }

    [Fact]
    public void Enums_are_stored_as_strings()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.SaveSettings();

        var json = File.ReadAllText(Path.Combine(dir.Path, "settings.json"));
        Assert.Contains("\"Drag\"", json);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "presets.json"), "{ not json");
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{ not json");

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.NotEmpty(store.Presets);
        Assert.True(store.Settings.ShowInDock);
        Assert.True(store.Settings.ShowMenuBarIcon);
        Assert.True(File.Exists(Path.Combine(dir.Path, "presets.json.broken")));
        Assert.True(File.Exists(Path.Combine(dir.Path, "settings.json.broken")));
    }

    [Fact]
    public void Dangling_preset_references_are_removed()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Profiles[0].PresetIds.Add("does-not-exist");
        store.SaveLibrary();

        Assert.DoesNotContain("does-not-exist", store.Profiles[0].PresetIds);
    }

    [Fact]
    public void Export_then_import_restores_presets()
    {
        using var dir = new TempDir();
        var store = new UserDataStore(dir.Path);
        store.Load();
        store.Presets.Add(new Preset { Id = "shared", Name = "Shared", TargetFormat = "png" });
        var bundle = Path.Combine(dir.Path, "bundle.json");
        store.Export(bundle);

        using var other = new TempDir();
        var target = new UserDataStore(other.Path);
        target.Load();
        target.Import(bundle);

        Assert.NotNull(target.FindPreset("shared"));
        Assert.Single(target.Profiles, p => p.IsFallback);
    }
}
