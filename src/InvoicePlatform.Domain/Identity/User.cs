namespace InvoicePlatform.Domain.Identity;

/// <summary>
/// Roles mirror the Bookkeeping app's fin_users.role enum so the two systems
/// describe permissions the same way, even though they hold separate users.
/// </summary>
public enum UserRole
{
    /// <summary>Full access, including managing other users.</summary>
    Admin,

    /// <summary>Normal use: upload, review, export.</summary>
    User,

    /// <summary>May look but not change anything.</summary>
    ReadOnly,
}

/// <summary>
/// An xChange user.
///
/// xChange keeps its own accounts: it is a separate application that happens to
/// share a domain with Bookkeeping today. The shape follows Bookkeeping's
/// fin_users table - email, bcrypt hash, name, role, active flag, last login -
/// so the login behaves identically, without the two sharing a store.
///
/// <see cref="OrganizationId"/> is the tenant key required by CLAUDE.md: every
/// tenant-owned record carries it, and every query filters on it.
/// </summary>
public sealed record User
{
    public required string Id { get; init; }

    public required string OrganizationId { get; init; }

    /// <summary>Lower-cased and trimmed. The natural key for login.</summary>
    public required string Email { get; init; }

    /// <summary>bcrypt. Never logged, never returned over the wire.</summary>
    public required string PasswordHash { get; init; }

    public required string Name { get; init; }

    public UserRole Role { get; init; } = UserRole.User;

    /// <summary>A deactivated account keeps its history but cannot log in.</summary>
    public bool IsActive { get; init; } = true;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
