/** Texts for the invoices area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Facturen",
  count: (count: number) => `${count} factuur(en)`,
  newInvoice: "Nieuwe factuur",
  empty: "Er zijn nog geen facturen. Upload er een en klik op Opslaan of Boeken.",
  columns: {
    number: "Nummer",
    buyer: "Afnemer",
    date: "Datum",
    amount: "Bedrag",
    status: "Status",
    actions: "Acties",
  },
  noNumber: "zonder nummer",
  booked: "Geboekt",
  draft: "Concept",
  deleteInvoice: "Factuur verwijderen",
  bookedInvoice: "geboekte factuur",
  draftInvoice: "concept",
  deleteConfirm: (what: string, number: string) =>
    `De ${what} ${number} en de PDF verwijderen? Dit kan niet ongedaan worden gemaakt.`,
  deleted: "Factuur verwijderd.",
  deleteFailed: "Verwijderen is mislukt.",
};

const en: typeof nl = {
  title: "Invoices",
  count: (count: number) => `${count} ${count === 1 ? "invoice" : "invoices"}`,
  newInvoice: "New invoice",
  empty: "There are no invoices yet. Upload one and click Save or Book.",
  columns: {
    number: "Number",
    buyer: "Customer",
    date: "Date",
    amount: "Amount",
    status: "Status",
    actions: "Actions",
  },
  noNumber: "no number",
  booked: "Booked",
  draft: "Draft",
  deleteInvoice: "Delete invoice",
  bookedInvoice: "booked invoice",
  draftInvoice: "draft",
  deleteConfirm: (what: string, number: string) => `Delete the ${what} ${number} and its PDF? This cannot be undone.`,
  deleted: "Invoice deleted.",
  deleteFailed: "Deleting failed.",
};

export const invoices = { nl, en };
