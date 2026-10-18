import type { Articles } from "./types.ts";

export const articlesEn: Articles = {
  "belgium-e-invoicing-2026": {
    title: "Belgium: mandatory e-invoicing since 2026 — what it means for you",
    description:
      "Since 1 January 2026, invoices between Belgian VAT-registered businesses must be structured e-invoices. Who is affected, what counts as an e-invoice and what to do if you still invoice in PDF.",
    readingMinutes: 4,
    blocks: [
      {
        type: "p",
        text: "Since 1 January 2026, businesses in Belgium can no longer send each other ordinary PDF invoices for most domestic transactions. Invoices between VAT-registered businesses established in Belgium must now be structured electronic invoices. If you still produce your invoices as PDFs from a word processor, a spreadsheet or an invoicing tool, this changes how you bill your Belgian business customers. This article explains what has changed, what counts as an e-invoice and what you can do in practice.",
      },
      { type: "h2", text: "Who is affected" },
      {
        type: "p",
        text: "The obligation covers domestic business-to-business invoices: the supplier and the customer are both VAT-registered and established in Belgium. Invoices to consumers fall outside it, and invoices to customers abroad follow their own rules. Some categories of businesses are excluded. Where exactly the line falls depends on your activity and VAT status, so check your situation with your accountant rather than assuming you are in or out.",
      },
      {
        type: "p",
        text: "The obligation works in both directions. As a supplier you must send structured e-invoices, and as a customer you must be able to receive them. Even a business that sends few invoices of its own needs a way to accept the e-invoices its suppliers now send.",
      },
      { type: "h2", text: "What counts as an e-invoice" },
      {
        type: "p",
        text: "An e-invoice in this sense is not simply an invoice delivered electronically. It is a structured, machine-readable file in which every element — supplier, customer, invoice lines, VAT rates, totals — sits in a defined field that software can read without human interpretation. A PDF does not qualify, even when it is sent by email and even when it looks perfectly tidy. The customer’s accounting system has to be able to process the invoice automatically.",
      },
      {
        type: "p",
        text: "The standard format in Belgium is PEPPOL BIS Billing 3.0, an invoice specification in UBL XML that complies with the European standard EN 16931. It is exchanged over the PEPPOL network, where businesses are reachable through service providers known as access points.",
      },
      { type: "h2", text: "Being reachable on PEPPOL" },
      {
        type: "p",
        text: "Most Belgian businesses are registered on PEPPOL under their enterprise number (the KBO/BCE number), using identifier scheme 0208. A business with enterprise number 0123456789 has the PEPPOL address 0208:0123456789. When you send an invoice, this address tells the network where to deliver it. When you receive, your own registration is what allows your suppliers to reach you.",
      },
      { type: "h2", text: "Practical steps if you still invoice in PDF" },
      {
        type: "ol",
        items: [
          "Check your customers. For each Belgian business customer, find out whether they are registered on PEPPOL and which document types they accept. Search by enterprise or VAT number, not by name.",
          "Choose how you will send. You need a route onto the network: an access point provider directly, or accounting or invoicing software that sends through one on your behalf.",
          "Produce valid BIS Billing 3.0 files. Either switch to software that creates them natively, or convert the invoices you already make. Whichever you choose, the file must pass the official validation rules, or the receiver may reject it.",
          "Keep reviewing the data. A structured invoice exposes every field to automatic processing. A wrong VAT number, an unclear VAT treatment or totals that do not add up will be noticed sooner than on paper.",
          "Arrange receiving. Make sure you are registered on PEPPOL and that incoming e-invoices arrive somewhere you or your accountant will actually process them.",
        ],
      },
      {
        type: "p",
        text: "xChange is built for the conversion route. You upload the PDF invoices you already make, review what was read from them, and download a PEPPOL BIS Billing 3.0 file that has been checked against the complete official validation rules. It also looks your customer up on PEPPOL by enterprise or VAT number. Sending through an access point directly from xChange is being worked on; for now you send the validated file through your own access point or accounting software.",
      },
      {
        type: "note",
        text: "Further steps, such as e-reporting, have been announced for later years. The details may still change, so follow official guidance and your accountant’s advice rather than planning far ahead on assumptions.",
      },
    ],
  },

  "what-is-peppol": {
    title: "What is PEPPOL (and a PEPPOL ID)?",
    description:
      "PEPPOL explained: the network, access points and the four-corner model, what a PEPPOL ID looks like, and how a lookup shows whether someone can receive your invoice.",
    readingMinutes: 4,
    blocks: [
      {
        type: "p",
        text: "PEPPOL comes up as soon as e-invoicing does. Belgium relies on it for mandatory domestic B2B e-invoices, Dutch government bodies receive invoices through it, and many other countries use it as well. Yet the name often stays vague. This article explains what PEPPOL is, how it delivers a document and what a PEPPOL ID is.",
      },
      { type: "h2", text: "A network, not a piece of software" },
      {
        type: "p",
        text: "PEPPOL is an international network for exchanging business documents such as invoices and credit notes. It is governed by OpenPeppol, an international non-profit association that sets the rules, specifications and agreements everyone on the network has to follow. PEPPOL is not an app you install or a portal you log into. It is a set of agreements and technical standards that lets different systems deliver documents to each other reliably.",
      },
      {
        type: "p",
        text: "Businesses do not connect to the network themselves. They connect through certified service providers called access points. Accounting and invoicing software usually works with one behind the scenes; some businesses contract an access point provider directly.",
      },
      { type: "h2", text: "The four-corner model" },
      {
        type: "p",
        text: "Delivery over PEPPOL is usually described as the four-corner model:",
      },
      {
        type: "ol",
        items: [
          "Corner 1 is the sender, who creates the invoice.",
          "Corner 2 is the sender’s access point, which takes the document onto the network.",
          "Corner 3 is the receiver’s access point, which accepts it.",
          "Corner 4 is the receiver, whose system processes the invoice.",
        ],
      },
      {
        type: "p",
        text: "Sender and receiver do not need the same provider. Once you are connected through one access point, you can reach anyone registered on PEPPOL, much as an email address can reach any other address regardless of who hosts it.",
      },
      { type: "h2", text: "What a PEPPOL ID looks like" },
      {
        type: "p",
        text: "Every receiver on the network is registered with a PEPPOL identifier. It has two parts, separated by a colon: a scheme code that says what kind of number follows, and the identifier itself. Common examples:",
      },
      {
        type: "ul",
        items: [
          "0208:0123456789 — a Belgian enterprise number (KBO/BCE).",
          "0106:12345678 — a Dutch Chamber of Commerce (KvK) number.",
          "0190 followed by an OIN — the identification number used by Dutch government bodies.",
        ],
      },
      {
        type: "p",
        text: "The scheme code matters. The same digits under a different scheme refer to a different registration, so a PEPPOL address is always the full combination of scheme and number.",
      },
      { type: "h2", text: "Looking someone up" },
      {
        type: "p",
        text: "Before sending, the sender’s access point looks the receiver up. This happens in two steps. The SML (Service Metadata Locator) is the central register that tells the network where a participant’s details are published. The SMP (Service Metadata Publisher) holds those details: which document types and formats the participant can receive, and which access point handles them.",
      },
      {
        type: "p",
        text: "The lookup answers two practical questions: can this business receive e-invoices over PEPPOL at all, and in which format? A receiver might accept PEPPOL BIS Billing 3.0, a national variant, or several. There is also the public PEPPOL Directory, a searchable list in which you can look participants up yourself. Not every registered participant necessarily appears in it, so the SMP lookup remains the authoritative check.",
      },
      {
        type: "note",
        text: "xChange performs this lookup by VAT or company number when you prepare an invoice: it shows whether the customer can receive e-invoices, fills in their PEPPOL address and picks a format they accept.",
      },
      { type: "h2", text: "BIS Billing 3.0 in brief" },
      {
        type: "p",
        text: "PEPPOL BIS Billing 3.0 is the PEPPOL specification for invoices and credit notes. It is expressed in UBL XML and complies with EN 16931, the European standard for electronic invoices. It defines which fields an invoice contains, how they are coded, and a set of validation rules a file must pass — for instance that the line amounts add up to the totals. It is the standard format for domestic B2B invoices in Belgium and is widely accepted elsewhere on the network.",
      },
    ],
  },

  "bis-3-or-nlcius": {
    title: "BIS 3.0 or NLCIUS: which format does my Dutch customer need?",
    description:
      "PEPPOL BIS Billing 3.0 and NLCIUS (SI-UBL 2.0) both build on EN 16931. How they relate, and how to decide which one to send to a Dutch customer.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "If you invoice Dutch customers electronically, you will come across two names: PEPPOL BIS Billing 3.0 and NLCIUS, also known as SI-UBL 2.0. Both describe a structured e-invoice, both are UBL XML, and both build on the same European standard. Yet a customer may accept one and not the other. This article explains how the two relate and how to decide which one to send.",
      },
      { type: "h2", text: "One European standard, several specifications" },
      {
        type: "p",
        text: "EN 16931 is the European standard for electronic invoices. It defines a core set of invoice information — who sells, who buys, what, at which price and with which VAT — and the rules that tie it together. On its own it is a model of the information rather than a ready-made file format. Specifications built on top of it, known as CIUS (core invoice usage specifications), tailor it to a particular community and express it in a concrete syntax such as UBL.",
      },
      {
        type: "p",
        text: "PEPPOL BIS Billing 3.0 is such a specification for the PEPPOL network and is used across many countries. NLCIUS is the Dutch national one: Dutch rules on top of EN 16931, expressed in UBL as SI-UBL 2.0. It adds stricter and additional Dutch requirements, and it is used notably by Dutch government buyers.",
      },
      { type: "h2", text: "What the two have in common" },
      {
        type: "ul",
        items: [
          "Both comply with EN 16931.",
          "Both are UBL XML files.",
          "Both can be delivered over PEPPOL, provided the receiver is registered for that document type.",
          "Each comes with its own official validation rules, and a file must pass the rules of the specification it claims to follow.",
        ],
      },
      {
        type: "p",
        text: "That last point is why the choice matters. A file that is valid as BIS 3.0 is not automatically valid as NLCIUS, and the reverse is also true. The receiver checks the invoice against the specification it expects, and an invoice that fails those checks can be rejected.",
      },
      { type: "h2", text: "How to decide" },
      {
        type: "p",
        text: "The reliable approach is to find out what your customer accepts rather than guess.",
      },
      {
        type: "ol",
        items: [
          "Ask the customer. Larger organisations, and public bodies in particular, often set out their invoicing requirements in the order, the contract or their instructions for suppliers.",
          "Check their PEPPOL registration. A customer’s registration lists the document types it can receive. If only one of the two formats is listed, that settles it; if both are, either can be delivered.",
        ],
      },
      { type: "h2", text: "Government buyers" },
      {
        type: "p",
        text: "Dutch government buyers — the central government and many other public bodies — expect e-invoices, typically delivered over PEPPOL. They accept NLCIUS and generally also PEPPOL BIS Billing 3.0. They are usually registered on PEPPOL under their OIN, scheme 0190. Because NLCIUS was designed with these buyers in mind, it is a sound choice when the receiving body lists it, but check the registration rather than assume.",
      },
      { type: "h2", text: "Private Dutch customers" },
      {
        type: "p",
        text: "For invoices between Dutch businesses there is no general e-invoicing obligation (yet). Whether you send a structured invoice is a matter of agreement with your customer. If a private customer does want e-invoices over PEPPOL, BIS Billing 3.0 is widely accepted. Dutch businesses are often registered under their KvK number, scheme 0106, and here too the registration shows what the customer can actually receive.",
      },
      {
        type: "note",
        text: "xChange exports both PEPPOL BIS Billing 3.0 and NLCIUS (SI-UBL 2.0), and checks each file against the complete official validation rules of its specification before you can download it. When it looks your customer up on PEPPOL, it picks a format the receiver accepts.",
      },
    ],
  },

  "find-peppol-address": {
    title: "How to find your customer’s PEPPOL address",
    description:
      "Where a PEPPOL address comes from, how to build it from a VAT or company number, how to check it, and what to do if your customer is not registered.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "To send an invoice over PEPPOL you need the receiver’s PEPPOL address, just as you need an email address to send an email. Without it the invoice cannot be delivered, and with a wrong one it may go astray or be refused. Fortunately, the address is usually built from a number you already have on file. This article shows how to find it and how to check it.",
      },
      { type: "h2", text: "Why you need it" },
      {
        type: "p",
        text: "On PEPPOL, invoices are addressed to a PEPPOL identifier, not to a name or an email address. Your access point uses that identifier to look up where the receiver can be reached and which document formats it accepts. If the identifier is missing or wrong, the lookup fails or points to a different organisation. Getting it right once saves you failed deliveries later.",
      },
      { type: "h2", text: "How the address is built" },
      {
        type: "p",
        text: "A PEPPOL identifier has two parts joined by a colon: a scheme code that says what kind of number it is, and the number itself. For the customers you are most likely to meet:",
      },
      {
        type: "ul",
        items: [
          "Belgian businesses: scheme 0208 with the 10-digit enterprise number (KBO/BCE). The enterprise number is the VAT number without the country code: from BE0123456789 you take 0123456789, which gives 0208:0123456789.",
          "Dutch businesses: often scheme 0106 with the KvK (Chamber of Commerce) number.",
          "Dutch government bodies: scheme 0190 with their OIN, the identification number used by Dutch public bodies.",
        ],
      },
      {
        type: "p",
        text: "These are the usual cases, not guarantees. A business can be registered under another scheme, or not at all, so always check.",
      },
      { type: "h2", text: "What to ask your customer" },
      {
        type: "p",
        text: "The simplest route is to ask. A short question when you take on a new customer, or before your first e-invoice to them, avoids guesswork:",
      },
      {
        type: "ul",
        items: [
          "Are you registered on PEPPOL, and under which identifier?",
          "Which invoice format do you prefer or require?",
          "Are there references, such as an order number, that must appear on the invoice?",
        ],
      },
      { type: "h2", text: "How to check" },
      {
        type: "p",
        text: "Whether you were given an identifier or worked one out yourself, verify it. You can search the public PEPPOL Directory, which lists many registered participants, or ask your access point or service provider to look the identifier up. The lookup tells you whether the customer can receive e-invoices over PEPPOL and which document types they accept. Not every registered participant necessarily appears in the Directory, so a lookup through your provider is the more complete check.",
      },
      {
        type: "p",
        text: "Search by number, not by name. Company names are ambiguous: they are not unique, the same business may trade under one name and be registered under another, and similar names can belong to unrelated businesses. A VAT number or company number identifies exactly one organisation. xChange follows the same principle: it looks customers up only by VAT or company number, never by name, and fills in the PEPPOL address it finds.",
      },
      { type: "h2", text: "If the customer is not registered" },
      {
        type: "p",
        text: "If the lookup finds nothing, first check the number and the scheme. If both are right, ask the customer: they may be in the middle of registering, use a different identifier, or have no PEPPOL registration yet. In that case, agree another delivery route for the time being. Bear in mind that in Belgium, domestic invoices between VAT-registered businesses must be structured e-invoices since 2026, so a Belgian business customer will normally need to become reachable.",
      },
      {
        type: "note",
        text: "Once you have a confirmed PEPPOL address, store it in your customer records alongside the VAT and company number. Every invoice then goes to the same verified address, and you only need to check again when the customer’s details change.",
      },
    ],
  },
};
