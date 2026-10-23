import type { Metadata } from "next";
import Link from "next/link";

import { CtaBand } from "../../components/CtaBand";
import { HeroMock } from "../../components/HeroMock";
import { Icon } from "../../components/Icon";
import { getDictionary } from "../../content/i18n";
import { pricing } from "../../content/pricing";
import { fill } from "../../lib/format";
import { resolveLang, type LangParams } from "../../lib/lang";
import { pageMetadata } from "../../lib/metadata";
import { appSignupHref, href } from "../../lib/routes";

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  const { home } = getDictionary(lang);
  return pageMetadata(lang, "", { ...home.meta, title: `xChange — ${home.meta.title}` }, { absoluteTitle: true });
}

export default async function HomePage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const { home, common } = getDictionary(lang);
  const n = pricing.trialConversions;

  return (
    <>
      <section className="hero">
        <div className="container hero__grid">
          <div className="hero__text">
            <p className="eyebrow">{home.hero.eyebrow}</p>
            <h1>{home.hero.title}</h1>
            <p className="lead">{home.hero.lead}</p>
            <div className="hero__actions">
              <a href={appSignupHref(lang)} className="btn btn--primary btn--lg">
                {home.hero.primaryCta}
                <Icon name="arrowRight" />
              </a>
              <Link href="#how-it-works" className="btn btn--secondary btn--lg">
                {home.hero.secondaryCta}
              </Link>
            </div>
            <p className="hero__note">
              <Icon name="check" />
              {fill(home.hero.note, { n })}
            </p>
          </div>
          <HeroMock mock={home.mock} />
        </div>
      </section>

      <section className="section section--alt" aria-labelledby="problem-title">
        <div className="container">
          <div className="section__head">
            <p className="eyebrow">{home.problem.eyebrow}</p>
            <h2 id="problem-title">{home.problem.title}</h2>
            <p className="lead">{home.problem.lead}</p>
          </div>
          <ul className="grid grid--3">
            {home.problem.points.map((point) => (
              <li key={point.title} className="card card--plain">
                <h3>{point.title}</h3>
                <p>{point.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="section" id="how-it-works" aria-labelledby="steps-title">
        <div className="container">
          <div className="section__head">
            <p className="eyebrow">{home.steps.eyebrow}</p>
            <h2 id="steps-title">{home.steps.title}</h2>
            <p className="lead">{home.steps.lead}</p>
          </div>
          <ol className="steps">
            {home.steps.items.map((step, i) => (
              <li key={step.title} className="steps__item">
                <span className="steps__number" aria-hidden="true">
                  {i + 1}
                </span>
                <h3>{step.title}</h3>
                <p>{step.text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="section section--alt" id="product" aria-labelledby="features-title">
        <div className="container">
          <div className="section__head">
            <p className="eyebrow">{home.features.eyebrow}</p>
            <h2 id="features-title">{home.features.title}</h2>
            <p className="lead">{home.features.lead}</p>
          </div>
          <ul className="grid grid--4">
            {home.features.items.map((feature) => (
              <li key={feature.title} className="feature">
                <span className="feature__icon">
                  <Icon name={feature.icon} />
                </span>
                <h3>{feature.title}</h3>
                <p>{feature.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="section" aria-labelledby="formats-title">
        <div className="container">
          <div className="section__head">
            <p className="eyebrow">{home.formats.eyebrow}</p>
            <h2 id="formats-title">{home.formats.title}</h2>
            <p className="lead">{home.formats.lead}</p>
          </div>
          <ul className="grid grid--4">
            {home.formats.items.map((item) => (
              <li key={item.country} className="card card--plain format-card">
                <div className="format-card__head">
                  <h3>{item.country}</h3>
                  {item.demo && <span className="badge badge--muted">{common.demo}</span>}
                </div>
                <p className="format-card__format">{item.format}</p>
                <p>{item.text}</p>
              </li>
            ))}
          </ul>
          <div className="callout">
            <span className="callout__icon">
              <Icon name="shieldCheck" />
            </span>
            <div>
              <h3>{home.formats.validationTitle}</h3>
              <p>{home.formats.validationText}</p>
            </div>
          </div>
        </div>
      </section>

      <section className="section section--alt section--tight" aria-labelledby="roadmap-title">
        <div className="container">
          <h2 id="roadmap-title" className="roadmap__title">
            {home.roadmap.title}
          </h2>
          <ul className="grid grid--2">
            {home.roadmap.items.map((item) => (
              <li key={item.title} className="roadmap__item">
                <span className="feature__icon feature__icon--muted">
                  <Icon name={item.icon} />
                </span>
                <div>
                  <h3>
                    {item.title} <span className="badge">{common.comingSoon}</span>
                  </h3>
                  <p>{item.text}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <CtaBand
        title={home.cta.title}
        text={fill(home.cta.text, { n })}
        primary={{ href: appSignupHref(lang), label: home.cta.primary }}
        secondary={{ href: href(lang, "pricing"), label: home.cta.secondary }}
      />
    </>
  );
}
