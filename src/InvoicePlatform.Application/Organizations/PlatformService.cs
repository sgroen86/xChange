using InvoicePlatform.Application.Identity;
using InvoicePlatform.Domain.Identity;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// What the platform administrator does: create organisations, change their
/// name or country, and hand out the invitation for an organisation's first
/// user. Who may call this is the API's concern (platform administrators only).
/// </summary>
public sealed class PlatformService(IPlatformStore store, IUserStore users, IClock clock)
{
    public async Task<IReadOnlyList<OrganizationSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await store.ListOrganizationsAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.UtcNow;
        var summaries = new List<OrganizationSummary>();

        foreach (var organization in organizations.OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var userCount = await store.CountUsersAsync(organization.OrganizationId, cancellationToken).ConfigureAwait(false);
            var invitationPending = organization.PendingInvitationHash is not null
                && organization.PendingInvitationExpiresAt is { } expires && expires > now;
            summaries.Add(new OrganizationSummary(organization, userCount, invitationPending));
        }

        return summaries;
    }

    public async Task<PlatformResult> CreateOrganizationAsync(
        string createdByUserId,
        string name,
        string countryCode,
        string firstUserName,
        string firstUserEmail,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateOrganization(name, countryCode) ?? await ValidateInviteeAsync(firstUserName, firstUserEmail, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return PlatformResult.Fail(error);
        }

        var organization = new Organization
        {
            OrganizationId = Guid.NewGuid().ToString("n"),
            Name = name.Trim(),
            CountryCode = Countries.Find(countryCode)!.Code,
            CreatedAt = clock.UtcNow,
            CreatedByUserId = createdByUserId,
        };

        return await InviteAsync(organization, firstUserName, firstUserEmail, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PlatformResult> UpdateOrganizationAsync(
        string organizationId,
        string name,
        string countryCode,
        CancellationToken cancellationToken = default)
    {
        var existing = await store.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return PlatformResult.NotFound;
        }

        var error = ValidateOrganization(name, countryCode);
        if (error is not null)
        {
            return PlatformResult.Fail(error);
        }

        var updated = existing with { Name = name.Trim(), CountryCode = Countries.Find(countryCode)!.Code };
        await store.SaveOrganizationAsync(updated, cancellationToken).ConfigureAwait(false);
        return new PlatformResult(PlatformOutcome.Succeeded, Organization: updated);
    }

    public Task<PlatformSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        => store.GetSettingsAsync(cancellationToken);

    /// <param name="extractionModel">One of <see cref="ExtractionModels.All"/>, or null for the configured default.</param>
    /// <returns>Null when saved; otherwise why not.</returns>
    public async Task<string?> UpdateSettingsAsync(string? extractionModel, CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(extractionModel) ? null : extractionModel.Trim();
        if (model is not null && !ExtractionModels.IsAllowed(model))
        {
            return "Kies een van de aangeboden modellen.";
        }

        var current = await store.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        await store.SaveSettingsAsync(current with { ExtractionModel = model }, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>A new invitation for the organisation; the previous one stops working.</summary>
    public async Task<PlatformResult> ReissueInvitationAsync(
        string organizationId,
        string name,
        string email,
        CancellationToken cancellationToken = default)
    {
        var existing = await store.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return PlatformResult.NotFound;
        }

        var error = await ValidateInviteeAsync(name, email, cancellationToken).ConfigureAwait(false);
        return error is not null
            ? PlatformResult.Fail(error)
            : await InviteAsync(existing, name, email, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlatformResult> InviteAsync(
        Organization organization, string name, string email, CancellationToken cancellationToken)
    {
        if (organization.PendingInvitationHash is { } previous)
        {
            await store.TryDeleteInvitationAsync(previous, cancellationToken).ConfigureAwait(false);
        }

        var now = clock.UtcNow;
        var code = Invitation.NewCode();
        var invitation = new Invitation(
            Invitation.HashOf(code),
            organization.OrganizationId,
            User.NormalizeEmail(email),
            name.Trim(),
            now,
            now + Invitation.Lifetime);

        var updated = organization with
        {
            PendingInvitationHash = invitation.CodeHash,
            PendingInvitationEmail = invitation.Email,
            PendingInvitationExpiresAt = invitation.ExpiresAt,
        };

        await store.SaveInvitationAsync(invitation, cancellationToken).ConfigureAwait(false);
        await store.SaveOrganizationAsync(updated, cancellationToken).ConfigureAwait(false);

        // The code is returned once and never stored: only its hash is.
        return new PlatformResult(PlatformOutcome.Succeeded, Organization: updated, InvitationCode: code);
    }

    private static string? ValidateOrganization(string name, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naam van de organisatie is verplicht.";
        }

        return Countries.IsSupported(countryCode) ? null : "Kies een land uit de lijst.";
    }

    private async Task<string?> ValidateInviteeAsync(string name, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Naam van de gebruiker is verplicht.";
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return "Ongeldig e-mailadres.";
        }

        var existing = await users.FindByEmailAsync(User.NormalizeEmail(email), cancellationToken).ConfigureAwait(false);
        return existing is null ? null : "Er bestaat al een account met dit e-mailadres.";
    }
}

public sealed record OrganizationSummary(Organization Organization, int UserCount, bool InvitationPending);

public enum PlatformOutcome
{
    Succeeded,
    Invalid,
    NotFound,
}

/// <param name="InvitationCode">Only when an invitation was made; shown to the administrator once.</param>
public sealed record PlatformResult(
    PlatformOutcome Outcome,
    string? Error = null,
    Organization? Organization = null,
    string? InvitationCode = null)
{
    public static readonly PlatformResult NotFound = new(PlatformOutcome.NotFound);

    public static PlatformResult Fail(string error) => new(PlatformOutcome.Invalid, error);
}
