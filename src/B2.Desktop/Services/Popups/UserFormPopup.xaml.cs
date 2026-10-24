using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Users;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Maui.Views;

namespace B2.Desktop.Services.Popups;

/// <summary>
/// F-02 Add/Edit user dialog (wireframe § 6.8). Validates input against the same rules the
/// server enforces (username pattern, Constitution VII password complexity) so the Admin gets
/// the reason inline instead of a 400. Closes with a <see cref="UserFormResult"/> on submit or
/// <c>null</c> on Hủy / [×] / Esc / backdrop click.
/// </summary>
public partial class UserFormPopup : Popup
{
    // Picker order; the labels come from AppResources (wireframe § 6.8 default: Operator).
    private static readonly UserRoleDto[] Roles = [UserRoleDto.Operator, UserRoleDto.Supervisor, UserRoleDto.Admin];

    private readonly bool _isCreate;
    private bool _closing;

    public UserFormPopup(UserDto? existing)
    {
        InitializeComponent();
        _isCreate = existing is null;

        RolePicker.ItemsSource = Roles.Select(UserRoleLabels.For).ToList();
        RolePicker.SelectedIndex = Array.IndexOf(Roles, existing?.Role ?? UserRoleDto.Operator);

        if (existing is null)
        {
            TitleLabel.Text = AppResources.Users_Dialog_CreateTitle;
            SubmitButton.Text = AppResources.Users_Dialog_CreateSubmit;
            PasswordEntry.Text = TemporaryPassword.Generate();
        }
        else
        {
            TitleLabel.Text = AppResources.Users_Dialog_EditTitle;
            SubmitButton.Text = AppResources.Common_Save;
            UsernameEntry.Text = existing.Username;
            UsernameEntry.IsReadOnly = true;
            FullNameEntry.Text = existing.FullName;
            PasswordLabel.IsVisible = false;
            PasswordSection.IsVisible = false;
        }

        SemanticProperties.SetHint(SubmitButton, SubmitButton.Text);
    }

    private void OnGenerateClicked(object? sender, EventArgs e)
    {
        PasswordEntry.Text = TemporaryPassword.Generate();
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (_closing)
        {
            return;
        }

        var username = (UsernameEntry.Text ?? string.Empty).Trim().ToLowerInvariant();
        var fullName = (FullNameEntry.Text ?? string.Empty).Trim();
        UserRoleDto? role = RolePicker.SelectedIndex >= 0 ? Roles[RolePicker.SelectedIndex] : null;
        var password = _isCreate ? PasswordEntry.Text ?? string.Empty : null;

        var error = Validate(username, fullName, role, password);
        if (error is not null)
        {
            ErrorLabel.Text = error;
            ErrorLabel.IsVisible = true;
            return;
        }

        _closing = true;
        SubmitButton.IsEnabled = false;
        await CloseAsync(new UserFormResult(username, fullName, role!.Value, password)).ConfigureAwait(true);
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await CloseAsync(null).ConfigureAwait(true);
    }

    private string? Validate(string username, string fullName, UserRoleDto? role, string? password)
    {
        if (_isCreate && !TemporaryPassword.IsValidUsername(username))
        {
            return AppResources.Users_Error_UsernameInvalid;
        }

        if (fullName.Length == 0)
        {
            return AppResources.Users_Error_FullNameRequired;
        }

        if (role is null)
        {
            return AppResources.Users_Error_RoleRequired;
        }

        if (_isCreate && !TemporaryPassword.MeetsComplexity(password))
        {
            return AppResources.Users_Error_PasswordWeak;
        }

        return null;
    }
}
