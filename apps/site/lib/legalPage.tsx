import type { Metadata } from "next";

import { LegalDocument } from "../components/LegalDocument";
import { company } from "../content/company";
import { getDictionary } from "../content/i18n";
import { legal } from "../content/legal";
import type { LegalContent } from "../content/legal/types";
import { formatDate } from "./format";
import { resolveLang, type LangParams } from "./lang";
import { pageMetadata } from "./metadata";

/** Builds the metadata function and page component for one legal document. */
export function legalPage(doc: keyof LegalContent) {
  async function generateMetadata({ params }: LangParams): Promise<Metadata> {
    const lang = await resolveLang(params);
    const content = legal[lang][doc];
    return pageMetadata(lang, `${doc}/`, { title: content.title, description: content.description });
  }

  async function Page({ params }: LangParams) {
    const lang = await resolveLang(params);
    return (
      <LegalDocument
        doc={legal[lang][doc]}
        labels={getDictionary(lang).legal}
        updated={formatDate(company.legalUpdated, lang)}
      />
    );
  }

  return { generateMetadata, Page };
}
