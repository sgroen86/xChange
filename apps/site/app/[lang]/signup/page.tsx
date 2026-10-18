import type { Metadata } from "next";
import Link from "next/link";

import { Icon } from "../../../components/Icon";
import { getDictionary } from "../../../content/i18n";
import { pricing } from "../../../content/pricing";
import { appUrl } from "../../../lib/config";
import { fill } from "../../../lib/format";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { href } from "../../../lib/routes";

/* Self sign-up is being built separately; until then this page says so. */

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
          <div className="focus-card__rows">
            <div className="focus-card__row">
              <span>{t.contactText}</span>
              <Link href={href(lang, "contact")} className="btn btn--primary">
                {t.contactCta}
              </Link>
            </div>
            <div className="focus-card__row">
              <span>{t.loginText}</span>
              <a href={appUrl} className="btn btn--secondary">
                {t.loginCta}
              </a>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
