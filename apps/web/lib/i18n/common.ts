/** Texts used across the app. Area-specific texts live in their own file next to this one. */

const nl = {
  save: "Opslaan",
  cancel: "Annuleren",
  delete: "Verwijderen",
  close: "Sluiten",
  loading: "Laden…",
  busy: "Bezig…",
  retry: "Opnieuw",
  none: "geen",
  yes: "Ja",
  no: "Nee",
  noApi: "Er is geen API geconfigureerd.",
  apiUnreachable: "De API is niet bereikbaar.",
  apiStatus: (status: number) => `De API gaf status ${status}.`,
  loadFailed: "Laden is mislukt.",
  saveFailed: "Opslaan is mislukt.",
  somethingFailed: "Dat is mislukt.",
  language: "Taal",
};

const en: typeof nl = {
  save: "Save",
  cancel: "Cancel",
  delete: "Delete",
  close: "Close",
  loading: "Loading…",
  busy: "Working…",
  retry: "Retry",
  none: "none",
  yes: "Yes",
  no: "No",
  noApi: "No API is configured.",
  apiUnreachable: "The API cannot be reached.",
  apiStatus: (status: number) => `The API returned status ${status}.`,
  loadFailed: "Loading failed.",
  saveFailed: "Saving failed.",
  somethingFailed: "That did not work.",
  language: "Language",
};

export const common = { nl, en };
