"use client";

/**
 * Inputs bound to the canonical invoice by field path. The path is also the
 * input's id, which is how a rule violation from the server finds its field.
 */

import { createContext, useContext } from "react";

import { getAtPath, type CanonicalInvoice } from "../../../../lib/canonical";

export interface Option {
  value: string;
  label: string;
}

interface Editor {
  invoice: CanonicalInvoice;
  set: (path: string, value: string | null) => void;
  /** Paths with a violation or a local issue; their inputs are marked. */
  invalid: ReadonlySet<string>;
  readOnly: boolean;
}

export const EditorContext = createContext<Editor | null>(null);

function useEditor(): Editor {
  const editor = useContext(EditorContext);
  if (!editor) throw new Error("Field used outside an EditorContext.");
  return editor;
}

/** Keeps a value the list does not know, so nothing read from a PDF silently disappears. */
function withCurrent(options: Option[], current: string | null): Option[] {
  return current && !options.some((o) => o.value === current)
    ? [...options, { value: current, label: current }]
    : options;
}

interface FieldProps {
  path: string;
  label: string;
  type?: "text" | "date" | "email";
  placeholder?: string;
  hint?: string;
}

export function TextField({ path, label, type = "text", placeholder, hint }: FieldProps) {
  const { invoice, set, invalid, readOnly } = useEditor();
  return (
    <div className="form__group">
      <label htmlFor={path}>{label}</label>
      <input
        id={path}
        type={type}
        value={getAtPath(invoice, path) ?? ""}
        placeholder={placeholder}
        readOnly={readOnly}
        className={invalid.has(path) ? "input--invalid" : undefined}
        aria-invalid={invalid.has(path) || undefined}
        onChange={(event) => set(path, event.target.value)}
      />
      {hint && <div className="form-help">{hint}</div>}
    </div>
  );
}

export function TextAreaField({ path, label }: FieldProps) {
  const { invoice, set, invalid, readOnly } = useEditor();
  return (
    <div className="form__group">
      <label htmlFor={path}>{label}</label>
      <textarea
        id={path}
        value={getAtPath(invoice, path) ?? ""}
        readOnly={readOnly}
        className={invalid.has(path) ? "input--invalid" : undefined}
        onChange={(event) => set(path, event.target.value)}
      />
    </div>
  );
}

export function SelectField({ path, label, options }: FieldProps & { options: Option[] }) {
  const { invoice, set, invalid, readOnly } = useEditor();
  const current = getAtPath(invoice, path);
  return (
    <div className="form__group">
      <label htmlFor={path}>{label}</label>
      <select
        id={path}
        value={current ?? ""}
        disabled={readOnly}
        className={invalid.has(path) ? "input--invalid" : undefined}
        onChange={(event) => set(path, event.target.value)}
      >
        <option value="">—</option>
        {withCurrent(options, current).map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  );
}

/** A compact input for a table cell. */
export function CellInput({ path, label, amount = false }: { path: string; label: string; amount?: boolean }) {
  const { invoice, set, invalid, readOnly } = useEditor();
  const classes = ["cell-input", amount ? "cell-input--amount" : "", invalid.has(path) ? "input--invalid" : ""];
  return (
    <input
      id={path}
      className={classes.filter(Boolean).join(" ")}
      type="text"
      inputMode={amount ? "decimal" : undefined}
      value={getAtPath(invoice, path) ?? ""}
      aria-label={label}
      readOnly={readOnly}
      onChange={(event) => set(path, event.target.value)}
    />
  );
}

export function CellSelect({ path, label, options }: { path: string; label: string; options: Option[] }) {
  const { invoice, set, invalid, readOnly } = useEditor();
  const current = getAtPath(invoice, path);
  return (
    <select
      id={path}
      className={`cell-input ${invalid.has(path) ? "input--invalid" : ""}`.trim()}
      value={current ?? ""}
      aria-label={label}
      disabled={readOnly}
      onChange={(event) => set(path, event.target.value)}
    >
      <option value="">—</option>
      {withCurrent(options, current).map((option) => (
        <option key={option.value} value={option.value}>
          {option.value}
        </option>
      ))}
    </select>
  );
}

/* --- Code lists offered on the screen ---------------------------------- */

export const TYPE_CODES: Option[] = [
  { value: "380", label: "Factuur (380)" },
  { value: "381", label: "Creditnota (381)" },
];

export const CURRENCIES: Option[] = ["EUR", "USD", "GBP", "SGD"].map((code) => ({ value: code, label: code }));

/** Electronic address schemes (EAS) that matter for Dutch, Belgian and Singapore trading partners. */
export const ENDPOINT_SCHEMES: Option[] = [
  { value: "0106", label: "0106 · KvK-nummer" },
  { value: "0190", label: "0190 · OIN (overheid)" },
  { value: "9944", label: "9944 · Nederlands btw-nummer" },
  { value: "0208", label: "0208 · Belgisch ondernemingsnummer" },
  { value: "9925", label: "9925 · Belgisch btw-nummer" },
  { value: "0088", label: "0088 · GLN" },
  { value: "0195", label: "0195 · Singapore UEN" },
];

export const REGISTRATION_SCHEMES: Option[] = [
  { value: "0106", label: "0106 · KvK" },
  { value: "0190", label: "0190 · OIN" },
  { value: "0208", label: "0208 · Belgisch ondernemingsnummer" },
  { value: "0195", label: "0195 · Singapore UEN" },
];

export const UNIT_CODES: Option[] = [
  { value: "C62", label: "stuk" },
  { value: "HUR", label: "uur" },
  { value: "DAY", label: "dag" },
  { value: "MON", label: "maand" },
  { value: "KGM", label: "kilogram" },
  { value: "MTR", label: "meter" },
  { value: "LTR", label: "liter" },
];

/** Direct debit (49, 59) is left out: it needs a mandate reference xChange does not record. */
export const PAYMENT_MEANS: Option[] = [
  { value: "30", label: "30 · Overschrijving" },
  { value: "58", label: "58 · SEPA-overschrijving" },
  { value: "48", label: "48 · Betaalkaart" },
  { value: "57", label: "57 · Doorlopende opdracht" },
];
