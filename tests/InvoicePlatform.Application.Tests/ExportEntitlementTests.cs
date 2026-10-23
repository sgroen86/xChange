using InvoicePlatform.Application.Billing;
using InvoicePlatform.Application.Usage;

namespace InvoicePlatform.Application.Tests;

/// <summary>Who may take an e-invoice away: a running plan, credits left, or no limit at all.</summary>
public class ExportEntitlementTests
{
    private static readonly UsageSummary Trial = new(0, 1, Trial: true);

    private static BillingAccount Account(string? status = null, int credits = 0)
        => BillingAccount.None with { Status = status, Credits = credits };

    [Fact]
    public void Without_a_plan_or_credits_a_limited_organisation_may_not_export()
    {
        Assert.False(ExportEntitlement.Allows(Account(), Trial));
        Assert.False(ExportEntitlement.Allows(Account(), new UsageSummary(0, 5)));
    }

    [Theory]
    [InlineData(SubscriptionStatuses.Active)]
    [InlineData(SubscriptionStatuses.Trialing)]
    [InlineData(SubscriptionStatuses.PastDue)]
    public void A_running_subscription_may_export(string status)
        => Assert.True(ExportEntitlement.Allows(Account(status), Trial));

    [Theory]
    [InlineData("canceled")]
    [InlineData(SubscriptionStatuses.Unpaid)]
    [InlineData("incomplete_expired")]
    public void An_ended_subscription_with_credits_left_may_export(string status)
        => Assert.True(ExportEntitlement.Allows(Account(status, credits: 3), Trial with { SubscribedOnce = true }));

    [Theory]
    [InlineData("canceled")]
    [InlineData(SubscriptionStatuses.Unpaid)]
    public void An_ended_subscription_without_credits_may_not_export(string status)
        => Assert.False(ExportEntitlement.Allows(Account(status), Trial with { SubscribedOnce = true }));

    [Fact]
    public void Credits_alone_may_export()
        => Assert.True(ExportEntitlement.Allows(Account(credits: 1), Trial));

    [Fact]
    public void An_organisation_without_a_limit_may_export()
        => Assert.True(ExportEntitlement.Allows(Account(), new UsageSummary(12, null)));

    [Fact]
    public void Without_a_limit_but_after_a_plan_ended_it_follows_the_plan()
    {
        var usage = new UsageSummary(12, null, SubscribedOnce: true);

        Assert.False(ExportEntitlement.Allows(Account("canceled"), usage));
        Assert.True(ExportEntitlement.Allows(Account(SubscriptionStatuses.Active), usage));
    }

    [Fact]
    public async Task Having_paid_once_grants_nothing_by_itself()
    {
        var store = new FakeUsageMeter();
        await store.SetLimitAsync("org-a", 1, trial: true);
        store["org-a"].PaidOnce = true;
        store["org-a"].SubscribedOnce = true;
        store["org-a"].Status = "canceled";

        Assert.False((await store.GetAsync("org-a")).Trial);
        Assert.False(await ExportEntitlement.AllowsAsync(store, store, "org-a"));
    }
}
