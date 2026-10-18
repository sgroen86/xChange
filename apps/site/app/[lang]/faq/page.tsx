import type { Metadata } from "next";

import { CtaBand } from "../../../components/CtaBand";
import { FaqList } from "../../../components/FaqList";
import { PageIntro } from "../../../components/PageIntro";
import { getDictionary } from "../../../content/i18n";
import { pricing } from "../../../content/pricing";
import { fill } from "../../../lib/format";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { href } from "../../../lib/routes";

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  return pageMetadata(lang, "faq/", getDictionary(lang).faq.meta);
}

export default async function FaqPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const dict = getDictionary(lang);
  const t = dict.faq;
  const n = pricing.trialConversions;

  return (
    <>
      <PageIntro eyebrow={t.eyebrow} title={t.title} lead={t.lead} />
      <section className="section section--flush-top">
        <div className="container container--narrow">
          <FaqList items={t.items.map((item) => ({ q: item.q, a: fill(item.a, { n }) }))} />
        </div>
      </section>
      <CtaBand
        title={t.more.title}
        text={t.more.text}
        primary={{ href: href(lang, "contact"), label: t.more.cta }}
        secondary={{ href: href(lang, "pricing"), label: dict.nav.pricing }}
      />
    </>
  );
}
