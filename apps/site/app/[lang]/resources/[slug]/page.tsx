import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import { CtaBand } from "../../../../components/CtaBand";
import { Icon } from "../../../../components/Icon";
import { articles } from "../../../../content/articles";
import { articleSlugs, type ArticleBlock, type ArticleSlug } from "../../../../content/articles/types";
import { getDictionary } from "../../../../content/i18n";
import { pricing } from "../../../../content/pricing";
import { fill } from "../../../../lib/format";
import { resolveLang } from "../../../../lib/lang";
import { pageMetadata } from "../../../../lib/metadata";
import { href } from "../../../../lib/routes";

type Props = { params: Promise<{ lang: string; slug: string }> };

export const dynamicParams = false;

export function generateStaticParams() {
  return articleSlugs.map((slug) => ({ slug }));
}

function isSlug(value: string): value is ArticleSlug {
  return (articleSlugs as readonly string[]).includes(value);
}

async function resolve(params: Props["params"]) {
  const lang = await resolveLang(params);
  const { slug } = await params;
  if (!isSlug(slug)) notFound();
  return { lang, slug, article: articles[lang][slug] };
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { lang, slug, article } = await resolve(params);
  return pageMetadata(lang, `resources/${slug}/`, { title: article.title, description: article.description });
}

function Block({ block }: { block: ArticleBlock }) {
  switch (block.type) {
    case "h2":
      return <h2>{block.text}</h2>;
    case "p":
      return <p>{block.text}</p>;
    case "ul":
      return (
        <ul>
          {block.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      );
    case "ol":
      return (
        <ol>
          {block.items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ol>
      );
    case "note":
      return (
        <aside className="prose__note">
          <Icon name="info" />
          <p>{block.text}</p>
        </aside>
      );
  }
}

export default async function ArticlePage({ params }: Props) {
  const { lang, article } = await resolve(params);
  const t = getDictionary(lang).resources;

  return (
    <>
      <article className="article">
        <div className="container container--prose">
          <nav aria-label={t.breadcrumb} className="breadcrumb">
            <Link href={href(lang, "resources")}>
              <Icon name="arrowRight" className="icon--back" />
              {t.backToResources}
            </Link>
          </nav>
          <header className="article__header">
            <p className="eyebrow">{t.eyebrow}</p>
            <h1>{article.title}</h1>
            <p className="lead">{article.description}</p>
            <p className="article__meta">{fill(t.readingTime, { n: article.readingMinutes })}</p>
          </header>
          <div className="prose">
            {article.blocks.map((block, i) => (
              <Block key={i} block={block} />
            ))}
          </div>
        </div>
      </article>
      <CtaBand
        title={t.cta.title}
        text={fill(t.cta.text, { n: pricing.trialConversions })}
        primary={{ href: href(lang, "signup"), label: t.cta.button }}
        secondary={{ href: href(lang, "resources"), label: t.backToResources }}
      />
    </>
  );
}
