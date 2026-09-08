"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState } from "react";

import { Icon } from "../../../../components/Icon";
import { useToast } from "../../../../components/Toast";
import {
  calculate,
  emptyLine,
  toCanonicalXml,
  validate,
  type CanonicalInvoiceDraft,
  type CanonicalLine,
  type CanonicalParty,
  type ValidationIssue,
} from "../../../../lib/canonical";
import { formatAmount, formatScaled, SCALE_AMOUNT } from "../../../../lib/money";
import { getPdf } from "../../../../lib/pdfStore";
import { loadDraft, loadServerResult, saveDraft } from "../../../../lib/store";

type TabKey = "invoice" | "xml" | "warnings";

type PdfState =
  | { status: "loading" }
  | { status: "ready"; url: string; name: string }
  | { status: "missing" };

export default function ReviewClient() {
  const pathname = usePathname() ?? "";
  const showToast = useToast();

  const invoiceId = pathname.match(/\/invoices\/([^/]+)\/review/)?.[1] ?? "";

  /* This component is mounted with ssr:false, so the browser-only stores can be
     read straight into the initial state — no effect, no hydration mismatch. */
  const [draft, setDraft] = useState<CanonicalInvoiceDraft | null>(() =>
    invoiceId ? loadDraft(invoiceId) : null,
  );
  const [server] = useState(() => (invoiceId ? loadServerResult(invoiceId) : null));
  const [tab, setTab] = useState<TabKey>("invoice");
  const [validatedAt, setValidatedAt] = useState<string | null>(null);

  /* The PDF is a Blob in IndexedDB, so unlike the draft it cannot be read
     synchronously. The object URL is created here and revoked on unmount. */
  const [pdf, setPdf] = useState<PdfState>(() =>
    invoiceId ? { status: "loading" } : { status: "missing" },
  );

  useEffect(() => {
    if (!invoiceId) return;

    let objectUrl: string | null = null;
    let cancelled = false;

    getPdf(invoiceId)
      .then((record) => {
        if (cancelled) return;
        if (!record) {
          setPdf({ status: "missing" });
          return;
        }
        objectUrl = URL.createObjectURL(record.blob);
        setPdf({ status: "ready", url: objectUrl, name: record.name });
      })
      .catch(() => {
        if (!cancelled) setPdf({ status: "missing" });
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [invoiceId]);

  const totals = useMemo(() => (draft ? calculate(draft) : null), [draft]);

  /* The API's XML and warnings are authoritative: they come from the same
     deterministic server code that will produce the final document. The local
     versions are only used when the extraction was mocked, or after the user
     has edited fields the server has not seen. */
  const localIssues = useMemo(() => (draft ? validate(draft) : []), [draft]);

  const issues: ValidationIssue[] = useMemo(() => {
    if (!server) return localIssues;
    return server.warnings.map((warning) => ({
      severity: warning.severity === "error" ? "error" : "warning",
      code: warning.code,
      path: warning.path,
      message: warning.message,
    }));
  }, [server, localIssues]);

  const xml = useMemo(
    () => server?.canonicalXml ?? (draft ? toCanonicalXml(draft) : ""),
    [server, draft],
  );

  const errorCount = issues.filter((issue) => issue.severity === "error").length;
  const warningCount = issues.length - errorCount;

  const update = (patch: Partial<CanonicalInvoiceDraft>) =>
    setDraft((current) => (current ? { ...current, ...patch } : current));

  const updateParty = (side: "supplier" | "customer", patch: Partial<CanonicalParty>) =>
    setDraft((current) =>
      current ? { ...current, [side]: { ...current[side], ...patch } } : current,
    );

  const updateLine = (id: string, patch: Partial<CanonicalLine>) =>
    setDraft((current) =>
      current
        ? {
            ...current,
            lines: current.lines.map((line) => (line.id === id ? { ...line, ...patch } : line)),
          }
        : current,
    );

  const addLine = () =>
    setDraft((current) =>
      current ? { ...current, lines: [...current.lines, emptyLine(current.lines.length + 1)] } : current,
    );

  const removeLine = (id: string) =>
    setDraft((current) =>
      current ? { ...current, lines: current.lines.filter((line) => line.id !== id) } : current,
    );

  const onSave = () => {
    if (!draft) return;
    saveDraft(draft);
    showToast("Concept opgeslagen.", "success");
  };

  const onValidate = () => {
    if (!draft) return;
    setValidatedAt(new Date().toISOString());

    if (errorCount > 0) {
      setTab("warnings");
      showToast(`${errorCount} fout(en) gevonden. Corrigeer deze eerst.`, "danger");
      return;
    }
    if (warningCount > 0) {
      setTab("warnings");
      showToast(`Geldig, met ${warningCount} waarschuwing(en).`, "warning");
      return;
    }
    showToast("Factuur is geldig.", "success");
  };

  const onDownload = () => {
    if (!draft) return;
    if (errorCount > 0) {
      showToast("XML niet gedownload: corrigeer eerst de fouten.", "danger");
      setTab("warnings");
      return;
    }

    const blob = new Blob([xml], { type: "application/xml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${draft.number || "factuur"}.xml`;
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    URL.revokeObjectURL(url);
    showToast("XML gedownload.", "success");
  };

  if (!draft) {
    return (
      <>
        <div className="page-header">
          <div>
            <h1>
              <Icon name="fileText" /> Factuur beoordelen
            </h1>
            <p className="page-header__subtitle">Concept niet gevonden</p>
          </div>
        </div>
        <div className="card">
          <div className="card__body">
            <div className="alert alert--warning">
              <Icon name="info" />
              <div>
                Er is geen concept voor <code>{invoiceId}</code> in deze sessie. Upload de PDF
                opnieuw om een nieuw concept te maken.
              </div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--primary" href="/upload">
                <Icon name="fileUp" /> Naar uploaden
              </Link>
            </div>
          </div>
        </div>
      </>
    );
  }

  const currency = draft.currency || "EUR";

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileText" /> {draft.number || "Concept zonder nummer"}
          </h1>
          <p className="page-header__subtitle">
            {draft.extraction.sourceFileName} · {draft.extraction.engine} ·{" "}
            {errorCount > 0 ? (
              <span className="text-danger">{errorCount} fout(en)</span>
            ) : (
              <span>Geen fouten</span>
            )}
          </p>
        </div>
        <div className="page-header__actions">
          <button type="button" className="btn btn--secondary" onClick={onSave}>
            <Icon name="save" /> Opslaan
          </button>
          <button type="button" className="btn btn--secondary" onClick={onValidate}>
            <Icon name="check" /> Valideren
          </button>
          <button type="button" className="btn btn--primary" onClick={onDownload}>
            <Icon name="download" /> XML downloaden
          </button>
        </div>
      </div>

      <div className="review-split">
        {/* ---------- Left: PDF ---------- */}
        <div className="pdf-pane">
          <div className="card">
            <div className="card__header">
              <h3>Bron-PDF</h3>
              <span className="badge badge--neutral">{draft.extraction.sourceFileName}</span>
            </div>
            <div className="card__body card__body--flush">
              {pdf.status === "ready" && (
                <iframe className="pdf-frame" src={pdf.url} title="Bron-PDF" />
              )}
              {pdf.status === "loading" && (
                <div className="pdf-empty">
                  <span className="text-soft text-sm">Laden&hellip;</span>
                </div>
              )}
              {pdf.status === "missing" && (
                <div className="pdf-empty">
                  <Icon name="eye" />
                  <div>
                    De PDF is niet gevonden in de opslag van deze browser.
                    <br />
                    Upload het bestand opnieuw om het hier te bekijken.
                  </div>
                  <Link className="btn btn--secondary btn--sm" href="/upload">
                    Opnieuw uploaden
                  </Link>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* ---------- Right: canonical invoice ---------- */}
        <div>
          <div className="tabs">
            <button
              type="button"
              className={`tab ${tab === "invoice" ? "tab--active" : ""}`.trim()}
              onClick={() => setTab("invoice")}
            >
              Factuur
            </button>
            <button
              type="button"
              className={`tab ${tab === "xml" ? "tab--active" : ""}`.trim()}
              onClick={() => setTab("xml")}
            >
              Canonieke XML
            </button>
            <button
              type="button"
              className={`tab ${tab === "warnings" ? "tab--active" : ""}`.trim()}
              onClick={() => setTab("warnings")}
            >
              Waarschuwingen{issues.length > 0 ? ` (${issues.length})` : ""}
            </button>
          </div>

          {tab === "invoice" && (
            <div className="stack form">
              <div className="card">
                <div className="card__header">
                  <h3>Factuurgegevens</h3>
                  <span className={`badge ${draft.validated ? "badge--success" : "badge--warning"}`}>
                    {draft.validated ? "Gevalideerd" : "Concept"}
                  </span>
                </div>
                <div className="card__body">
                  <div className="form__row">
                    <div className="form__group">
                      <label htmlFor="number">Factuurnummer</label>
                      <input
                        id="number"
                        type="text"
                        value={draft.number}
                        onChange={(event) => update({ number: event.target.value })}
                      />
                    </div>
                    <div className="form__group">
                      <label htmlFor="currency">Valuta</label>
                      <select
                        id="currency"
                        value={draft.currency}
                        onChange={(event) => update({ currency: event.target.value })}
                      >
                        <option value="EUR">EUR</option>
                        <option value="USD">USD</option>
                        <option value="GBP">GBP</option>
                      </select>
                    </div>
                  </div>
                  <div className="form__row">
                    <div className="form__group">
                      <label htmlFor="issueDate">Factuurdatum</label>
                      <input
                        id="issueDate"
                        type="date"
                        value={draft.issueDate}
                        onChange={(event) => update({ issueDate: event.target.value })}
                      />
                    </div>
                    <div className="form__group">
                      <label htmlFor="dueDate">Vervaldatum</label>
                      <input
                        id="dueDate"
                        type="date"
                        value={draft.dueDate}
                        onChange={(event) => update({ dueDate: event.target.value })}
                      />
                    </div>
                  </div>
                  <div className="form__group">
                    <label htmlFor="note">Opmerking</label>
                    <textarea
                      id="note"
                      value={draft.note}
                      onChange={(event) => update({ note: event.target.value })}
                    />
                  </div>
                </div>
              </div>

              {(["supplier", "customer"] as const).map((side) => (
                <div className="card" key={side}>
                  <div className="card__header">
                    <h3>{side === "supplier" ? "Leverancier" : "Afnemer"}</h3>
                  </div>
                  <div className="card__body">
                    <div className="form__row">
                      <div className="form__group">
                        <label htmlFor={`${side}-name`}>Naam</label>
                        <input
                          id={`${side}-name`}
                          type="text"
                          value={draft[side].name}
                          onChange={(event) => updateParty(side, { name: event.target.value })}
                        />
                      </div>
                      <div className="form__group">
                        <label htmlFor={`${side}-vat`}>Btw-nummer</label>
                        <input
                          id={`${side}-vat`}
                          type="text"
                          value={draft[side].vatId}
                          onChange={(event) => updateParty(side, { vatId: event.target.value })}
                        />
                      </div>
                    </div>
                    <div className="form__row">
                      <div className="form__group">
                        <label htmlFor={`${side}-street`}>Straat</label>
                        <input
                          id={`${side}-street`}
                          type="text"
                          value={draft[side].street}
                          onChange={(event) => updateParty(side, { street: event.target.value })}
                        />
                      </div>
                      <div className="form__group">
                        <label htmlFor={`${side}-zip`}>Postcode</label>
                        <input
                          id={`${side}-zip`}
                          type="text"
                          value={draft[side].postalCode}
                          onChange={(event) => updateParty(side, { postalCode: event.target.value })}
                        />
                      </div>
                      <div className="form__group">
                        <label htmlFor={`${side}-city`}>Plaats</label>
                        <input
                          id={`${side}-city`}
                          type="text"
                          value={draft[side].city}
                          onChange={(event) => updateParty(side, { city: event.target.value })}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              ))}

              <div className="card">
                <div className="card__header">
                  <h3>Factuurregels</h3>
                  <button type="button" className="btn btn--secondary btn--sm" onClick={addLine}>
                    <Icon name="filePlus" /> Regel toevoegen
                  </button>
                </div>
                <div className="card__body card__body--flush">
                  <div className="table-wrap">
                    <table className="table">
                      <thead>
                        <tr>
                          <th>Omschrijving</th>
                          <th>Aantal</th>
                          <th>Prijs</th>
                          <th>Btw %</th>
                          <th className="cell-amount">Netto</th>
                          <th />
                        </tr>
                      </thead>
                      <tbody>
                        {draft.lines.map((line) => (
                          <tr key={line.id}>
                            <td>
                              <input
                                className="cell-input"
                                type="text"
                                value={line.description}
                                aria-label="Omschrijving"
                                onChange={(event) =>
                                  updateLine(line.id, { description: event.target.value })
                                }
                              />
                            </td>
                            <td>
                              <input
                                className="cell-input cell-input--amount"
                                type="text"
                                inputMode="decimal"
                                value={line.quantity}
                                aria-label="Aantal"
                                onChange={(event) =>
                                  updateLine(line.id, { quantity: event.target.value })
                                }
                              />
                            </td>
                            <td>
                              <input
                                className="cell-input cell-input--amount"
                                type="text"
                                inputMode="decimal"
                                value={line.unitPrice}
                                aria-label="Prijs"
                                onChange={(event) =>
                                  updateLine(line.id, { unitPrice: event.target.value })
                                }
                              />
                            </td>
                            <td>
                              <input
                                className="cell-input cell-input--amount"
                                type="text"
                                inputMode="decimal"
                                value={line.vatRate}
                                aria-label="Btw-percentage"
                                onChange={(event) =>
                                  updateLine(line.id, { vatRate: event.target.value })
                                }
                              />
                            </td>
                            <td className="cell-amount">
                              {formatScaled(totals?.lineNetUnits[line.id] ?? 0, SCALE_AMOUNT)}
                            </td>
                            <td>
                              <button
                                type="button"
                                className="btn btn--ghost btn--icon"
                                onClick={() => removeLine(line.id)}
                                aria-label="Regel verwijderen"
                              >
                                <Icon name="trash" />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>Totalen</h3>
                </div>
                <div className="card__body">
                  <div className="totals">
                    <div className="totals__row">
                      <span>Totaal exclusief btw</span>
                      <span className="totals__value">
                        {formatAmount(totals?.taxExclusiveUnits ?? 0, currency)}
                      </span>
                    </div>
                    {totals?.vatBreakdown.map((bucket) => (
                      <div className="totals__row" key={bucket.rate}>
                        <span>
                          Btw {bucket.rate}% over{" "}
                          {formatAmount(bucket.taxableUnits, currency)}
                        </span>
                        <span className="totals__value">
                          {formatAmount(bucket.taxUnits, currency)}
                        </span>
                      </div>
                    ))}
                    <div className="totals__row totals__row--grand">
                      <span>Totaal inclusief btw</span>
                      <span className="totals__value">
                        {formatAmount(totals?.taxInclusiveUnits ?? 0, currency)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}

          {tab === "xml" && (
            <div className="card">
              <div className="card__header">
                <h3>Canonieke XML</h3>
                <span className="badge badge--neutral">Deterministisch gegenereerd</span>
              </div>
              <div className="card__body card__body--flush">
                <pre className="xml-view">{xml}</pre>
              </div>
            </div>
          )}

          {tab === "warnings" && (
            <div className="card">
              <div className="card__header">
                <h3>Validatie</h3>
                <span className="badge badge--neutral">
                  {errorCount} fout(en) · {warningCount} waarschuwing(en)
                </span>
              </div>
              <div className="card__body card__body--flush">
                {issues.length === 0 ? (
                  <div className="card__body">
                    <div className="alert alert--success">
                      <Icon name="check" />
                      <div>Geen problemen gevonden.</div>
                    </div>
                  </div>
                ) : (
                  <div className="issue-list">
                    {issues.map((issue) => (
                      <div className="issue" key={`${issue.code}-${issue.path}`}>
                        <span
                          className={`badge ${
                            issue.severity === "error" ? "badge--danger" : "badge--warning"
                          }`}
                        >
                          {issue.severity === "error" ? "Fout" : "Let op"}
                        </span>
                        <div className="issue__body">
                          <div className="issue__message">{issue.message}</div>
                          <div className="issue__path">
                            {issue.code} · {issue.path}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {validatedAt && (
            <p className="text-muted text-xs" style={{ marginTop: "var(--space-3)" }}>
              Laatst gevalideerd: {new Date(validatedAt).toLocaleTimeString("nl-NL")}
            </p>
          )}
        </div>
      </div>
    </>
  );
}
