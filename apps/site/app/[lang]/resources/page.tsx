import type { Metadata } from "next";
import Link from "next/link";

import { Icon } from "../../../components/Icon";
import { PageIntro } from "../../../components/PageIntro";
import { articles } from "../../../content/articles";
import { articleSlugs } from "../../../content/articles/types";
import { getDictionary } from "../../../content/i18n";
import { fill } from "../../../lib/format";
import { resolveLang, type LangParams } from "../../../lib/lang";
import { pageMetadata } from "../../../lib/metadata";
import { articleHref } from "../../../lib/routes";

export async function generateMetadata({ params }: LangParams): Promise<Metadata> {
  const lang = await resolveLang(params);
  return pageMetadata(lang, "resources/", getDictionary(lang).resources.meta);
}

export default async function ResourcesPage({ params }: LangParams) {
  const lang = await resolveLang(params);
  const t = getDictionary(lang).resources;

  return (
    <>
      <PageIntro wide eyebrow={t.eyebrow} title={t.title} lead={t.lead} />
      <section className="section section--flush-top">
        <div className="container">
          <ul className="grid grid--2">
            {articleSlugs.map((slug) => {
              const article = articles[lang][slug];
              return (
                <li key={slug} className="article-card">
                  <p className="article-card__meta">{fill(t.readingTime, { n: article.readingMinutes })}</p>
                  <h2>
                    <Link href={articleHref(lang, slug)} className="article-card__link">
                      {article.title}
                    </Link>
                  </h2>
                  <p>{article.description}</p>
                  <span className="article-card__more" aria-hidden="true">
                    {t.readArticle}
                    <Icon name="arrowRight" />
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      </section>
    </>
  );
}
