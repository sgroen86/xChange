import assert from "node:assert/strict";
import { test } from "node:test";

import { dictionaries } from "./index.ts";
import { preferredLanguage } from "./language.ts";

/** Every text, with its path, including those produced by functions (called with sample arguments). */
function texts(value: unknown, path: string): Array<[string, unknown]> {
  if (typeof value === "function") return [[path, value(1, "x", "y")]];
  if (value && typeof value === "object") {
    return Object.entries(value).flatMap(([key, child]) => texts(child, `${path}.${key}`));
  }
  return [[path, value]];
}

test("every text exists and is filled in, in every language", () => {
  const nl = new Map(texts(dictionaries.nl, ""));
  for (const [lang, dictionary] of Object.entries(dictionaries)) {
    const all = texts(dictionary, "");
    assert.deepEqual(all.map(([path]) => path).sort(), [...nl.keys()].sort(), `${lang} has other keys than nl`);
    for (const [path, value] of all) {
      assert.equal(typeof value, "string", `${lang}${path} is not text`);
      assert.ok((value as string).trim().length > 0, `${lang}${path} is empty`);
    }
  }
});

test("Dutch browsers get Dutch, everyone else English", () => {
  assert.equal(preferredLanguage(["nl-NL", "en"]), "nl");
  assert.equal(preferredLanguage(["nl-BE"]), "nl");
  assert.equal(preferredLanguage(["fr-BE", "nl"]), "en");
  assert.equal(preferredLanguage(["en-GB"]), "en");
  assert.equal(preferredLanguage([]), "en");
});
