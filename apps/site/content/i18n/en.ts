import type { Dictionary } from "./types.ts";

export const en: Dictionary = {
  meta: {
    siteName: "xChange",
    description:
      "Turn your own PDF sales invoices into validated e-invoices: PEPPOL BIS 3.0 and NLCIUS, checked against the official validation rules.",
  },
  nav: {
    product: "Product",
    pricing: "Pricing",
    resources: "Resources",
    faq: "FAQ",
    login: "Log in",
    signup: "Sign up",
    openMenu: "Open menu",
    closeMenu: "Close menu",
    mainNav: "Main navigation",
    languageLabel: "Language",
    skipToContent: "Skip to content",
    homeLabel: "xChange — home",
  },
  footer: {
    tagline: "From PDF invoice to validated e-invoice. Built for businesses in Belgium and the Netherlands.",
    productHeading: "Product",
    companyHeading: "Contact",
    legalHeading: "Legal",
    languagesHeading: "Language",
    contact: "Contact",
    privacy: "Privacy policy",
    terms: "Terms of service",
    trust: "Trust & security",
    hostedInEu: "Hosted in the EU (Frankfurt)",
    rights: "All rights reserved.",
  },
  common: {
    comingSoon: "Coming soon",
    demo: "Demo",
    learnMore: "Read more",
    externalLinkNote: "(opens the app)",
  },
  home: {
    meta: {
      title: "Turn PDF invoices into validated e-invoices",
      description:
        "Upload your PDF sales invoice, review the details and download a PEPPOL BIS 3.0 or NLCIUS e-invoice that passes the official validation rules.",
    },
    hero: {
      eyebrow: "E-invoicing for Belgium and the Netherlands",
      title: "From PDF invoice to validated e-invoice",
      lead: "You already make your invoices. xChange reads your PDF, lets you check everything and delivers a PEPPOL BIS 3.0 or NLCIUS file that passes the official validation rules — no new accounting software required.",
      primaryCta: "Try it free",
      secondaryCta: "See how it works",
      note: "{n} free conversions, no card needed.",
    },
    mock: {
      label: "Example: a PDF invoice that xChange turns into a validated e-invoice",
      fileName: "invoice-2026-0142.pdf",
      reading: "Read and checked",
      customer: "Customer",
      customerValue: "Janssens Bouw BV",
      vatNumber: "VAT number",
      peppolId: "PEPPOL ID",
      total: "Total incl. VAT",
      totalValue: "€1,210.00",
      format: "PEPPOL BIS Billing 3.0",
      valid: "Valid — 0 errors",
      receives: "Receives e-invoices via PEPPOL",
      download: "Download XML",
    },
    problem: {
      eyebrow: "The problem",
      title: "A PDF is no longer an e-invoice",
      lead: "More and more customers want your invoice as a structured file over PEPPOL. Rebuilding the way you invoice takes time you would rather spend on your customers.",
      points: [
        {
          title: "Mandatory in Belgium",
          text: "Since 1 January 2026, Belgian businesses must send each other e-invoices. A PDF by email no longer does the job.",
        },
        {
          title: "Government buyers ask for it",
          text: "Dutch public bodies expect e-invoices in NLCIUS or PEPPOL BIS 3.0. One missing field and the invoice is rejected.",
        },
        {
          title: "Switching is a lot of work",
          text: "New invoicing software means new templates, new habits and moving your history. Often you just want to keep making the PDF you make today.",
        },
      ],
    },
    steps: {
      eyebrow: "How it works",
      title: "A valid e-invoice in four steps",
      lead: "Keep invoicing the way you are used to. xChange handles the conversion and the checks.",
      items: [
        {
          title: "Upload or email your PDF",
          text: "Drop one or several PDFs into the app, or email them to your personal xChange address. Emailed PDFs wait for your approval before they are read.",
        },
        {
          title: "AI reads the invoice",
          text: "Claude, Anthropic's AI model, extracts supplier, customer, lines, VAT and totals from your PDF.",
        },
        {
          title: "You review",
          text: "The review screen shows every field with live validation. Correct what needs correcting; amounts are recalculated exactly.",
        },
        {
          title: "Download the e-invoice",
          text: "Pick the format your customer accepts. The file is checked against the complete official validation rules before you can download it.",
        },
      ],
    },
    features: {
      eyebrow: "Product",
      title: "Everything you need to deliver e-invoices",
      lead: "Built around one promise: you never get a file that contains an error.",
      items: [
        {
          icon: "shieldCheck",
          title: "Officially validated",
          text: "Every file runs through the complete official validation rules — the same ones the network uses. If there is an error, you cannot download it.",
        },
        {
          icon: "search",
          title: "PEPPOL lookup",
          text: "Find your customer by VAT or company number, never by name. You see straight away whether they receive e-invoices, and their PEPPOL address is filled in.",
        },
        {
          icon: "fileCheck",
          title: "The right format",
          text: "xChange picks a format your customer accepts on PEPPOL: PEPPOL BIS 3.0 or NLCIUS.",
        },
        {
          icon: "calculator",
          title: "Exact totals",
          text: "Amounts, VAT and the XML are calculated deterministically. No AI in the arithmetic, so no rounding surprises.",
        },
        {
          icon: "users",
          title: "Remembers your customers",
          text: "Customer details are saved as a relation and filled in automatically on the next invoice.",
        },
        {
          icon: "refresh",
          title: "Learns from your corrections",
          text: "Correct your own company details or a customer's VAT treatment and xChange remembers it. Settings show what it has learned, and you can make it forget.",
        },
        {
          icon: "inbox",
          title: "Your own inbox",
          text: "Email PDFs to your personal address. Nothing is read until you approve it.",
        },
        {
          icon: "lock",
          title: "Data in the EU",
          text: "Your data is stored with Amazon Web Services in Frankfurt. Received emails are deleted after 30 days.",
        },
      ],
    },
    formats: {
      eyebrow: "Formats and countries",
      title: "Ready for Belgium and the Netherlands",
      lead: "The formats your customers and public bodies ask for, each checked against its own official rules.",
      items: [
        {
          country: "Belgium",
          format: "PEPPOL BIS Billing 3.0",
          text: "The standard format for mandatory B2B e-invoicing. Customers are usually reachable by their enterprise number.",
          demo: false,
        },
        {
          country: "Netherlands",
          format: "PEPPOL BIS 3.0 · NLCIUS / SI-UBL 2.0",
          text: "BIS 3.0 for business customers on PEPPOL, NLCIUS for the Dutch government.",
          demo: false,
        },
        {
          country: "United Kingdom",
          format: "PEPPOL BIS Billing 3.0",
          text: "For UK customers who receive over PEPPOL.",
          demo: false,
        },
        {
          country: "Singapore",
          format: "PINT-SG",
          text: "Available as a demonstration of the international PINT format.",
          demo: true,
        },
      ],
      validationTitle: "No faulty file leaves the building",
      validationText:
        "Every format is checked with its complete set of official validation rules. A file with an error is never offered for download — you see which field to fix.",
    },
    roadmap: {
      title: "Coming soon",
      items: [
        {
          icon: "send",
          title: "Send directly over PEPPOL",
          text: "Today you download the validated file and send it through your own channel. Sending from xChange through a PEPPOL access point is on its way.",
        },
        {
          icon: "mail",
          title: "Receive incoming invoices",
          text: "Receiving e-invoices from your suppliers is on the roadmap too.",
        },
      ],
    },
    cta: {
      title: "Try it with your own invoices",
      text: "{n} free conversions, no card needed. You will see straight away whether your customer can receive e-invoices.",
      primary: "Try it free",
      secondary: "See pricing",
    },
  },
  pricing: {
    meta: {
      title: "Pricing",
      description:
        "Subscriptions with included conversions, or pay per conversion with credits. Start with {n} free conversions, no card needed.",
    },
    eyebrow: "Pricing",
    title: "Simple pricing, per invoice",
    lead: "Choose a monthly plan or buy credits. You pay per PDF converted. Cancel any month.",
    perMonth: "per month",
    conversionsPerMonth: "{n} invoices per month",
    mostPopular: "Most popular",
    choosePlan: "Choose {plan}",
    plans: {
      starter: { blurb: "For freelancers and small businesses sending a handful of invoices a week." },
      pro: { blurb: "For growing businesses that invoice every day." },
      business: { blurb: "For companies and accounting firms with a high invoice volume." },
    },
    trial: {
      title: "Try it free",
      text: "Start with {n} free conversions. No card, no commitment.",
      cta: "Try it free",
    },
    payg: {
      title: "Pay as you go",
      text: "Invoice irregularly? Buy credits and pay only for what you use.",
      packLabel: "{n} conversions",
      perConversion: "{price} per conversion",
      noSubscription: "No subscription",
      cta: "Buy credits",
    },
    included: {
      title: "Included in every plan",
      lead: "No features locked behind a pricier plan. You only choose the volume.",
      items: [
        "AI reads your PDF invoices",
        "Review screen with live validation",
        "Upload several PDFs at once",
        "Personal email address for your PDFs, with approval first",
        "Export to PEPPOL BIS 3.0 and NLCIUS / SI-UBL 2.0",
        "Checks against the complete official validation rules",
        "PEPPOL lookup by VAT or company number",
        "Customers remembered and filled in automatically",
        "Learning from your corrections",
        "Data hosted in the EU (Frankfurt)",
      ],
      comingSoon: ["Sending directly through a PEPPOL access point", "Receiving incoming invoices"],
    },
    notes: [
      "All prices exclude VAT.",
      "A conversion is one PDF read into an invoice.",
      "Plans are monthly and can be cancelled at any time. Payments are handled securely by Stripe.",
    ],
    questions: {
      title: "Questions about pricing",
      items: [
        {
          q: "What counts as a conversion?",
          a: "One PDF that xChange reads and turns into an invoice. Correcting it, or exporting it again in another format, does not count again.",
        },
        {
          q: "Do I need a card for the trial?",
          a: "No. You get {n} free conversions without any payment details. You only pay, via Stripe, once you choose a plan or buy credits.",
        },
        {
          q: "Can I change plans or cancel?",
          a: "Yes. Plans run month by month and you can cancel at any time. Cancellation takes effect at the end of the current month.",
        },
        {
          q: "Do prices include VAT?",
          a: "No, all prices exclude VAT. VAT is shown on your invoice.",
        },
      ],
    },
  },
  resources: {
    meta: {
      title: "Resources",
      description:
        "Practical guides to e-invoicing in Belgium and the Netherlands: the Belgian mandate, PEPPOL, BIS 3.0 and NLCIUS.",
    },
    eyebrow: "Resources",
    title: "E-invoicing, clearly explained",
    lead: "What the Belgian mandate means, how PEPPOL works and which format your customer needs.",
    readArticle: "Read the article",
    readingTime: "{n} min read",
    backToResources: "Back to resources",
    breadcrumb: "Breadcrumb",
    cta: {
      title: "Ready to get started?",
      text: "Convert your first PDF invoices for free and see straight away whether your customer can receive e-invoices.",
      button: "Try it free",
    },
  },
  faq: {
    meta: {
      title: "Frequently asked questions",
      description:
        "Answers about formats, accuracy, privacy, the email inbox, PEPPOL lookup, pricing and cancelling.",
    },
    eyebrow: "FAQ",
    title: "Questions and answers",
    lead: "Can't find your question? Feel free to get in touch.",
    items: [
      {
        q: "Which formats can xChange produce?",
        a: "PEPPOL BIS Billing 3.0 (for Belgium, the Netherlands and the United Kingdom) and NLCIUS / SI-UBL 2.0 for the Dutch government. PINT-SG for Singapore is available as a demo.",
      },
      {
        q: "How accurately does the AI read my invoices?",
        a: "The AI reads most invoices well, but not flawlessly. That is why you review every invoice on the review screen before exporting: you see every field, with live validation, and can change anything. Amounts and totals are not calculated by the AI but exactly by xChange itself.",
      },
      {
        q: "How do I know the file is correct?",
        a: "Every file is checked against the complete official validation rules of the chosen format — the same rules the network uses. You cannot download a file with an error; you see which field needs fixing. Whether your invoice is commercially and fiscally correct remains your responsibility.",
      },
      {
        q: "Where is my data stored?",
        a: "In the EU, with Amazon Web Services in Frankfurt. The PDF is read by Anthropic in the United States, under the EU standard contractual clauses. The Trust & security page lists all subprocessors and retention periods.",
      },
      {
        q: "How long do you keep my data?",
        a: "Received emails are deleted after 30 days. What the AI model read from an upload is kept for 30 days so we can learn from your corrections. Your invoices are kept for as long as your account exists or until you delete them.",
      },
      {
        q: "How does the email inbox work?",
        a: "You get a personal email address. PDFs you send (or forward) to it appear in the app, but are only read after you approve them. You decide what gets processed.",
      },
      {
        q: "How does xChange look up my customer on PEPPOL?",
        a: "By your customer's VAT or company number, never by name. You see whether the customer can receive e-invoices, their PEPPOL address is filled in, and xChange picks a format they accept.",
      },
      {
        q: "Can I send invoices directly over PEPPOL?",
        a: "Not yet. Today you download the validated file and send it through your own accounting software or PEPPOL service provider, or as agreed with your customer. Sending directly from xChange is coming soon, as is receiving incoming invoices.",
      },
      {
        q: "Does xChange get smarter as I use it?",
        a: "Yes. Customers are remembered and filled in on the next invoice, and corrections to your own company details or the VAT treatment per customer are applied to later invoices. Settings show what has been learned, and you can make it forget.",
      },
      {
        q: "Can I try it for free?",
        a: "Yes. You get {n} free conversions, no card needed. After that you choose a plan or buy credits.",
      },
      {
        q: "Can I cancel at any time?",
        a: "Yes. Plans run month by month and can be cancelled at any time. Cancellation takes effect at the end of the current month.",
      },
      {
        q: "Which languages does xChange support?",
        a: "This website is available in Dutch, English and French. The app itself is currently in Dutch.",
      },
    ],
    more: {
      title: "Another question?",
      text: "We are happy to help.",
      cta: "Get in touch",
    },
  },
  contact: {
    meta: {
      title: "Contact",
      description: "Get in touch with xChange about the product, pricing or support.",
    },
    eyebrow: "Contact",
    title: "Get in touch",
    lead: "Questions about xChange, pricing or e-invoicing? Send us an email.",
    generalTitle: "General questions",
    generalText: "About the product, pricing, privacy or partnerships.",
    supportTitle: "Support",
    supportText: "Help with using the app.",
    companyTitle: "Company details",
    registrationLabel: "Company number",
    vatLabel: "VAT number",
  },
  signup: {
    meta: {
      title: "Sign up",
      description: "Registration for xChange opens soon.",
    },
    eyebrow: "Sign up",
    title: "Registration opens soon",
    lead: "We are putting the finishing touches to self sign-up. Don't want to wait? Get in touch and we will set up your account for you.",
    trialNote: "You will start with {n} free conversions, no card needed.",
    loginText: "Already have an account?",
    loginCta: "Log in",
    contactText: "Want to start now, or have a question?",
    contactCta: "Get in touch",
  },
  login: {
    meta: {
      title: "Log in",
      description: "Log in to the xChange app.",
    },
    title: "Log in to xChange",
    lead: "You log in to the app, where you upload, review and export your invoices.",
    cta: "Go to the app",
    noAccount: "No account yet?",
    signupCta: "Sign up",
  },
  legal: {
    draftNote: "Draft — pending legal review",
    lastUpdated: "Last updated:",
    contents: "Contents",
  },
};
