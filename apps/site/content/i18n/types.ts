import type { PlanId } from "../pricing.ts";

/**
 * Every UI string on the site, per language. nl.ts, en.ts and fr.ts all
 * implement this type, and lib/content.test.ts checks that they have exactly
 * the same keys and the same number of list items.
 *
 * Placeholders in braces ({n}, {price}, {email}) are filled by lib/format.ts.
 */

export type IconName =
  | "upload"
  | "mail"
  | "sparkles"
  | "edit"
  | "shieldCheck"
  | "search"
  | "users"
  | "refresh"
  | "calculator"
  | "globe"
  | "lock"
  | "fileCheck"
  | "send"
  | "inbox";

export type PageMeta = { title: string; description: string };

export type TitledText = { title: string; text: string };

export type Dictionary = {
  meta: {
    siteName: string;
    /** Default description for pages without their own. */
    description: string;
  };
  nav: {
    product: string;
    pricing: string;
    resources: string;
    faq: string;
    login: string;
    signup: string;
    openMenu: string;
    closeMenu: string;
    mainNav: string;
    languageLabel: string;
    skipToContent: string;
    homeLabel: string;
  };
  footer: {
    tagline: string;
    productHeading: string;
    companyHeading: string;
    legalHeading: string;
    languagesHeading: string;
    contact: string;
    privacy: string;
    terms: string;
    trust: string;
    hostedInEu: string;
    rights: string;
  };
  common: {
    comingSoon: string;
    demo: string;
    learnMore: string;
    externalLinkNote: string;
  };
  home: {
    meta: PageMeta;
    hero: {
      eyebrow: string;
      title: string;
      lead: string;
      primaryCta: string;
      secondaryCta: string;
      note: string;
    };
    mock: {
      label: string;
      fileName: string;
      reading: string;
      customer: string;
      customerValue: string;
      vatNumber: string;
      peppolId: string;
      total: string;
      totalValue: string;
      format: string;
      valid: string;
      receives: string;
      download: string;
    };
    problem: { eyebrow: string; title: string; lead: string; points: TitledText[] };
    steps: { eyebrow: string; title: string; lead: string; items: TitledText[] };
    features: {
      eyebrow: string;
      title: string;
      lead: string;
      items: (TitledText & { icon: IconName })[];
    };
    formats: {
      eyebrow: string;
      title: string;
      lead: string;
      items: { country: string; format: string; text: string; demo: boolean }[];
      validationTitle: string;
      validationText: string;
    };
    roadmap: { title: string; items: (TitledText & { icon: IconName })[] };
    cta: { title: string; text: string; primary: string; secondary: string };
  };
  pricing: {
    meta: PageMeta;
    eyebrow: string;
    title: string;
    lead: string;
    perMonth: string;
    /** "{n} invoices per month" */
    conversionsPerMonth: string;
    mostPopular: string;
    choosePlan: string;
    plans: Record<PlanId, { blurb: string }>;
    trial: { title: string; text: string; cta: string };
    payg: {
      title: string;
      text: string;
      /** "{n} conversions" */
      packLabel: string;
      /** "{price} per conversion" */
      perConversion: string;
      noSubscription: string;
      cta: string;
    };
    included: { title: string; lead: string; items: string[]; comingSoon: string[] };
    notes: string[];
    questions: { title: string; items: { q: string; a: string }[] };
  };
  resources: {
    meta: PageMeta;
    eyebrow: string;
    title: string;
    lead: string;
    readArticle: string;
    /** "{n} min read" */
    readingTime: string;
    backToResources: string;
    breadcrumb: string;
    cta: { title: string; text: string; button: string };
  };
  faq: {
    meta: PageMeta;
    eyebrow: string;
    title: string;
    lead: string;
    items: { q: string; a: string }[];
    more: { title: string; text: string; cta: string };
  };
  contact: {
    meta: PageMeta;
    eyebrow: string;
    title: string;
    lead: string;
    generalTitle: string;
    generalText: string;
    supportTitle: string;
    supportText: string;
    companyTitle: string;
    registrationLabel: string;
    vatLabel: string;
  };
  signup: {
    meta: PageMeta;
    eyebrow: string;
    title: string;
    lead: string;
    trialNote: string;
    signupCta: string;
    pricingText: string;
    pricingCta: string;
    loginText: string;
    loginCta: string;
    appLanguageNote: string;
  };
  login: {
    meta: PageMeta;
    title: string;
    lead: string;
    cta: string;
    noAccount: string;
    signupCta: string;
  };
  legal: {
    draftNote: string;
    lastUpdated: string;
    contents: string;
  };
};
