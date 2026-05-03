namespace B2.Domain.Users;

/// <summary>
/// User aggregate (FR-014: no hard delete — use <see cref="IsActive"/>).
/// </summary>
public sealed class User
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }

    /// <summary>Normalized lowercase username (INV: write-once).</summary>
    public string Username { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    public bool MustChangePassword { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? FailedLoginWindowStartedAt { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private User()
    {
    }

    public User(Guid id, Username username, string passwordHash, string fullName, Role role, DateTimeOffset createdAt, bool mustChangePassword = true)
    {
        Id = id;
        Username = username.Value;
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("FULL_NAME_REQUIRED", "Full name is required.", 400);
        }

        FullName = fullName.Trim();
        Role = role;
        IsActive = true;
        MustChangePassword = mustChangePassword;
        CreatedAt = createdAt;
    }

    public bool IsLocked(DateTimeOffset now) => LockedUntil.HasValue && LockedUntil.Value > now;

    public void EnsureNotLocked(DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new DomainException("USER_INACTIVE", "User is inactive.", 403);
        }

        if (IsLocked(now))
        {
            throw new DomainException("ACCOUNT_LOCKED", "Account is temporarily locked.", 423);
        }
    }

    public void RecordFailedLogin(DateTimeOffset now)
    {
        if (FailedLoginWindowStartedAt is null || now - FailedLoginWindowStartedAt > FailureWindow)
        {
            FailedLoginWindowStartedAt = now;
            FailedLoginCount = 1;
        }
        else
        {
            FailedLoginCount++;
        }

        if (FailedLoginCount >= MaxFailedAttempts)
        {
            LockedUntil = now.Add(LockDuration);
            FailedLoginCount = 0;
            FailedLoginWindowStartedAt = null;
        }
    }

    public void RecordSuccessfulLogin(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        FailedLoginWindowStartedAt = null;
        LastLoginAt = now;
    }

    public void SetPasswordHash(string hash, bool mustChange)
    {
        PasswordHash = hash ?? throw new ArgumentNullException(nameof(hash));
        MustChangePassword = mustChange;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void UpdateProfile(string fullName, Role role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("FULL_NAME_REQUIRED", "Full name is required.", 400);
        }

        FullName = fullName.Trim();
        Role = role;
    }
}
