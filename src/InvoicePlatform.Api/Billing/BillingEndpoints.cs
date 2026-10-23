using System.Text;
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Billing;
using InvoicePlatform.Contracts.Billing;

namespace InvoicePlatform.Api.Billing;

/// <summary>
/// The organisation's plan and credits, and Stripe's webhook. There is no
/// organisation id in any route: it comes from the session, and for the
/// webhook from what xChange itself stored at Stripe.
/// </summary>
public static class BillingEndpoints
{
    public const string SignatureHeader = "Stripe-Signature";

    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/billing", StatusAsync)
            .WithName("GetBilling")
            .RequireAuthenticatedUser();

        group.MapGet("/billing/plans", PlansAsync)
            .WithName("GetBillingPlans")
            .RequireAuthenticatedUser();

        group.MapPost("/billing/checkout", CheckoutAsync)
            .WithName("StartCheckout")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        group.MapPost("/billing/portal", PortalAsync)
            .WithName("OpenBillingPortal")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        // Anonymous by nature: Stripe calls it. The signature is the authentication.
        group.MapPost("/stripe/webhook", WebhookAsync)
            .WithName("StripeWebhook");

        return group;
    }

    private static async Task<IResult> StatusAsync(HttpContext context, BillingService billing, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var status = await billing.GetStatusAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        var account = status.Account;
        return Results.Ok(new BillingStatusResponse
        {
            Plan = account.PlanId,
            Status = account.Status,
            PeriodStart = account.PeriodStart,
            PeriodEnd = account.PeriodEnd,
            CancelAtPeriodEnd = account.CancelAtPeriodEnd,
            PeriodAllowance = account.PeriodAllowance,
            PeriodUsed = account.PeriodUsed,
            Credits = account.Credits,
            Trial = status.Usage.Trial,
            CanExport = status.CanExport,
            BaseLimit = status.Usage.Limit,
            BaseUsed = status.Usage.Used,
            HasCustomer = account.CustomerId is not null,
        });
    }

    private static async Task<IResult> PlansAsync(BillingService billing, CancellationToken cancellationToken)
    {
        var offer = await billing.GetPlansAsync(cancellationToken).ConfigureAwait(false);
        if (offer is null)
        {
            return Unavailable();
        }

        return Results.Ok(new BillingPlansResponse
        {
            Plans = [.. offer.Plans.Select(p => new BillingPlanResponse
            {
                Id = p.Plan.Id,
                Name = p.Plan.Name,
                MonthlyConversions = p.Plan.MonthlyConversions,
                Amount = p.Price?.Amount,
                Currency = p.Price?.Currency,
            })],
            Credits = new BillingCreditsResponse
            {
                Conversions = offer.Credits.Credits,
                Amount = offer.CreditsPrice?.Amount,
                Currency = offer.CreditsPrice?.Currency,
            },
        });
    }

    private static async Task<IResult> CheckoutAsync(HttpContext context, BillingService billing, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        // Read here, not bound as a parameter: binding runs before the guards.
        var request = await RequestBody.ReadAsync<CheckoutRequest>(context, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await billing.StartCheckoutAsync(user.OrganizationId, user.Email, request.Plan, request.Credits, cancellationToken)
            .ConfigureAwait(false);
        return ToResult(result);
    }

    private static async Task<IResult> PortalAsync(HttpContext context, BillingService billing, CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var result = await billing.StartPortalAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return ToResult(result);
    }

    private static async Task<IResult> WebhookAsync(HttpContext context, BillingService billing, CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes Stripe sent, so the body is read
        // raw - never through model binding, which would re-serialise it.
        string payload;
        using (var reader = new StreamReader(context.Request.Body, new UTF8Encoding(false)))
        {
            payload = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var outcome = await billing.HandleWebhookAsync(payload, context.Request.Headers[SignatureHeader].ToString(), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            // Stripe retries a 5xx, so events wait until the secret is set.
            WebhookOutcome.NotConfigured => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
            WebhookOutcome.InvalidSignature => Results.StatusCode(StatusCodes.Status400BadRequest),
            _ => Results.Ok(new { received = true }),
        };
    }

    private static IResult ToResult(CheckoutResult result)
    {
        var text = BillingApiText.Current;
        return result.Outcome switch
        {
            CheckoutOutcome.Started => Results.Ok(new RedirectResponse { Url = result.Url! }),
            CheckoutOutcome.Invalid => Results.Problem(
                detail: text.InvalidCheckoutDetail,
                title: ApiText.Current.InvalidRequestTitle,
                statusCode: StatusCodes.Status400BadRequest),
            CheckoutOutcome.AlreadySubscribed => Results.Problem(
                detail: text.AlreadySubscribedDetail,
                title: text.AlreadySubscribedTitle,
                statusCode: StatusCodes.Status409Conflict),
            CheckoutOutcome.NoCustomer => Results.Problem(
                detail: text.NoCustomerDetail,
                title: text.NoCustomerTitle,
                statusCode: StatusCodes.Status409Conflict),
            _ => Unavailable(),
        };
    }

    private static IResult Unavailable()
        => Results.Problem(
            detail: BillingApiText.Current.UnavailableDetail,
            title: BillingApiText.Current.UnavailableTitle,
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
