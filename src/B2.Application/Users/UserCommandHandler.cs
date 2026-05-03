using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Users;
using B2.Shared.Contracts.Users;

namespace B2.Application.Users;

public sealed class UserCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public UserCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<UserDto> CreateAsync(Guid actorUserId, UserCreateRequest request, CancellationToken cancellationToken)
    {
        var username = new Username(request.Username);
        if (await _users.UsernameExistsAsync(username.Value, cancellationToken))
        {
            throw new DomainException("USERNAME_TAKEN", "Username already exists.", 409);
        }

        var user = new User(
            Guid.NewGuid(),
            username,
            _hasher.HashPassword(request.TemporaryPassword),
            request.FullName,
            RoleMapping.ToDomain(request.Role),
            _clock.UtcNow,
            mustChangePassword: true);

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var after = System.Text.Json.JsonSerializer.Serialize(new { user.Username, user.Role, user.FullName });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Create, nameof(User), user.Id, null, after, null));

        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid actorUserId, Guid id, UserUpdateRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("USER_NOT_FOUND", "User not found.", 404);

        var before = System.Text.Json.JsonSerializer.Serialize(new { user.FullName, user.Role, user.IsActive });

        if (request.FullName is not null || request.Role is not null)
        {
            user.UpdateProfile(
                request.FullName ?? user.FullName,
                request.Role is { } r ? RoleMapping.ToDomain(r) : user.Role);
        }

        if (request.IsActive is { } active)
        {
            if (active)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { user.FullName, user.Role, user.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(User), user.Id, before, after, null));

        return user.ToDto();
    }

    public async Task ResetPasswordAsync(Guid actorUserId, Guid id, PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("USER_NOT_FOUND", "User not found.", 404);

        var before = System.Text.Json.JsonSerializer.Serialize(new { mustChangePassword = user.MustChangePassword });
        user.SetPasswordHash(_hasher.HashPassword(request.TemporaryPassword), mustChange: true);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { mustChangePassword = user.MustChangePassword });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(User), user.Id, before, after, null));
    }

    public async Task<(IReadOnlyList<UserDto> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await _users.ListAsync(page, pageSize, cancellationToken);
        return (items.Select(u => u.ToDto()).ToList(), total);
    }
}
