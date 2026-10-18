import type { Metadata } from "next";

import { Icon } from "../../../components/Icon";
import { PageIntro } from "../../../components/PageIntro";
import { company, formatAddress } from "../../../content/company";
import { getDictionary } from "../../../content/i18n";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";

/* No form: there is no backend for one. Mail addresses come from content/company.ts. */

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  return pageMetadata(lang, "contact/", getDictionary(lang).contact.meta);
}

export default async function ContactPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const t = getDictionary(lang).contact;

  return (
    <>
      <PageIntro eyebrow={t.eyebrow} title={t.title} lead={t.lead} />
      <section className="section section--flush-top">
        <div className="container container--narrow">
          <div className="grid grid--2">
            <div className="card card--plain contact-card">
              <span className="feature__icon">
                <Icon name="mail" />
              </span>
              <h2>{t.generalTitle}</h2>
              <p>{t.generalText}</p>
              <a href={`mailto:${company.contactEmail}`} className="contact-card__email">
                {company.contactEmail}
              </a>
            </div>
            <div className="card card--plain contact-card">
              <span className="feature__icon">
                <Icon name="inbox" />
              </span>
              <h2>{t.supportTitle}</h2>
              <p>{t.supportText}</p>
              <a href={`mailto:${company.supportEmail}`} className="contact-card__email">
                {company.supportEmail}
              </a>
            </div>
          </div>
          <div className="card card--plain company-card">
            <h2>{t.companyTitle}</h2>
            <address>
              <strong>{company.name}</strong>
              <br />
              {formatAddress()}
            </address>
            <dl>
              <div>
                <dt>{t.registrationLabel}</dt>
                <dd>{company.registrationNumber}</dd>
              </div>
              <div>
                <dt>{t.vatLabel}</dt>
                <dd>{company.vatNumber}</dd>
              </div>
            </dl>
          </div>
        </div>
      </section>
    </>
  );
}
