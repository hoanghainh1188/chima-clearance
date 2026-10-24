using System.Globalization;
using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services.Users;

/// <summary>
/// Wireframe § 6.8 search + status filter, applied client-side to the loaded page of users.
/// Matching ignores case and Vietnamese tone marks ("hung" finds "Hùng"). Pure BCL so the
/// test project can link it.
/// </summary>
public static class UserListFilter
{
    private const CompareOptions MatchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IReadOnlyList<UserDto> Apply(IEnumerable<UserDto> users, string? search, UserStatusFilter status)
    {
        ArgumentNullException.ThrowIfNull(users);
        var term = search?.Trim();
        return users
            .Where(u => status switch
            {
                UserStatusFilter.Active => u.IsActive,
                UserStatusFilter.Inactive => !u.IsActive,
                _ => true,
            })
            .Where(u => string.IsNullOrEmpty(term) || Contains(u.FullName, term) || Contains(u.Username, term))
            .ToList();
    }

    private static bool Contains(string source, string term) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(source, term, MatchOptions) >= 0;
}
