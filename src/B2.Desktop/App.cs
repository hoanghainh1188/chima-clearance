namespace B2.Desktop;

// Sprint S0: bootstrap-only Application subclass. Window title and chrome are
// placeholders — real shell + theming arrive in S1 (see specs/05-ui-wireframes.md).
public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new ContentPage());
}
