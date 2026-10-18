import assert from "node:assert/strict";
import { test } from "node:test";

import { articles } from "../content/articles/index.ts";
import { articleSlugs } from "../content/articles/types.ts";
import { dictionaries } from "../content/i18n/index.ts";
import { langs } from "../content/langs.ts";
import { legal } from "../content/legal/index.ts";
import { pricePerConversionCents, pricing } from "../content/pricing.ts";
import { fill, formatEuroCents } from "./format.ts";
import { switchLang } from "./routes.ts";

/**
 * Every leaf path of a value: object keys and array indices, so two
 * dictionaries match only when they have the same keys AND the same number
 * of list items (FAQ entries, features, steps …).
 */
function shape(value: unknown, prefix = ""): string[] {
  if (Array.isArray(value)) {
    return [`${prefix}[]#${value.length}`, ...value.flatMap((item, i) => shape(item, `${prefix}[${i}]`))];
  }
  if (value !== null && typeof value === "object") {
    return Object.keys(value)
      .sort()
      .flatMap((key) => shape((value as Record<string, unknown>)[key], prefix ? `${prefix}.${key}` : key));
  }
  return [prefix];
}

function strings(value: unknown, prefix = ""): [string, string][] {
  if (typeof value === "string") return [[prefix, value]];
  if (Array.isArray(value)) return value.flatMap((item, i) => strings(item, `${prefix}[${i}]`));
  if (value !== null && typeof value === "object") {
    return Object.entries(value).flatMap(([key, item]) => strings(item, prefix ? `${prefix}.${key}` : key));
  }
  return [];
}

test("every language dictionary has exactly the same keys and list lengths", () => {
  const reference = shape(dictionaries.nl);
  for (const lang of langs) {
    assert.deepEqual(shape(dictionaries[lang]), reference, `dictionary "${lang}" differs from "nl"`);
  }
});

test("no dictionary string is empty", () => {
  for (const lang of langs) {
    for (const [path, text] of strings(dictionaries[lang])) {
      assert.ok(text.trim().length > 0, `${lang}: ${path} is empty`);
    }
  }
});

test("placeholders are the same in every language", () => {
  const placeholders = (lang: (typeof langs)[number]) =>
    strings(dictionaries[lang]).map(([path, text]) => `${path}:${(text.match(/\{\w+\}/g) ?? []).sort().join(",")}`);
  for (const lang of langs) {
    assert.deepEqual(placeholders(lang), placeholders("nl"), `placeholders in "${lang}" differ from "nl"`);
  }
});

test("every article exists in all three languages, with content", () => {
  for (const lang of langs) {
    assert.deepEqual(Object.keys(articles[lang]).sort(), [...articleSlugs].sort(), `articles in "${lang}"`);
    for (const slug of articleSlugs) {
      const article = articles[lang][slug];
      assert.ok(article.title.trim(), `${lang}/${slug}: title`);
      assert.ok(article.description.trim(), `${lang}/${slug}: description`);
      assert.ok(article.readingMinutes > 0, `${lang}/${slug}: readingMinutes`);
      assert.ok(article.blocks.length >= 5, `${lang}/${slug}: too few blocks`);
    }
  }
});

test("legal documents have the same sections in every language", () => {
  const outline = (lang: (typeof langs)[number]) =>
    Object.fromEntries(
      Object.entries(legal[lang]).map(([doc, content]) => [
        doc,
        // Same sections, same tables; how prose splits into paragraphs and
        // bullets may differ per language.
        content.sections.map((s) => (s.table ? `table:${s.table.head.length}x${s.table.rows.length}` : "text")),
      ]),
    );
  for (const lang of langs) {
    assert.deepEqual(outline(lang), outline("nl"), `legal outline "${lang}" differs from "nl"`);
  }
});

test("prices render from whole cents", () => {
  assert.equal(formatEuroCents(1900, "en"), "€19");
  assert.equal(formatEuroCents(150, "en"), "€1.50");
  assert.equal(pricePerConversionCents({ conversions: 20, priceCents: 3000 }), 150);
  for (const plan of pricing.plans) assert.ok(Number.isInteger(plan.monthlyPriceCents));
});

test("fill replaces known placeholders only", () => {
  assert.equal(fill("{n} free, {x}", { n: 10 }), "10 free, {x}");
});

test("switching language keeps the page", () => {
  assert.equal(switchLang("/nl/pricing/", "fr"), "/fr/pricing/");
  assert.equal(switchLang("/nl/resources/what-is-peppol", "en"), "/en/resources/what-is-peppol/");
  assert.equal(switchLang("/en", "nl"), "/nl/");
  assert.equal(switchLang("/", "fr"), "/fr/");
});
