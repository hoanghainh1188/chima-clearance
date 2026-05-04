using Microsoft.Maui.Storage;

namespace B2.Desktop.Services.UserSettings;

public sealed class UserSettingsService : IUserSettingsService
{
    private const string FontScaleKey = "B2.UI.FontScaleMode";

    public FontScaleMode GetFontScale()
    {
        var stored = Preferences.Default.Get(FontScaleKey, (int)FontScaleMode.Normal);
        return Enum.IsDefined(typeof(FontScaleMode), stored)
            ? (FontScaleMode)stored
            : FontScaleMode.Normal;
    }

    public void SetFontScale(FontScaleMode mode)
    {
        Preferences.Default.Set(FontScaleKey, (int)mode);
    }
}
