using InvoicePlatform.Application.Identity;

namespace InvoicePlatform.Infrastructure.Identity;

/// <summary>
/// bcrypt, the same algorithm Bookkeeping uses through PHP's
/// password_hash(PASSWORD_BCRYPT). Keeping the algorithm identical means a
/// password that is acceptable in one application is stored the same way in the
/// other, and neither has a weaker story than the other.
/// </summary>
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Work factor. 12 is roughly a quarter-second per hash on current
    /// hardware: slow enough to make offline guessing expensive, fast enough
    /// that a Lambda cold start plus a login stays comfortable.
    /// </summary>
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A stored value that is not a bcrypt hash is a failed verification,
            // not an exception for the caller to handle.
            return false;
        }
    }
}
