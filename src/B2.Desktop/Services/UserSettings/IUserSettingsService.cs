namespace B2.Desktop.Services.UserSettings;

/// <summary>
/// Persistent per-user UI preferences. Backed by <c>Microsoft.Maui.Storage.Preferences</c>
/// so values survive app restarts. Read once at startup
/// (<see cref="App"/> constructor) to apply font scaling to
/// <c>Application.Current.Resources</c> before any page is parsed.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>The currently saved font scale; returns <see cref="FontScaleMode.Normal"/> when unset.</summary>
    FontScaleMode GetFontScale();

    /// <summary>Persist a new font scale. Caller is responsible for re-applying the scale.</summary>
    void SetFontScale(FontScaleMode mode);
}
