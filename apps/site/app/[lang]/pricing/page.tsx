import type { Metadata } from "next";

import { CtaBand } from "../../../components/CtaBand";
import { FaqList } from "../../../components/FaqList";
import { Icon } from "../../../components/Icon";
import { PageIntro } from "../../../components/PageIntro";
import { getDictionary } from "../../../content/i18n";
import { pricePerConversionCents, pricing } from "../../../content/pricing";
import { fill, formatEuroCents, formatNumber } from "../../../lib/format";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { appSignupHref, href } from "../../../lib/routes";

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  const { pricing: t } = getDictionary(lang);
  return pageMetadata(lang, "pricing/", {
    title: t.meta.title,
    description: fill(t.meta.description, { n: pricing.trialConversions }),
  });
}

export default async function PricingPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const dict = getDictionary(lang);
  const t = dict.pricing;
  const n = pricing.trialConversions;
  // Every button goes to sign-up in the app; with a plan, payment follows the confirmation.
  const trialSignup = appSignupHref(lang);

  return (
    <>
      <PageIntro wide eyebrow={t.eyebrow} title={t.title} lead={t.lead}>
        <p className="trial-pill">
          <Icon name="sparkles" />
          <span>
            <strong>{t.trial.title}</strong> — {fill(t.trial.text, { n })}
          </span>
        </p>
      </PageIntro>

      <section className="section section--flush-top" aria-label={t.title}>
        <div className="container">
          <ul className="plans">
            {pricing.plans.map((plan) => (
              <li key={plan.id} className={plan.highlighted ? "plan plan--highlighted" : "plan"}>
                {plan.highlighted && <span className="plan__badge">{t.mostPopular}</span>}
                <h2 className="plan__name">{plan.name}</h2>
                <p className="plan__blurb">{t.plans[plan.id].blurb}</p>
                <p className="plan__price">
                  <span className="plan__amount">{formatEuroCents(plan.monthlyPriceCents, lang)}</span>
                  <span className="plan__period">{t.perMonth}</span>
                </p>
                <p className="plan__volume">
                  <Icon name="fileCheck" />
                  {fill(t.conversionsPerMonth, { n: formatNumber(plan.includedConversions, lang) })}
                </p>
                <a
                  href={appSignupHref(lang, plan.id)}
                  className={plan.highlighted ? "btn btn--primary btn--block" : "btn btn--secondary btn--block"}
                >
                  {fill(t.choosePlan, { plan: plan.name })}
                </a>
              </li>
            ))}
          </ul>

          <div className="payg">
            <div className="payg__text">
              <h2>{t.payg.title}</h2>
              <p>{t.payg.text}</p>
            </div>
            <ul className="payg__packs">
              {pricing.creditPacks.map((pack) => (
                <li key={pack.conversions} className="payg__pack">
                  <span className="payg__label">{fill(t.payg.packLabel, { n: formatNumber(pack.conversions, lang) })}</span>
                  <span className="payg__price">{formatEuroCents(pack.priceCents, lang)}</span>
                  <span className="payg__unit">
                    {fill(t.payg.perConversion, { price: formatEuroCents(pricePerConversionCents(pack), lang) })}
                    {" · "}
                    {t.payg.noSubscription}
                  </span>
                </li>
              ))}
            </ul>
            <a href={appSignupHref(lang, "credits")} className="btn btn--secondary">
              {t.payg.cta}
            </a>
          </div>

          <ul className="price-notes">
            {t.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </div>
      </section>

      <section className="section section--alt" aria-labelledby="included-title">
        <div className="container">
          <div className="section__head">
            <h2 id="included-title">{t.included.title}</h2>
            <p className="lead">{t.included.lead}</p>
          </div>
          <ul className="checklist">
            {t.included.items.map((item) => (
              <li key={item}>
                <Icon name="check" />
                {item}
              </li>
            ))}
            {t.included.comingSoon.map((item) => (
              <li key={item} className="checklist__soon">
                <Icon name="send" />
                <span>
                  {item} <span className="badge">{dict.common.comingSoon}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="section" aria-labelledby="pricing-questions">
        <div className="container container--narrow">
          <h2 id="pricing-questions" className="section__title">
            {t.questions.title}
          </h2>
          <FaqList items={t.questions.items.map((item) => ({ q: item.q, a: fill(item.a, { n }) }))} />
        </div>
      </section>

      <CtaBand
        title={dict.home.cta.title}
        text={fill(dict.home.cta.text, { n })}
        primary={{ href: trialSignup, label: t.trial.cta }}
        secondary={{ href: href(lang, "faq"), label: dict.nav.faq }}
      />
    </>
  );
}
