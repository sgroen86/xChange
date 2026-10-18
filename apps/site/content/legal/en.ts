import { company, formatAddress } from "../company.ts";
import type { LegalContent } from "./types.ts";

export const legalEn: LegalContent = {
  privacy: {
    title: "Privacy policy",
    description: `How ${company.name} handles personal data when you use xChange or visit its website.`,
    intro: [
      "This privacy policy is for everyone whose personal data we handle through xChange: the people who use the application on behalf of their organisation, people who contact us, and visitors to our website.",
      `xChange is a service of ${company.name}. Below we explain which data we process, why, for how long, who else is involved and what rights you have.`,
    ],
    sections: [
      {
        heading: "Who is responsible",
        paragraphs: [
          `The controller for the processing described in this policy is ${company.name}, ${formatAddress()}. Registration number: ${company.registrationNumber}.${company.vatNumber ? ` VAT number: ${company.vatNumber}.` : ""}`,
          `For any question about privacy or this policy, email ${company.contactEmail}.`,
        ],
      },
      {
        heading: "Scope and our two roles",
        paragraphs: [
          "We act in two different roles, depending on the data.",
          "For account data, billing data, support correspondence and website data, we are the controller: we decide why and how that data is processed, and this policy applies in full.",
          "For the invoice data you upload or email to xChange, we are a processor. Your organisation is the controller: it decides which invoices it processes with xChange, and we process that data only to provide the service on its behalf. That relationship is governed by the data processing agreement, which forms part of our terms of service and is summarised on the Trust & security page.",
        ],
      },
      {
        heading: "Which data we process",
        items: [
          "Account data: your name, your email address and your password, which we store only as a hash and never in readable form.",
          "Organisation details: the name, address, VAT number, company number and other details of the organisation you use xChange for.",
          "Invoices and PDFs you upload or email to xChange, and the data on them, such as names, addresses and contact details of your customers. We process these as a processor.",
          "Emails sent to your personal xChange inbox address, including their attachments.",
          "Corrections you make on the review screen. We use them to learn for your organisation only, so the next invoice needs fewer fixes.",
          "Billing data: your plan, payments and invoices from us. Payments are handled by our payment provider; we do not store full card numbers.",
          "Technical logs: IP address, timestamps and error messages, generated when you use the application.",
          "Correspondence with us, for example when you email us with a question.",
        ],
        after: [
          "Our website does not use analytics or advertising cookies. The application uses only what is strictly necessary to keep you logged in.",
        ],
      },
      {
        heading: "Purposes and legal bases",
        table: {
          head: ["Purpose", "Legal basis"],
          rows: [
            ["Providing the service and your account", "Performance of the contract"],
            ["Billing and accounting", "Legal obligation"],
            ["Security, preventing abuse and analysing errors", "Legitimate interest"],
            ["Support", "Performance of the contract or legitimate interest"],
            ["Learning from corrections for your own organisation", "Performance of the contract"],
            ["Service announcements about your account", "Legitimate interest"],
          ],
        },
        after: [
          "We do not sell personal data, and we do not use it for advertising.",
        ],
      },
      {
        heading: "Use of AI",
        paragraphs: [
          "When you upload a PDF, or approve a PDF that was emailed to your inbox, it is read by an AI model of a specialised processor to extract the invoice fields.",
          "A person always reviews and, where needed, corrects the result before anything is exported. Amounts, totals and the XML file are calculated by deterministic software, not by the AI model.",
          "There is no automated decision-making with legal or similarly significant effects for you within the meaning of Article 22 of the GDPR.",
        ],
      },
      {
        heading: "Processors and recipients",
        paragraphs: [
          "We use the following service providers, which process personal data on our instructions:",
        ],
        table: {
          head: ["Processor", "Purpose", "Location"],
          rows: [
            ["Hosting provider", "Hosting and storage", "European Union"],
            [
              "AI text-recognition provider",
              "Reading uploaded PDFs",
              "European Union and United States",
            ],
            [
              "Payment provider",
              "Payment processing",
              "European Union and United States",
            ],
          ],
        },
        after: [
          `An up-to-date list of our processors is available on request at ${company.contactEmail}.`,
          "We share personal data with authorities only when we are required to by law.",
        ],
      },
      {
        heading: "Transfers outside the EEA",
        paragraphs: [
          "Reading PDFs may involve processing outside the EU, in the United States. That transfer takes place under the EU standard contractual clauses.",
          "Any other transfer of personal data outside the European Economic Area takes place only with appropriate safeguards as required by the GDPR.",
        ],
      },
      {
        heading: "How long we keep data",
        items: [
          `Account and organisation data: for as long as your account exists, and up to ${company.retentionAfterClosing} after it is closed.`,
          `Invoices and PDFs: until you delete them or close your account; after closing, they are deleted within ${company.retentionAfterClosing}.`,
          "Raw received emails: deleted after 30 days.",
          "The model’s reading of an upload (the extraction result): kept for 30 days.",
          "Billing records: for as long as tax and accounting law requires.",
          "Technical logs: for a limited period.",
        ],
      },
      {
        heading: "Security",
        paragraphs: [
          "We protect data with technical and organisational measures, including encryption in transit and at rest, separate storage per organisation, and restricted access to production. The Trust & security page describes these measures in more detail.",
        ],
      },
      {
        heading: "Your rights",
        paragraphs: [
          "Under the GDPR you have the right to access your personal data, to have it rectified or erased, to restrict its processing, to receive it in a portable format and to object to processing. Where processing is based on your consent, you can withdraw that consent at any time.",
          `To exercise a right, email ${company.contactEmail}. We will answer within one month. We may ask you to confirm your identity first.`,
          "For invoice data we process as a processor, your request is a matter for the organisation that uploaded the invoice. If you send it to us, we will forward it to that organisation and assist it in handling the request.",
        ],
      },
      {
        heading: "Complaints",
        paragraphs: [
          `If you are unhappy with how we handle your data, please contact us first at ${company.contactEmail}, so we can try to resolve it.`,
          `You also have the right to lodge a complaint with ${company.supervisoryAuthority} (${company.supervisoryAuthorityUrl}), or with the data protection authority in the country where you live or work.`,
        ],
      },
      {
        heading: "Changes to this policy",
        paragraphs: [
          "We may update this policy, for example when the service changes. The current version is always published on this page.",
          "We will inform account holders of material changes in advance.",
        ],
      },
    ],
  },

  terms: {
    title: "Terms of service",
    description: `The terms that apply to the use of xChange, provided by ${company.name}.`,
    intro: [
      `These terms apply to every use of xChange, a service provided by ${company.name}, ${formatAddress()}.`,
      "xChange is intended for businesses only, not for consumers. By creating an account, you confirm that you use the service for your business or profession and that you are authorised to accept these terms on behalf of your organisation.",
    ],
    sections: [
      {
        heading: "Definitions",
        items: [
          "Service: the xChange web application and everything that comes with it.",
          "Customer: the business or organisation that has an account with xChange.",
          "User: a person who uses the Service on behalf of the Customer.",
          "Conversion: one PDF read into an invoice by the Service.",
          "Plan: the monthly subscription the Customer has chosen, with a number of included Conversions.",
          "Credits: prepaid Conversions bought as a credit pack, outside a Plan.",
        ],
      },
      {
        heading: "The service",
        paragraphs: [
          "xChange turns your own PDF sales invoices into validated e-invoices. You upload PDFs or email them to your personal inbox address; an AI model reads the invoice; you review and correct the fields on a review screen; xChange then produces the e-invoice file.",
          "The Service currently supports PEPPOL BIS Billing 3.0, NLCIUS / SI-UBL 2.0 and, as a demonstration only, PINT-SG. Every file is checked against the complete official validation rules for its format before it can be downloaded. A file with an error is not handed out.",
          "The Service can look up your customer on the PEPPOL network by VAT number or company number, remembers your customers, and learns from your corrections for your own organisation.",
          "Not included at this time: sending invoices through a PEPPOL access point, and receiving incoming invoices. You download the validated file and send it yourself, through your own access point or accounting software, or by the route agreed with your customer.",
          "We develop the Service continuously. Features may be added, changed or removed over time.",
        ],
      },
      {
        heading: "Account and access",
        paragraphs: [
          "You provide accurate and complete information when you create an account, and keep it up to date.",
          "Keep your login details confidential. The Customer is responsible for its Users and for everything that happens through their accounts.",
          `If you suspect misuse of an account, let us know straight away at ${company.supportEmail}.`,
        ],
      },
      {
        heading: "Free trial",
        paragraphs: [
          "New Customers can try the Service with 10 free Conversions. No payment card is needed and there is no obligation to continue.",
          "We may end or change the trial conditions for new sign-ups.",
        ],
      },
      {
        heading: "Plans, prices and payment",
        paragraphs: [
          "We offer monthly subscriptions with a number of included Conversions, and pay-as-you-go credit packs.",
          "Current prices are shown on the pricing page. All prices exclude VAT. Payment is handled by our payment provider.",
          `We announce price changes at least ${company.noticePeriod} in advance. A new price takes effect from the next billing period after that notice.`,
        ],
      },
      {
        heading: "Cancellation and termination",
        paragraphs: [
          "You can cancel your subscription at any time. The cancellation takes effect at the end of the current billing period.",
          "We may suspend or terminate your access in the event of a serious breach of these terms or non-payment, after giving you notice.",
          "Export the data you want to keep before you close your account. After closing, your data is deleted as described in the privacy policy.",
        ],
      },
      {
        heading: "Fair use",
        paragraphs: ["When using the Service, you will not:"],
        items: [
          "upload unlawful content, or documents you are not entitled to process;",
          "attempt to access other customers’ data or to break or bypass the security of the Service;",
          "use the Service automatically or in bulk beyond what your Plan allows, or in a way that overloads it;",
          "resell or make the Service available to third parties without our agreement;",
          "reverse engineer the Service, except as far as the law permits.",
        ],
      },
      {
        heading: "Your responsibility for invoice content",
        paragraphs: [
          "You remain responsible for the content of your invoices and for the data you review, correct, book and export.",
          "Validation checks the file against the rules of its format. It does not check whether the invoice is commercially or fiscally correct, for example whether the right VAT rate has been applied.",
          "Check each invoice before you send it. You remain responsible for your own tax and bookkeeping obligations.",
        ],
      },
      {
        heading: "Your data",
        paragraphs: [
          "Your data remains yours. We process it only to provide the Service, as described in the privacy policy and the data processing agreement.",
          "You can export your validated files at any time while you have an account.",
        ],
      },
      {
        heading: "AI-assisted reading",
        paragraphs: [
          "Invoice fields are read from your PDFs by an AI model. Fields read this way can contain errors. That is why reviewing the result is part of the process, and why you check the fields before you export a file.",
        ],
      },
      {
        heading: "Availability and support",
        paragraphs: [
          "We make reasonable efforts to keep the Service available and working properly, but we do not guarantee uninterrupted availability.",
          "We may carry out maintenance, which can make the Service temporarily unavailable.",
          `For help with the Service, email ${company.supportEmail}.`,
        ],
      },
      {
        heading: "Intellectual property",
        paragraphs: [
          `The Service and its software remain the property of ${company.name}. During your subscription or trial you receive a non-exclusive, non-transferable licence to use the Service for your own business.`,
          "Your content stays yours. You grant us the rights we need to process it in order to provide the Service.",
        ],
      },
      {
        heading: "Liability",
        paragraphs: [
          "Our total liability is limited to the amounts you paid us for the Service in the 12 months before the event that caused the damage.",
          "We are not liable for indirect damage, such as lost profit, loss of data beyond what can be restored from backups, or fines resulting from the content of your invoices.",
          "These limitations do not apply where the law does not allow them, such as in the case of intent or gross negligence.",
        ],
      },
      {
        heading: "Changes to these terms",
        paragraphs: [
          `We announce changes to these terms at least ${company.noticePeriod} before they take effect.`,
          "If you continue to use the Service after a change has taken effect, you accept the new terms. If you do not agree, you can cancel your subscription before the change takes effect.",
        ],
      },
      {
        heading: "Governing law and disputes",
        paragraphs: [
          `These terms are governed by ${company.governingLaw}.`,
          `Disputes are submitted to ${company.competentCourt}.`,
          `We would rather solve a problem together first. If you have a complaint, contact us at ${company.contactEmail}.`,
        ],
      },
    ],
  },

  trust: {
    title: "Trust & security",
    description: "How xChange stores, reads, protects and retains your data, in short.",
    intro: [
      "This page explains, in short, how xChange handles your data: where it is stored, who processes it, how long we keep it and how we protect it. It also summarises the data processing agreement that applies to the invoice data you process with xChange.",
    ],
    sections: [
      {
        heading: "Where your data lives",
        paragraphs: [
          "xChange is hosted in the European Union. Your documents and invoice records are stored there.",
        ],
      },
      {
        heading: "How invoices are read",
        paragraphs: [
          "An AI model of a specialised processor reads the PDF and extracts the invoice fields. This may involve processing outside the EU, in the United States; that transfer takes place under the EU standard contractual clauses.",
          "You review every field before anything is exported. Totals and the XML file are calculated deterministically: there is no AI in the arithmetic or in generating the file.",
          "Every export is checked against the complete official validation rules for its format. A file with an error is never handed out.",
        ],
      },
      {
        heading: "Subprocessors",
        table: {
          head: ["Subprocessor", "Purpose", "Location"],
          rows: [
            ["Hosting provider", "Hosting and storage", "European Union"],
            [
              "AI text-recognition provider",
              "Reading uploaded PDFs",
              "European Union and United States",
            ],
            [
              "Payment provider",
              "Payment processing",
              "European Union and United States",
            ],
          ],
        },
        after: [
          `An up-to-date list of our subprocessors is available on request at ${company.contactEmail}.`,
          "We announce changes to our subprocessors in advance.",
        ],
      },
      {
        heading: "Retention",
        table: {
          head: ["Data", "Retention"],
          rows: [
            ["Raw received emails", "30 days"],
            ["The model’s reading of an upload", "30 days"],
            [
              "Invoices and source PDFs",
              `Until you delete them or close your account, then deleted within ${company.retentionAfterClosing}`,
            ],
            [
              "Account data",
              `For as long as the account exists, then deleted within ${company.retentionAfterClosing}`,
            ],
            ["Billing records", "As long as the law requires"],
          ],
        },
      },
      {
        heading: "Security measures",
        items: [
          "Data is encrypted in transit (TLS) and at rest.",
          "Each organisation’s data is stored separately, keyed by organisation, and cannot be reached by other organisations.",
          "Passwords are stored only as bcrypt hashes.",
          "Repeated failed logins lock the login temporarily.",
          "API keys and other secrets are kept in a managed secret store, not in code.",
          "Access to production is limited to what is needed.",
        ],
      },
      {
        heading: "Data processing agreement: summary",
        paragraphs: [
          "For the invoice data you process with xChange, your organisation is the controller and we are the processor. The data processing agreement covers the following:",
        ],
        items: [
          "Subject and duration: providing xChange, for the term of the subscription.",
          "Nature and purpose: reading, storing, validating and exporting invoices.",
          "Types of personal data: names, addresses, email addresses, phone numbers, VAT and company numbers, and bank details appearing on invoices; user account data.",
          "Categories of data subjects: the customer’s users, and the customer’s own customers and their contact persons.",
          "We process the data only on the customer’s documented instructions.",
          "Staff with access to the data are bound by confidentiality.",
          "We take appropriate technical and organisational security measures.",
          "We use subprocessors only under equivalent data protection obligations.",
          "We assist the customer with data subject requests and with data protection impact assessments.",
          "We notify the customer of a personal data breach without undue delay.",
          "At the end of the service, we delete or return the data.",
          "We make available the information needed to demonstrate compliance, including for audits.",
        ],
        after: [
          `The data processing agreement forms part of our terms of service. You can request a copy at ${company.contactEmail}.`,
        ],
      },
      {
        heading: "Incidents",
        paragraphs: [
          "If a security incident occurs, we investigate it straight away and take measures to contain it and limit the consequences.",
          "If personal data is affected, we notify the affected customers without undue delay, with the information they need to meet their own obligations. We cooperate with the competent authorities.",
        ],
      },
      {
        heading: "Questions and reporting a vulnerability",
        paragraphs: [
          `For questions about security or privacy, email ${company.contactEmail}.`,
          `If you have found a vulnerability, please report it to the same address and give us the opportunity to fix it before you make it public. We appreciate responsible disclosure.`,
        ],
      },
    ],
  },
};
