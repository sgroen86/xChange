"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { Icon } from "../../../components/Icon";
import { getToken } from "../../../lib/auth";
import { getBilling, startCheckout } from "../../../lib/billing";
import { useLang, useText } from "../../../lib/i18n";
import { formatMoney } from "../../../lib/money";
import { SIGNUP_PLANS, checkoutPurchase, forgetPlan, rememberedPlan, type SignupPlanId } from "../../../lib/signupPlan";

/**
 * After sign-up, confirmation and MFA: the plan chosen on the website is paid
 * before the app opens. Stripe returns to Settings on success (the app proper)
 * and on cancel; without a remembered plan there is nothing to pay and the app
 * opens on the trial.
 */
export default function PaymentClient() {
  const router = useRouter();
  const t = useText();
  const lang = useLang();
  const p = t.auth.payment;
  const [plan] = useState<SignupPlanId | null>(() => rememberedPlan());
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);

  /** Checkout for the plan, or straight to the app when a plan already runs. Leaves for Stripe. */
  const pay = (chosen: SignupPlanId): Promise<void> =>
    getBilling().then((status) => {
      // A running plan is changed in the portal, not with a second Checkout.
      if (chosen !== "credits" && status.plan !== null) {
        forgetPlan();
        router.replace("/upload");
        return;
      }
      forgetPlan();
      return startCheckout(checkoutPurchase(chosen));
    });

  const failed = (cause: unknown) => setError(cause instanceof Error ? cause.message : t.common.somethingFailed);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    if (!getToken()) {
      router.replace("/login");
      return;
    }
    if (!plan) {
      router.replace("/upload");
      return;
    }
    pay(plan).catch(failed);
    // Runs once on arrival.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!plan) return null;
  const chosen = SIGNUP_PLANS[plan];

  return (
    <div className="card">
      <div className="card__body">
        <h1 className="standalone__title">{p.title}</h1>
        <p className="text-soft" style={{ marginBottom: "var(--space-4)" }}>
          {chosen.name ?? t.auth.signup.creditsName(chosen.conversions)} ·{" "}
          <strong>{formatMoney(chosen.amount, chosen.currency, lang)}</strong> {chosen.oneOff ? t.settings.billing.oneOff : t.settings.billing.perMonth}
        </p>
        {error ? (
          <>
            <div className="alert alert--danger" style={{ marginBottom: "var(--space-4)" }}>
              <Icon name="x" />
              <div>{error}</div>
            </div>
            <div className="form__actions">
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => {
                  setError(null);
                  pay(plan).catch(failed);
                }}
              >
                {p.retry}
              </button>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => {
                  forgetPlan();
                  router.replace("/upload");
                }}
              >
                {p.trialInstead}
              </button>
            </div>
          </>
        ) : (
          <div className="alert alert--info">
            <Icon name="clock" />
            <div>{p.going}</div>
          </div>
        )}
      </div>
    </div>
  );
}
