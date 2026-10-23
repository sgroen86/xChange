# xChange: betalen via Stripe (abonnementen, tegoed, limieten)

**Datum:** 2026-10-23
**Status:** product en prijzen goedgekeurd door de eigenaar; de technische uitwerking hieronder niet
besproken

## Product (beslissing eigenaar)

| Plan | Prijs (EUR excl. btw) | Conversies | Stripe lookup_key |
|---|---|---|---|
| Proef | gratis | 10 in totaal (bestaand) | — |
| Starter | € 19 / maand | 50 / maand | `xchange_starter_monthly` |
| Pro | € 49 / maand | 250 / maand | `xchange_pro_monthly` |
| Business | € 129 / maand | 1.000 / maand | `xchange_business_monthly` |
| Losse tegoed | € 30 eenmalig | +20 | `xchange_credits_20` |

- Maandconversies gelden per factuurperiode van Stripe en schuiven niet door.
- Gekocht tegoed komt erbovenop, wordt pas ná de maandconversies gebruikt en vervalt nooit.
- Geen Stripe Tax nog: prijzen `tax_behavior = exclusive`, btw later.
- Organisaties zonder limiet (door de platformbeheerder aangemaakt) blijven onbeperkt, tot ze een
  abonnement nemen. Vanaf dat moment gelden de regels hieronder voor hen ook, ook na opzeggen.

## Grenzen

- `IBillingProvider` (Application) is alles wat de rest van xChange van Stripe ziet: klant maken,
  Checkout-sessie (abonnement of eenmalig), portaalsessie, prijzen per lookup_key, en een webhook
  controleren en vertalen naar eigen `BillingEvent`-typen. `StripeBillingProvider`
  (Infrastructure, Stripe.net) is de enige plek met Stripe-typen.
- Prijzen worden bij Stripe opgezocht op lookup_key en 5 minuten bewaard: een prijs wijzigen in
  Stripe vraagt geen deploy. De catalogus (`BillingCatalogue`) kent alleen plan-id, conversies en
  lookup_key, nooit een bedrag.
- Bedragen gaan als `decimal` door de code en als string over de lijn.

## Geheim

Secrets Manager `xchange/stripe`: `{"secretKey": "...", "webhookSecret": "..."}`, tijdens het
draaien gelezen door de rol van de API (alleen `GetSecretValue` op `xchange/stripe-??????`), net als
de Anthropic-sleutel. Lokaal kan `Stripe:SecretKey` / `Stripe:WebhookSecret` uit configuratie.

- Geen geheim of geen `secretKey`: betaalknoppen geven 503 ("betalen is nog niet ingericht").
- Geen `webhookSecret`: de webhook geeft 503, zodat Stripe blijft herhalen tot het er is. Een
  onvolledig geheim wordt na 5 minuten opnieuw gelezen; een nieuwe deploy is niet nodig.

## Opslag (tabel `xchange-invoices`)

Alles op het bestaande item van de organisatie, zodat één lezing de hele stand geeft en elke
afboeking één voorwaardelijke update is:

```
pk = ORG#<org>  sk = ALLOWANCE
  used, limit, plan="trial"          bestaand: proef / limiet / onbeperkt
  periodAllowance, periodUsed         maandconversies van het abonnement
  periodStart, periodEnd, periodResetFrom
  credits                             gekocht tegoed
  stripeCustomerId, stripeSubscriptionId, subscriptionPlan, subscriptionStatus,
  cancelAtPeriodEnd, subscriptionEventAt
  subscribedOnce, paidOnce            (zie verbruik en proef)

pk = STRIPE#CUSTOMER#<customerId>  sk = CUSTOMER    organizationId   (webhook -> organisatie)
pk = STRIPE#EVENT#<eventId>        sk = EVENT       verwerkt; TTL 30 dagen (idempotentie)
```

De twee `STRIPE#`-items zijn verwijzingen, geen tenantgegevens. Er is één Stripe-klant per
organisatie; tweemaal tegelijk afrekenen maakt hooguit een ongebruikte klant in Stripe.

