namespace B2.Desktop.Services.UserSettings;

/// <summary>
/// User-selectable font scale per wireframe § 5 status bar / § 9 large-text
/// mode. Each level adds a fixed offset to every <c>FontSize*</c> token in
/// <c>Resources/Styles/Typography.xaml</c>: Normal = +0, Large = +4, ExtraLarge
/// = +8 (so body 16 → 20 → 24 matches the wireframe).
/// </summary>
public enum FontScaleMode
{
    Normal = 0,
    Large = 1,
    ExtraLarge = 2,
}
