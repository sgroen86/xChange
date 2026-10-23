using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Usage;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Identity;

/// <summary>Pending sign-ups, keyed by the hash of their confirmation code.</summary>
public interface ISignupStore
{
    Task SaveAsync(PendingSignup signup, CancellationToken cancellationToken = default);

    /// <summary>Reads and removes it in one step, so a link confirms once.</summary>
    Task<PendingSignup?> TryTakeAsync(string codeHash, CancellationToken cancellationToken = default);
}

/// <summary>Sends plain-text email, from the platform's own address.</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default);
}

/// <param name="AppUrl">Where the app lives, for the link in the confirmation mail.</param>
public sealed record SignupSettings(string AppUrl);

public enum SignupOutcome
{
    /// <summary>A confirmation mail went out (or, for a known address, a "you already have an account" mail).</summary>
    MailSent,
    Closed,
    Invalid,
    Locked,
}

public sealed record SignupResult(SignupOutcome Outcome, string? Error = null);

/// <summary>
/// Self sign-up. Someone enters their details; xChange mails a link; the link
/// creates their organisation with them as admin, on a trial of
/// <see cref="TrialConversions"/> conversion (downloading needs a plan or
/// credits, see <see cref="Billing.ExportEntitlement"/>), and signs them in
/// straight away (MFA is optional for them). Closed until
/// the platform administrator opens it.
///
/// The answer to a sign-up is the same whether the address is new or already
/// has an account (the mail differs), so the form does not reveal who uses
/// xChange. Every attempt counts towards the same per-address lockout as login,
/// and at most <see cref="LoginAttempts.MaxSignupMails"/> mails go to one
/// address per <see cref="LoginAttempts.SignupMailWindow"/>.
/// </summary>
public sealed class SignupService(
    ISignupStore signups,
    IUserStore users,
    ILoginAttemptStore attempts,
    IPasswordHasher passwords,
    IClock clock,
    IPlatformStore platform,
    IEmailSender email,
    IUsageMeter usage,
    AuthService auth,
    SignupSettings settings)
{
    /// <summary>A self-signed-up organisation's free conversions. Existing organisations keep the limit they have.</summary>
    public const int TrialConversions = 1;

    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken = default)
        => (await platform.GetSettingsAsync(cancellationToken).ConfigureAwait(false)).SignupOpen;

    public async Task<SignupResult> StartAsync(
        string name,
        string emailAddress,
        string password,
        string organizationName,
        string countryCode,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        if (!await IsOpenAsync(cancellationToken).ConfigureAwait(false))
        {
            return new SignupResult(SignupOutcome.Closed, IdentityText.Current.SignupClosed);
        }

        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return new SignupResult(SignupOutcome.Locked, IdentityText.Current.TooManyAttempts);
        }

        var error = AuthService.ValidateCredentials(emailAddress, password, name)
            ?? (string.IsNullOrWhiteSpace(organizationName) ? IdentityText.Current.CompanyNameRequired : null)
            ?? (Countries.IsSupported(countryCode) ? null : IdentityText.Current.ChooseCountry);
        if (error is not null)
        {
            return new SignupResult(SignupOutcome.Invalid, error);
        }

        // Every sign-up counts, so the form cannot be used to send mail in bulk.
        await auth.RecordAttemptAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);

        // And per recipient, so one inbox cannot be flooded from many addresses.
        // Counted for new and known addresses alike, like everything else here.
        var address = User.NormalizeEmail(emailAddress);
        var mailKey = LoginAttempts.SignupMailKey(address);
        var mails = await attempts.GetAsync(mailKey, cancellationToken).ConfigureAwait(false);
        if (mails is not null && mails.IsLocked(now))
        {
            return new SignupResult(SignupOutcome.Locked, IdentityText.Current.TooManySignupMails);
        }

        await attempts.SaveAsync(
            LoginAttempts.AfterFailure(mailKey, mails, now, LoginAttempts.MaxSignupMails, LoginAttempts.SignupMailWindow),
            cancellationToken).ConfigureAwait(false);

        // Hashed whether or not the address is known: skipping bcrypt for a
        // known one would make that answer measurably faster.
        var passwordHash = passwords.Hash(password);

        // The mail goes out within this request, so it is in the language the person signed up in.
        var text = IdentityText.Current;
        if (await users.FindByEmailAsync(address, cancellationToken).ConfigureAwait(false) is not null)
        {
            await email.SendAsync(address, text.KnownAddressMailSubject, text.KnownAddressMailBody(settings.AppUrl), cancellationToken)
                .ConfigureAwait(false);
            return new SignupResult(SignupOutcome.MailSent);
        }

        var code = Invitation.NewCode();
        await signups.SaveAsync(
            new PendingSignup(
                Invitation.HashOf(code),
                address,
                name.Trim(),
                passwordHash,
                organizationName.Trim(),
                countryCode.Trim().ToUpperInvariant(),
                now,
                now + PendingSignup.Lifetime),
            cancellationToken).ConfigureAwait(false);

        await email.SendAsync(
            address,
            text.ConfirmationMailSubject,
            text.ConfirmationMailBody(
                name.Trim(),
                organizationName.Trim(),
                $"{settings.AppUrl}/signup/confirm/?code={Uri.EscapeDataString(code)}",
                TrialConversions),
            cancellationToken).ConfigureAwait(false);

        return new SignupResult(SignupOutcome.MailSent);
    }

    /// <summary>
    /// The link in the mail: creates the organisation (trial) and its first
    /// user (admin), and returns the session: MFA is optional for a company
    /// admin, see <see cref="UserMfa.IsMandatoryFor"/>.
    /// </summary>
    public async Task<AuthResult> ConfirmAsync(string code, string clientKey, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var record = await attempts.GetAsync(clientKey, cancellationToken).ConfigureAwait(false);
        if (record is not null && record.IsLocked(now))
        {
            return AuthResult.Locked();
        }

        var signup = string.IsNullOrWhiteSpace(code)
            ? null
            : await signups.TryTakeAsync(Invitation.HashOf(code), cancellationToken).ConfigureAwait(false);
        if (signup is null || signup.IsExpired(now))
        {
            await auth.RecordAttemptAsync(clientKey, record, now, cancellationToken).ConfigureAwait(false);
            return AuthResult.Fail(IdentityText.Current.InvalidConfirmationLink);
        }

        var userId = Guid.NewGuid().ToString("n");
        var organization = new Organization
        {
            OrganizationId = Guid.NewGuid().ToString("n"),
            Name = signup.OrganizationName,
            CountryCode = signup.CountryCode,
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        var user = new User
        {
            Id = userId,
            OrganizationId = organization.OrganizationId,
            Email = signup.Email,
            PasswordHash = signup.PasswordHash,
            Name = signup.Name,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now,
        };

        try
        {
            await users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // The address got an account between sign-up and confirmation.
            return AuthResult.Fail(IdentityText.Current.AccountWithEmailExistsLogIn);
        }

        // The limit before the organisation exists in the list, so it is never briefly unlimited.
        await usage.SetLimitAsync(organization.OrganizationId, TrialConversions, trial: true, cancellationToken).ConfigureAwait(false);
        await platform.SaveOrganizationAsync(organization, cancellationToken).ConfigureAwait(false);

        return await auth.SignInAsync(user, clientKey, cancellationToken).ConfigureAwait(false);
    }
}
