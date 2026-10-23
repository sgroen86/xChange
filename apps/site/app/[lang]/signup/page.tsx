import type { Metadata } from "next";
import Link from "next/link";

import { Icon } from "../../../components/Icon";
import { getDictionary } from "../../../content/i18n";
import { pricing } from "../../../content/pricing";
import { appUrl } from "../../../lib/config";
import { fill } from "../../../lib/format";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { appSignupHref, href } from "../../../lib/routes";

/*
 * Sign-up itself happens in the app; the site's buttons link there directly.
 * This page stays for old links and explains where to go.
 */

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  return pageMetadata(lang, "signup/", getDictionary(lang).signup.meta);
}

export default async function SignupPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const t = getDictionary(lang).signup;

  return (
    <section className="section focus-page">
      <div className="container container--narrow">
        <div className="focus-card">
          <p className="eyebrow">{t.eyebrow}</p>
          <h1>{t.title}</h1>
          <p className="lead">{t.lead}</p>
          <p className="focus-card__note">
            <Icon name="sparkles" />
            {fill(t.trialNote, { n: pricing.trialConversions })}
          </p>
          <a href={appSignupHref(lang)} className="btn btn--primary btn--lg">
            {t.signupCta}
            <Icon name="arrowRight" />
          </a>
          <div className="focus-card__rows">
            <div className="focus-card__row">
              <span>{t.pricingText}</span>
              <Link href={href(lang, "pricing")} className="btn btn--secondary">
                {t.pricingCta}
              </Link>
            </div>
            <div className="focus-card__row">
              <span>{t.loginText}</span>
              <a href={appUrl} className="btn btn--secondary">
                {t.loginCta}
              </a>
            </div>
          </div>
          <p className="focus-card__alt">{t.appLanguageNote}</p>
        </div>
      </div>
    </section>
  );
}
