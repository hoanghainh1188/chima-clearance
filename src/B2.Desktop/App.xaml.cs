using B2.Desktop.Services.UserSettings;

namespace B2.Desktop;

public partial class App : Application
{
    public App(IServiceProvider services, IUserSettingsService userSettings)
    {
        InitializeComponent();
        ApplyFontScale(userSettings.GetFontScale());
        MainPage = services.GetRequiredService<AppShell>();
    }

    /// <summary>
    /// Mutate the <c>FontSize*</c> design tokens before the first page is
    /// resolved. Wireframe § 3 / § 5 / § 9: Normal = +0, Large = +4,
    /// ExtraLarge = +8, applied uniformly across the token set so body-text
    /// stays the spec'd 16 / 20 / 24 px. Subsequent runtime changes write
    /// the new mode to <see cref="IUserSettingsService"/> but require a
    /// re-login (or app restart) to take effect — <c>StaticResource</c>
    /// bindings are resolved once at XAML parse.
    /// </summary>
    private void ApplyFontScale(FontScaleMode mode)
    {
        var offset = mode switch
        {
            FontScaleMode.Large => 4.0,
            FontScaleMode.ExtraLarge => 8.0,
            _ => 0.0,
        };

        if (offset == 0.0)
        {
            return;
        }

        SetScaled("FontSizeXS", 12.0, offset);
        SetScaled("FontSizeS", 14.0, offset);
        SetScaled("FontSizeM", 16.0, offset);
        SetScaled("FontSizeL", 18.0, offset);
        SetScaled("FontSizeXL", 22.0, offset);
        SetScaled("FontSizeXXL", 28.0, offset);
        SetScaled("FontSizeSearchInput", 24.0, offset);
    }

    private void SetScaled(string key, double baseValue, double offset)
    {
        Resources[key] = baseValue + offset;
    }
}
