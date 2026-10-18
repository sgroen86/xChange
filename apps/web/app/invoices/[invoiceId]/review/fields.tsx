"use client";

/**
 * Inputs bound to the canonical invoice by field path. The path is also the
 * input's id, which is how a rule violation from the server finds its field.
 */

import { createContext, useContext } from "react";

import { getAtPath, type CanonicalInvoice } from "../../../../lib/canonical";
import { type Dictionary } from "../../../../lib/i18n";

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
/* Functions of the dictionary, so their labels follow the language. */

export const typeCodes = (t: Dictionary): Option[] => [
  { value: "380", label: `${t.review.codes.invoice} (380)` },
  { value: "381", label: `${t.review.codes.creditNote} (381)` },
];

export const CURRENCIES: Option[] = ["EUR", "USD", "GBP", "SGD"].map((code) => ({ value: code, label: code }));

/**
 * "code · name" in the order given. The order is spelled out because object
 * keys that look like integers ("9944", "30") would otherwise be sorted.
 */
function coded<Code extends string>(order: readonly Code[], names: Record<Code, string>): Option[] {
  return order.map((value) => ({ value, label: `${value} · ${names[value]}` }));
}

/** Electronic address schemes (EAS) that matter for Dutch, Belgian, British and Singapore trading partners. */
export const endpointSchemes = (t: Dictionary): Option[] =>
  coded(["0106", "0190", "9944", "0208", "9925", "9932", "0088", "0195"] as const, t.review.codes.endpoint);

export const registrationSchemes = (t: Dictionary): Option[] =>
  coded(["0106", "0190", "0208", "0195"] as const, t.review.codes.registration);


export const unitCodes = (t: Dictionary): Option[] =>
  (["C62", "HUR", "DAY", "MON", "KGM", "MTR", "LTR"] as const).map((value) => ({ value, label: t.review.codes.units[value] }));

/** Direct debit (49, 59) is left out: it needs a mandate reference xChange does not record. */
export const paymentMeans = (t: Dictionary): Option[] => coded(["30", "58", "48", "57"] as const, t.review.codes.paymentMeans);
