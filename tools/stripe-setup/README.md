# Stripe setup

Prepares a Stripe account for xChange. Run it once per account: first with a test key, later with
the live key. It is idempotent: a second run leaves what already exists alone.

It creates:

- **Products and prices**, found by `lookup_key`, in EUR with `tax_behavior = exclusive`:

  | lookup_key | product id | price |
  |---|---|---|
  | `xchange_starter_monthly` | `xchange_starter` | € 19 / month |
  | `xchange_pro_monthly` | `xchange_pro` | € 49 / month |
  | `xchange_business_monthly` | `xchange_business` | € 129 / month |
  | `xchange_credits_20` | `xchange_credits` | € 30 once |

  An existing price is never changed. To change a price later, create a new price in the
  dashboard and move the lookup key to it ("transfer lookup key"); the API picks it up within five
  minutes, no deploy.
- **A customer portal configuration** (metadata `xchange=portal`, which the API looks for):
  switching between the three plans with proration, cancelling at the period end, updating the
  payment method, and the invoice history.
- **The webhook endpoint** for the URL you give, with the events the API handles
  (`checkout.session.completed`, `checkout.session.async_payment_succeeded`,
  `customer.subscription.created/updated/deleted`, `invoice.paid`, `invoice.payment_failed`), on
  the Stripe API version of the Stripe.net package the API uses.

## Run

```bash
export STRIPE_SECRET_KEY=sk_test_...        # never commit it; unset it afterwards
dotnet run --project tools/stripe-setup -- \
  https://<api function url>/api/v1/stripe/webhook \
  ./stripe-webhook-secret.txt
```

On Windows PowerShell: `$env:STRIPE_SECRET_KEY = "sk_test_..."` before the same `dotnet run`.

The webhook's **signing secret is written only to the file** (which must not exist yet), never to
the console. Stripe returns it only when the endpoint is created: on a later run the endpoint
already exists, nothing is written, and the secret can be revealed or rolled in the dashboard
(Developers → Webhooks).

## Then store the keys

The API reads `xchange/stripe` from Secrets Manager (`eu-central-1`) at runtime:

```bash
aws secretsmanager create-secret --name xchange/stripe \
  --secret-string "{\"secretKey\":\"sk_test_...\",\"webhookSecret\":\"$(cat stripe-webhook-secret.txt)\"}"
rm stripe-webhook-secret.txt
```

(`put-secret-value --secret-id xchange/stripe` instead of `create-secret` when it already exists.)
A restricted key (`rk_...`) works as `secretKey` if it may write customers, checkout sessions and
portal sessions and read prices and portal configurations.

Until the secret exists, the plan buttons answer "payments are not set up yet" and the webhook
answers 503, which Stripe retries for three days. A secret without `webhookSecret` is read again
every five minutes, so adding it later needs no deploy.
