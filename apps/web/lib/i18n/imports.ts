/** Texts for the imports area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  status: {
    awaitingApproval: "Wacht op goedkeuring",
    queued: "In de wachtrij",
    processing: "Wordt gelezen",
    ready: "Klaar om te beoordelen",
    failed: "Mislukt",
  },
  title: "Te beoordelen",
  count: (count: number) => `${count} factu${count === 1 ? "ur" : "ren"} in de lijst`,
  approveAll: (count: number) => `Alles verwerken (${count})`,
  uploadInvoices: "Facturen uploaden",
  noApi: "Er is geen API geconfigureerd: importeren in de achtergrond is niet beschikbaar.",
  emailNotice:
    "Per e-mail ontvangen PDF's worden pas gelezen als je ze goedkeurt. Bekijk ze eerst; verwijder wat geen factuur van jou is.",
  empty:
    "Niets te beoordelen. Upload meerdere PDF's tegelijk; ze worden in de achtergrond gelezen en verschijnen hier.",
  columns: {
    file: "Bestand",
    invoice: "Factuur",
    status: "Status",
    received: "Ontvangen",
  },
  from: (sender: string) => ` · van ${sender}`,
  view: "Bekijken",
  approve: "Verwerken",
  review: "Beoordelen",
  approvedOne: "Wordt nu gelezen.",
  approvedMany: (count: number) => `${count} facturen worden nu gelezen.`,
  requeued: "Opnieuw in de wachtrij gezet.",
  dismiss: "Uit de lijst verwijderen",
  dismissed: "Uit de lijst verwijderd.",
  notReadYet: "Deze factuur is nog niet gelezen.",
};

const en: typeof nl = {
  status: {
    awaitingApproval: "Awaiting approval",
    queued: "Queued",
    processing: "Being read",
    ready: "Ready to review",
    failed: "Failed",
  },
  title: "To review",
  count: (count: number) => `${count} ${count === 1 ? "invoice" : "invoices"} in the list`,
  approveAll: (count: number) => `Process all (${count})`,
  uploadInvoices: "Upload invoices",
  noApi: "No API is configured: background import is not available.",
  emailNotice:
    "PDFs received by email are only read once you approve them. Look at them first; delete anything that is not your invoice.",
  empty: "Nothing to review. Upload several PDFs at once; they are read in the background and appear here.",
  columns: {
    file: "File",
    invoice: "Invoice",
    status: "Status",
    received: "Received",
  },
  from: (sender: string) => ` · from ${sender}`,
  view: "View",
  approve: "Process",
  review: "Review",
  approvedOne: "Being read now.",
  approvedMany: (count: number) => `${count} invoices are being read now.`,
  requeued: "Queued again.",
  dismiss: "Remove from the list",
  dismissed: "Removed from the list.",
  notReadYet: "This invoice has not been read yet.",
};

export const imports = { nl, en };
