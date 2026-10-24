using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Filee.App.Controls;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;

namespace Filee.App.Tests;

public class LocalizationResourceTests
{
    [AvaloniaFact]
    public void Strings_are_available_as_application_resources()
    {
        TestServices.EnsureInitialized("ru");
        Assert.True(Application.Current!.TryGetResource("home.title", null, out var value));
        Assert.Equal("Главная", value);
    }

    [AvaloniaFact]
    public void DynamicResource_in_xaml_resolves_and_follows_language_changes()
    {
        TestServices.EnsureInitialized("en");
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, text.GetResourceObservable("home.title"));
        var toggle = new ToggleSwitch();
        var window = new Window { Content = new StackPanel { Children = { text, toggle } } };
        window.Show();
        Assert.Equal("Home", text.Text);

        TestServices.EnsureInitialized("ru");
        Assert.Equal("Главная", text.Text);
        Assert.Equal("Вкл.", toggle.OnContent);
        Assert.Equal("Выкл.", toggle.OffContent);
        window.Close();
    }
}
