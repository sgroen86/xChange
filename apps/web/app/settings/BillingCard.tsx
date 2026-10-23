"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import {
  getBilling,
  getPlans,
  openPortal,
  startCheckout,
  type BillingPlans,
  type BillingStatus,
  type PlanId,
} from "../../lib/billing";
import { text, useLang, useText } from "../../lib/i18n";
import { formatMoney } from "../../lib/money";

/** After returning from Stripe: how often, and how long, to look for the webhook's result. */
const POLL_EVERY_MS = 4000;
const POLL_TIMES = 8;

/**
 * The organisation's plan: what it has and has used, for everyone; choosing a
 * plan, buying credits and managing the subscription, for an admin. Payment
 * happens at Stripe. Coming back from there proves nothing - only Stripe's
 * webhook changes the plan - so after a payment this polls for a short while.
 */
export function BillingCard({ canEdit }: { canEdit: boolean }) {
  const t = useText();
  const b = t.settings.billing;
  const lang = useLang();
  const locale = lang === "en" ? "en-GB" : "nl-NL";
  const showToast = useToast();

  const [status, setStatus] = useState<BillingStatus | null>(null);
  const [plans, setPlans] = useState<BillingPlans | null>(null);
  const [plansFailed, setPlansFailed] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [waiting, setWaiting] = useState(false);

  useEffect(() => {
    if (!isApiConfigured) return;
    let cancelled = false;
    getBilling()
      .then((loaded) => {
        if (!cancelled) setStatus(loaded);
      })
      .catch(() => undefined);
    getPlans()
      .then((loaded) => {
        if (!cancelled) setPlans(loaded);
      })
      .catch(() => {
        if (!cancelled) setPlansFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // Back from Stripe: say what happened, and look again until the webhook has
  // been. Read from the URL once, on arrival, then removed from it.
  useEffect(() => {
    if (!isApiConfigured) return;
    const params = new URLSearchParams(window.location.search);
    const outcome = params.get("billing");
    if (!outcome) return;

    params.delete("billing");
    const query = params.toString();
    window.history.replaceState(null, "", `${window.location.pathname}${query ? `?${query}` : ""}#billing`);

    if (outcome !== "success") {
      showToast(text().settings.billing.paymentCancelled, "info");
      return;
    }

    let first: string | null = null;
    let times = 0;
    let timer: number | null = null;
    const tick = () => {
      times++;
      getBilling()
        .then((loaded) => {
          setStatus(loaded);
          const now = JSON.stringify(loaded);
          first ??= now;
          const settled = now !== first || times >= POLL_TIMES;
          setWaiting(!settled);
          if (!settled) timer = window.setTimeout(tick, POLL_EVERY_MS);
        })
        .catch(() => setWaiting(false));
    };
    tick();
    return () => {
      if (timer !== null) window.clearTimeout(timer);
    };
  }, [showToast]);

  // Back from Stripe with the browser's back button: the page may come from
  // the back-forward cache with its buttons still waiting for the redirect.
  useEffect(() => {
    const restore = (event: PageTransitionEvent) => {
      if (event.persisted) setBusy(null);
    };
    window.addEventListener("pageshow", restore);
    return () => window.removeEventListener("pageshow", restore);
  }, []);

  if (!status) return null;

  const go = async (key: string, action: () => Promise<void>) => {
    if (busy) return;
    setBusy(key);
    try {
      await action();
      // The browser is on its way to Stripe; keep the buttons disabled.
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.common.apiStatus(0), "danger");
      setBusy(null);
    }
  };

  const date = (iso: string | null) =>
    iso ? new Date(iso).toLocaleDateString(locale, { day: "numeric", month: "long", year: "numeric" }) : "";
  const count = (n: number) => n.toLocaleString(locale);

  const planName = plans?.plans.find((p) => p.id === status.plan)?.name ?? status.plan;
  const running = status.plan !== null;
  const trialLeft = status.baseLimit !== null ? Math.max(0, status.baseLimit - status.baseUsed) : null;
  const periodLeft = status.periodAllowance !== null ? Math.max(0, status.periodAllowance - status.periodUsed) : 0;
  const exhausted = trialLeft !== null && trialLeft === 0 && periodLeft === 0 && status.credits === 0;

  const headline = running ? planName : status.trial ? b.trial : status.baseLimit === null && !status.hasCustomer ? b.unlimited : b.noPlan;
  const statusLabel = status.status ? (b.status[status.status] ?? status.status) : null;

  return (
    <div className="card" id="billing" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{b.title}</h3>
        <span style={{ display: "flex", gap: "var(--space-2)" }}>
          <span className={`badge ${running ? "badge--success" : "badge--neutral"}`}>{headline}</span>
          {running && statusLabel && status.status !== "active" && (
            <span className={`badge ${status.status === "past_due" ? "badge--danger" : "badge--warning"}`}>{statusLabel}</span>
          )}
        </span>
      </div>
      <div className="card__body">
        {waiting && (
          <div className="alert alert--info" style={{ marginBottom: "var(--space-4)" }}>
            <Icon name="clock" />
            <div>{b.paymentReceived}</div>
          </div>
        )}

        {running && status.periodAllowance !== null && (
          <>
            <p className="text-sm">{b.periodUsed(Math.min(status.periodUsed, status.periodAllowance), status.periodAllowance)}</p>
            <Meter used={status.periodUsed} limit={status.periodAllowance} />
            {status.periodEnd && (
              <p className="text-soft text-sm" style={{ marginTop: "var(--space-2)" }}>
                {status.cancelAtPeriodEnd ? b.ends(date(status.periodEnd)) : b.renews(date(status.periodEnd))}
              </p>
            )}
          </>
        )}

        {status.status === "past_due" && (
          <div className="alert alert--warning" style={{ marginTop: "var(--space-4)" }}>
            <Icon name="info" />
            <div>{b.pastDue}</div>
          </div>
        )}

        {!running && status.trial && status.baseLimit !== null && (
          <>
            <p className="text-sm">{b.trialUsed(Math.min(status.baseUsed, status.baseLimit), status.baseLimit)}</p>
            <Meter used={status.baseUsed} limit={status.baseLimit} />
          </>
        )}

        {(status.credits > 0 || status.hasCustomer) && (
          <div className="form__row" style={{ marginTop: "var(--space-4)" }}>
            <div>
              <div className="text-sm">
                <strong>{b.credits}:</strong> {b.creditsLeft(status.credits)}
              </div>
              <div className="form-help">{b.creditsHelp}</div>
            </div>
          </div>
        )}

        {exhausted && (
          <div className="alert alert--warning" style={{ marginTop: "var(--space-4)" }}>
            <Icon name="info" />
            <div>{b.exhausted}</div>
          </div>
        )}

        {plans && (
          <>
            <h4 style={{ marginTop: "var(--space-5)", marginBottom: "var(--space-3)" }}>{b.choosePlan}</h4>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "var(--space-3)" }}>
              {plans.plans.map((plan) => {
                const current = plan.id === status.plan;
                return (
                  <div key={plan.id} className="stat-card" style={current ? { borderColor: "var(--color-primary)" } : undefined}>
                    <div className="stat-card__label">{plan.name}</div>
                    <div className="stat-card__value">{plan.amount ? formatMoney(plan.amount, plan.currency, lang) : "—"}</div>
                    <div className="stat-card__change">{b.perMonth}</div>
                    <p className="text-sm" style={{ margin: "var(--space-2) 0" }}>
                      {b.conversionsPerMonth(count(plan.monthlyConversions))}
                    </p>
                    {current ? (
                      <span className="badge badge--success">{b.current}</span>
                    ) : (
                      canEdit &&
                      !running && (
                        <button
                          type="button"
                          className="btn btn--primary btn--sm"
                          disabled={busy !== null || !plan.amount}
                          onClick={() => go(plan.id, () => startCheckout({ plan: plan.id as PlanId }))}
                        >
                          {busy === plan.id ? b.redirecting : b.choose}
                        </button>
                      )
                    )}
                  </div>
                );
              })}
            </div>
            {canEdit && running && <p className="text-soft text-sm" style={{ marginTop: "var(--space-2)" }}>{b.switchInPortal}</p>}
          </>
        )}

        {canEdit && plansFailed && (
          <p className="text-soft text-sm" style={{ marginTop: "var(--space-4)" }}>
            {b.pricesUnavailable}
          </p>
        )}

        {canEdit ? (
          <div className="form__actions" style={{ marginTop: "var(--space-5)", flexWrap: "wrap" }}>
            {plans && (
              <button
                type="button"
                className="btn btn--secondary"
                disabled={busy !== null || !plans.credits.amount}
                onClick={() => go("credits", () => startCheckout({ credits: true }))}
                title={b.buyCreditsHelp(plans.credits.conversions)}
              >
                <Icon name="euro" />{" "}
                {busy === "credits"
                  ? b.redirecting
                  : `${b.buyCredits(plans.credits.conversions)}${plans.credits.amount ? ` · ${formatMoney(plans.credits.amount, plans.credits.currency, lang)}` : ""}`}
              </button>
            )}
            {status.hasCustomer && (
              <button type="button" className="btn btn--secondary" disabled={busy !== null} onClick={() => go("portal", openPortal)}>
                <Icon name="settings" /> {busy === "portal" ? b.redirecting : b.manage}
              </button>
            )}
          </div>
        ) : (
          <p className="text-soft text-sm" style={{ marginTop: "var(--space-4)" }}>
            {b.adminOnly}
          </p>
        )}
        {canEdit && plans && <div className="form-help">{b.buyCreditsHelp(plans.credits.conversions)} {plans.credits.amount ? b.oneOff : ""}</div>}
      </div>
    </div>
  );
}

function Meter({ used, limit }: { used: number; limit: number }) {
  const shown = Math.min(used, limit);
  const percent = Math.round((shown / Math.max(1, limit)) * 100);
  return (
    <div
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={limit}
      aria-valuenow={shown}
      style={{ height: 8, borderRadius: 4, background: "var(--color-surface-sunken)", overflow: "hidden", marginTop: "var(--space-2)" }}
    >
      <div style={{ width: `${percent}%`, height: "100%", background: "var(--color-primary)" }} />
    </div>
  );
}
