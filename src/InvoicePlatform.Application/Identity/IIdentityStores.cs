using InvoicePlatform.Domain.Identity;

namespace InvoicePlatform.Application.Identity;

/// <summary>
/// Persistence for accounts. Declared here so the login logic never learns
/// which database is behind it (CLAUDE.md).
/// </summary>
public interface IUserStore
{
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAsync(
        string organizationId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>True when no account exists at all, which gates first-run setup.</summary>
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    /// <summary>Fails rather than overwriting when the email is already taken.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task UpdateLastLoginAsync(
        string organizationId,
        string userId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public interface ISessionStore
{
    Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default);

    Task<AuthSession?> FindAsync(string token, CancellationToken cancellationToken = default);

    Task DeleteAsync(string token, CancellationToken cancellationToken = default);
}

public interface ILoginAttemptStore
{
    Task<LoginAttempts?> GetAsync(string clientKey, CancellationToken cancellationToken = default);

    Task SaveAsync(LoginAttempts attempts, CancellationToken cancellationToken = default);

    Task ClearAsync(string clientKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Password hashing. An interface so the algorithm can change without the login
/// logic knowing, and so tests can substitute something fast.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>Supplies the current time, so expiry and lockout are testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