## Verbruik (`IUsageMeter`)

`TryConsumeAsync` geeft terug uit welke bak de conversie kwam, of `null` (geweigerd):

1. **Abonnement**: `periodUsed + 1` als `periodUsed < periodAllowance`.
2. **Tegoed**: `credits - 1` als `credits > 0`.
3. **Zoals vandaag**: `used + 1` als `used < limit`, of als er geen limiet is én de organisatie nooit
   een abonnement had (`subscribedOnce`).
4. Anders geweigerd (402, met een link naar het abonnement).

Elke stap is één voorwaardelijke update: twee uploads tegelijk kunnen niet beide de laatste nemen.
`RefundAsync(org, bak)` geeft terug aan dezelfde bak. De maandteller `USAGE#yyyy-MM` en het
kostengrootboek blijven ongewijzigd.

**Proef**: een organisatie is in proef zolang `plan = "trial"` en ze nooit betaalde
(`paidOnce`). Een actief abonnement of een gekocht tegoed zet `paidOnce`; daarna mag ze downloaden,
ook na opzeggen.

## Endpoints

```
GET  /api/v1/billing             stand (ieder ingelogd)
GET  /api/v1/billing/plans       plannen + prijzen uit Stripe, bedragen als string (ingelogd)
POST /api/v1/billing/checkout    {plan} of {credits:true} -> {url}   (Admin)
POST /api/v1/billing/portal      -> {url}   (Admin; 409 zonder Stripe-klant)
POST /api/v1/stripe/webhook      anoniem, ruwe body, Stripe-Signature (tolerantie 5 min)
```

Checkout: organisatie in `client_reference_id` én metadata (sessie en abonnement), bestaande klant
hergebruikt, terug naar `<AppUrl>/settings/?billing=success|cancel`. De terugkeer verandert niets:
alleen webhooks wijzigen de stand. Een tweede abonnement afsluiten wordt geweigerd (409): wisselen
gaat via het portaal.

## Webhooks

Organisatie altijd via de opgeslagen klant-id, anders de metadata die xChange zelf meegaf.
Onbekende events: 200, genegeerd. Elk verwerkt event schrijft zijn marker in dezelfde
DynamoDB-transactie als zijn effect, dus een herhaling door Stripe telt niet dubbel.

- `checkout.session.completed` — klant koppelen; bij een eenmalige betaling met
  `payment_status = paid` +20 tegoed. `checkout.session.async_payment_succeeded` doet hetzelfde
  voor betaalmethoden die later bevestigen (SEPA); anders zou zo'n betaling nooit tegoed geven.
- `customer.subscription.created/updated/deleted` — plan uit de lookup_key van de prijs, status,
  periode (van het abonnementsitem). `active`, `trialing` en `past_due` geven de maandconversies;
  `canceled`, `unpaid`, `incomplete*` en `paused` halen ze weg (tegoed blijft). Ouder dan het laatst
  verwerkte abonnements-event: genegeerd (Stripe garandeert geen volgorde).
- `invoice.paid` met `billing_reason` `subscription_create` of `subscription_cycle` — begin van een
  periode: `periodUsed = 0`, eenmaal per periodestart.
- `invoice.payment_failed` — status `past_due`. **Er wordt doorgeleverd** tot Stripe het abonnement
  zelf opzegt of op `unpaid` zet (volgens de herhaalinstellingen in het Stripe-dashboard).

## Web

Instellingen krijgt een kaart "Abonnement": plan en status, conversies deze periode, tegoed,
periode-einde (voor iedereen); planknoppen, "20 conversies kopen" en "Abonnement beheren" (Admin).
Na `?billing=success` een melding en een paar keer opnieuw lezen. De 402-meldingen bij uploaden en
downloaden linken naar die kaart.

## Eenmalige inrichting

`tools/stripe-setup` maakt idempotent de producten/prijzen, de portaalconfiguratie en het
webhook-endpoint, en schrijft het signing secret alleen naar een opgegeven bestand. Zie de README
daar.
