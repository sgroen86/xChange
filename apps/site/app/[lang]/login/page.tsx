import type { Metadata } from "next";
import Link from "next/link";

import { Icon } from "../../../components/Icon";
import { getDictionary } from "../../../content/i18n";
import { appUrl } from "../../../lib/config";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { href } from "../../../lib/routes";

/* The login itself lives in the app (NEXT_PUBLIC_APP_URL); this page points there. */

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  return pageMetadata(lang, "login/", getDictionary(lang).login.meta);
}

export default async function LoginPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const t = getDictionary(lang).login;

  return (
    <section className="section focus-page">
      <div className="container container--narrow">
        <div className="focus-card focus-card--center">
          <span className="feature__icon feature__icon--lg">
            <Icon name="lock" />
          </span>
          <h1>{t.title}</h1>
          <p className="lead">{t.lead}</p>
          <a href={appUrl} className="btn btn--primary btn--lg">
            {t.cta}
            <Icon name="arrowRight" />
          </a>
          <p className="focus-card__alt">
            {t.noAccount} <Link href={href(lang, "signup")}>{t.signupCta}</Link>
          </p>
        </div>
      </div>
    </section>
  );
}
