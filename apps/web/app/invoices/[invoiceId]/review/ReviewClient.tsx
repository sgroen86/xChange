"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";

import { Icon } from "../../../../components/Icon";
import { useToast } from "../../../../components/Toast";
import { isApiConfigured } from "../../../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../../../lib/auth";
import {
  editableFingerprint,
  emptyLine,
  exemptionReasonOf,
  localIssues,
  normalizeInvoice,
  setAtPath,
  setExemptionReason,
  toCanonicalXml,
  totalsOf,
  withCalculatedTotals,
  type CanonicalInvoice,
  type ReviewDraft,
} from "../../../../lib/canonical";
import {
  bookInvoice,
  checkInvoice,
  documentBlobUrl,
  exportInvoice,
  getInvoice,
  type RuleViolation,
} from "../../../../lib/invoices";
import { formatMoney } from "../../../../lib/money";
import { getPdf } from "../../../../lib/pdfStore";
import { matchRelation } from "../../../../lib/relationKeys";
import { listRelations, type Relation } from "../../../../lib/relations";
import { defaultRulesetOf, FALLBACK_SETTINGS, getSettings, type OrganizationSettings } from "../../../../lib/settings";
import { loadDraft, loadServerResult, saveDraft } from "../../../../lib/store";
import { CANONICAL_FORMAT, ExportPanel } from "./ExportPanel";
import { EditorContext } from "./fields";
import { InvoiceDetailsCard } from "./InvoiceDetailsCard";
import { LinesCard } from "./LinesCard";
import { PartyCard } from "./PartyCard";
import { PaymentCard } from "./PaymentCard";
import { TotalsCard } from "./TotalsCard";

type TabKey = "invoice" | "xml" | "notices";

type PdfState =
  | { status: "loading" }
  | { status: "ready"; url: string; name: string }
  | { status: "missing" };

/** Wait this long after the last keystroke before asking the server to check. */
const CHECK_DELAY_MS = 600;

function download(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}

export default function ReviewClient() {
  const pathname = usePathname() ?? "";
  const showToast = useToast();
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const readOnly = user?.role === "ReadOnly";

  const invoiceId = pathname.match(/\/invoices\/([^/]+)\/review/)?.[1] ?? "";

  /* Mounted with ssr:false, so the browser-only stores can be read straight
     into the initial state - no effect, no hydration mismatch. */
  const [draft, setDraft] = useState<ReviewDraft | null>(() => (invoiceId ? loadDraft(invoiceId) : null));
  const [server] = useState(() => (invoiceId ? loadServerResult(invoiceId) : null));
  const [tab, setTab] = useState<TabKey>("invoice");

  /** Fingerprint of the invoice as booked; null while it has never been booked. */
  const [bookedFingerprint, setBookedFingerprint] = useState<string | null>(null);
  const [booking, setBooking] = useState(false);

  const [settings, setSettings] = useState<OrganizationSettings>(FALLBACK_SETTINGS);
  const [chosenFormat, setChosenFormat] = useState<string | null>(null);
  const [relations, setRelations] = useState<Relation[]>([]);

  /* A known customer with its own format opens on that one (piece 4); the
     buyer is matched as the server does it, so editing the VAT number counts. */
  const buyer = draft?.invoice.buyer ?? null;
  const customer = useMemo(() => matchRelation(relations, buyer), [relations, buyer]);
  const customerFormat = settings.rulesets.find((r) => r.id === customer?.defaultRulesetId) ?? null;
  const format = chosenFormat ?? customerFormat?.id ?? defaultRulesetOf(settings).id;
  /** Tax categories, tax name and tax currency follow the chosen format; the canonical XML uses the default's. */
  const taxRuleset = settings.rulesets.find((r) => r.id === format) ?? defaultRulesetOf(settings);
  const taxName = taxRuleset.taxName === "btw" ? "Btw" : taxRuleset.taxName;
  const taxCategories = useMemo(
    () => taxRuleset.taxCategories.map((c) => ({ value: c.code, label: `${c.code} · ${c.label}` })),
    [taxRuleset],
  );

  const [violations, setViolations] = useState<RuleViolation[]>([]);
  const [checking, setChecking] = useState(false);
  const [checkFailed, setCheckFailed] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [officialWarnings, setOfficialWarnings] = useState<RuleViolation[]>([]);

  const [pdf, setPdf] = useState<PdfState>(() => (invoiceId ? { status: "loading" } : { status: "missing" }));

  /* The PDF is a Blob in IndexedDB, or on the server once booked. */
  useEffect(() => {
    if (!invoiceId) return;

    let objectUrl: string | null = null;
    let cancelled = false;

    getPdf(invoiceId)
      .then(async (record) => {
        if (cancelled) return;

        if (record) {
          objectUrl = URL.createObjectURL(record.blob);
          setPdf({ status: "ready", url: objectUrl, name: record.name });
          return;
        }

        const fromServer = await documentBlobUrl(invoiceId);
        if (cancelled) return;

        if (fromServer) {
          objectUrl = fromServer;
          setPdf({ status: "ready", url: fromServer, name: "document.pdf" });
        } else {
          setPdf({ status: "missing" });
        }
      })
      .catch(() => {
        if (!cancelled) setPdf({ status: "missing" });
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [invoiceId]);

  /* Whether this invoice is booked, and the booked version when this browser
     has no draft of its own - which is what makes an invoice opened from the
     list work in a fresh browser. */
  useEffect(() => {
    if (!invoiceId || !isApiConfigured) return;

    let cancelled = false;

    getInvoice(invoiceId)
      .then((stored) => {
        if (cancelled) return;
        const invoice = normalizeInvoice(stored.canonicalInvoice);
        setBookedFingerprint(editableFingerprint(invoice));
        setDraft(
          (current) =>
            current ?? {
              id: invoiceId,
              invoice,
              source: {
                fileName: stored.sourceFileName ?? "document.pdf",
                byteSize: 0,
                engine: stored.providerModel ?? "onbekend",
                extractedAt: stored.bookedAt ?? "",
              },
            },
        );
      })
      .catch(() => {
        // Not booked yet, or not found: the local draft (or the "not found" card) stands.
      });

    return () => {
      cancelled = true;
    };
  }, [invoiceId]);

  useEffect(() => {
    let cancelled = false;
    getSettings()
      .then((loaded) => {
        if (!cancelled) setSettings(loaded);
      })
      .catch(() => {
        // The fallback lists PEPPOL BIS 3.0, which is the system default anyway.
      });
    listRelations()
      .then((loaded) => {
        if (!cancelled) setRelations(loaded);
      })
      .catch(() => {
        // Without relations the organisation's format is the default; nothing else depends on them.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const invoice = draft?.invoice ?? null;
  const local = useMemo(() => (invoice ? localIssues(invoice) : []), [invoice]);
  const totals = useMemo(() => (invoice ? totalsOf(invoice) : null), [invoice]);
  const isRuleset = format !== CANONICAL_FORMAT;

  /* Ask the server what is still missing, shortly after the user stops typing.
     A newer edit aborts the request in flight. */
  useEffect(() => {
    if (!invoice || !isApiConfigured || !isRuleset || local.length > 0) return;

    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setChecking(true);
      checkInvoice(format, invoice, controller.signal)
        .then((found) => {
          setViolations(found);
          setCheckFailed(false);
        })
        .catch(() => {
          if (!controller.signal.aborted) setCheckFailed(true);
        })
        .finally(() => {
          if (!controller.signal.aborted) setChecking(false);
        });
    }, CHECK_DELAY_MS);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [invoice, format, isRuleset, local.length]);

  const invalid = useMemo(
    () => new Set([...(isRuleset ? violations.map((v) => v.path) : []), ...local.map((i) => i.path)]),
    [violations, local, isRuleset],
  );

  const dirty = invoice !== null && bookedFingerprint !== editableFingerprint(invoice);
  const booked = bookedFingerprint !== null;

  const update = (change: (current: CanonicalInvoice) => CanonicalInvoice) =>
    setDraft((current) => (current ? { ...current, invoice: change(current.invoice) } : current));

  const editor = useMemo(
    () =>
      invoice
        ? {
            invoice,
            set: (path: string, value: string | null) => update((current) => setAtPath(current, path, value)),
            invalid,
            readOnly,
          }
        : null,
    // update only closes over setDraft, which is stable.
    [invoice, invalid, readOnly],
  );

  const goTo = (path: string) => {
    setTab("invoice");
    window.setTimeout(() => {
      const field = document.getElementById(path);
      field?.scrollIntoView({ behavior: "smooth", block: "center" });
      field?.focus();
    }, 0);
  };

  const onSave = () => {
    if (!draft) return;
    saveDraft(draft);
    showToast("Concept opgeslagen in deze browser.", "success");
  };

  const onBook = async () => {
    if (!draft || booking) return;

    if (local.length > 0) {
      showToast("Corrigeer eerst de getallen die niet gelezen kunnen worden.", "danger");
      goTo(local[0].path);
      return;
    }

    if (pdf.status !== "ready") {
      showToast("De PDF is niet beschikbaar; upload hem opnieuw voordat je boekt.", "danger");
      return;
    }

    setBooking(true);
    try {
      const blob = await (await fetch(pdf.url)).blob();
      const stored = await bookInvoice({
        invoiceId,
        canonicalInvoice: draft.invoice,
        pdf: blob,
        sourceFileName: draft.source.fileName,
        providerModel: draft.source.engine,
      });
      setBookedFingerprint(editableFingerprint(normalizeInvoice(stored.canonicalInvoice)));
      saveDraft(draft);
      showToast("Factuur geboekt en opgeslagen.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Boeken is mislukt.", "danger");
    } finally {
      setBooking(false);
    }
  };

  const onDownload = async () => {
    if (!draft) return;

    if (!isRuleset) {
      const xml = toCanonicalXml(withCalculatedTotals(draft.invoice));
      download(new Blob([xml], { type: "application/xml;charset=utf-8" }), `${draft.invoice.invoiceNumber || "factuur"}.xml`);
      showToast("Canonieke XML gedownload.", "success");
      return;
    }

    setDownloading(true);
    try {
      const result = await exportInvoice(invoiceId, format);
      if (result.kind === "incomplete") {
        setViolations(result.violations);
        setOfficialWarnings([]);
        showToast("Het bestand voldoet niet aan de officiële regels. Zie de meldingen.", "warning");
        return;
      }
      download(result.blob, result.fileName);
      setOfficialWarnings(result.warnings);
      showToast(
        result.warnings.length > 0
          ? `${result.fileName} gedownload, met ${result.warnings.length} waarschuwing(en).`
          : `${result.fileName} gedownload.`,
        result.warnings.length > 0 ? "warning" : "success",
      );
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Exporteren is mislukt.", "danger");
    } finally {
      setDownloading(false);
    }
  };

  if (!draft || !invoice || !totals || !editor) {
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

  const notices = server?.warnings ?? [];
  const statusBadge = !booked ? (
    <span className="badge badge--warning">Niet geboekt</span>
  ) : dirty ? (
    <span className="badge badge--warning">Gewijzigd sinds boeken</span>
  ) : (
    <span className="badge badge--success">Geboekt</span>
  );

  return (
    <EditorContext.Provider value={editor}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileText" /> {invoice.invoiceNumber || "Concept zonder nummer"}
          </h1>
          <p className="page-header__subtitle">
            {draft.source.fileName} · {draft.source.engine} · Te betalen{" "}
            {formatMoney(totals.payable, invoice.currencyCode)}
          </p>
        </div>
        {!readOnly && (
          <div className="page-header__actions">
            <button type="button" className="btn btn--secondary" onClick={onSave}>
              <Icon name="save" /> Opslaan
            </button>
            <button type="button" className="btn btn--primary" onClick={() => void onBook()} disabled={booking}>
              <Icon name="check" /> {booking ? "Bezig…" : booked && !dirty ? "Opnieuw boeken" : "Boeken"}
            </button>
          </div>
        )}
      </div>

      <div className="review-split">
        <div className="pdf-pane">
          <div className="card">
            <div className="card__header">
              <h3>Bron-PDF</h3>
              <span className="badge badge--neutral">{draft.source.fileName}</span>
            </div>
            <div className="card__body card__body--flush">
              {pdf.status === "ready" && <iframe className="pdf-frame" src={pdf.url} title="Bron-PDF" />}
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

        <div className="stack">
          <ExportPanel
            rulesets={settings.rulesets}
            format={format}
            onFormat={setChosenFormat}
            defaultNote={
              chosenFormat === null && customerFormat
                ? `Standaardformaat van ${customer?.name ?? "deze afnemer"}.`
                : null
            }
            localIssues={local}
            violations={isRuleset ? violations : []}
            officialWarnings={isRuleset ? officialWarnings : []}
            checking={checking}
            checkFailed={checkFailed}
            exportable={booked && !dirty}
            apiConfigured={isApiConfigured}
            downloading={downloading}
            onDownload={() => void onDownload()}
            onGoTo={goTo}
          />

          <div className="tabs">
            <button type="button" className={`tab ${tab === "invoice" ? "tab--active" : ""}`.trim()} onClick={() => setTab("invoice")}>
              Factuur
            </button>
            <button type="button" className={`tab ${tab === "xml" ? "tab--active" : ""}`.trim()} onClick={() => setTab("xml")}>
              Canonieke XML
            </button>
            <button type="button" className={`tab ${tab === "notices" ? "tab--active" : ""}`.trim()} onClick={() => setTab("notices")}>
              Meldingen{notices.length > 0 ? ` (${notices.length})` : ""}
            </button>
          </div>

          {tab === "invoice" && (
            <div className="stack form">
              <InvoiceDetailsCard
                creditNote={invoice.typeCode === "381"}
                status={statusBadge}
                rate={
                  taxRuleset.taxCurrency && invoice.currencyCode && invoice.currencyCode !== taxRuleset.taxCurrency
                    ? { from: invoice.currencyCode, to: taxRuleset.taxCurrency }
                    : null
                }
              />
              <PartyCard side="seller" title="Verkoper (uw organisatie)" />
              <PartyCard side="buyer" title="Afnemer" />
              <LinesCard
                lines={invoice.lines}
                nets={totals.lineNet}
                taxCategories={taxCategories}
                taxName={taxName}
                readOnly={readOnly}
                onAdd={() => update((current) => ({ ...current, lines: [...current.lines, emptyLine()] }))}
                onRemove={(index) =>
                  update((current) => ({ ...current, lines: current.lines.filter((_, i) => i !== index) }))
                }
              />
              <PaymentCard />
              <TotalsCard
                totals={totals}
                currency={invoice.currencyCode}
                taxName={taxName}
                taxCurrency={
                  taxRuleset.taxCurrency && invoice.currencyCode !== taxRuleset.taxCurrency ? taxRuleset.taxCurrency : null
                }
                documentAdjustments={invoice.allowancesAndCharges}
                reasonFor={(category, rate) => exemptionReasonOf(invoice, category, rate)}
                onReason={(category, rate, reason) =>
                  update((current) => setExemptionReason(current, category, rate, reason))
                }
                invalid={invalid}
                readOnly={readOnly}
              />
            </div>
          )}

          {tab === "xml" && (
            <div className="card">
              <div className="card__header">
                <h3>Canonieke XML</h3>
                <span className="badge badge--neutral">Deterministisch gegenereerd</span>
              </div>
              <div className="card__body card__body--flush">
                <pre className="xml-view">{toCanonicalXml(withCalculatedTotals(invoice))}</pre>
              </div>
            </div>
          )}

          {tab === "notices" && (
            <div className="card">
              <div className="card__header">
                <h3>Meldingen bij het uitlezen</h3>
              </div>
              <div className="card__body card__body--flush">
                {notices.length === 0 ? (
                  <div className="card__body">
                    <div className="alert alert--success">
                      <Icon name="check" />
                      <div>Geen meldingen.</div>
                    </div>
                  </div>
                ) : (
                  <div className="issue-list">
                    {notices.map((notice) => (
                      <div className="issue" key={`${notice.code}-${notice.path}`}>
                        <span
                          className={`badge ${
                            notice.severity === "error"
                              ? "badge--danger"
                              : notice.severity === "info"
                                ? "badge--info"
                                : "badge--warning"
                          }`}
                        >
                          {notice.severity === "error" ? "Fout" : notice.severity === "info" ? "Ingevuld" : "Let op"}
                        </span>
                        <div className="issue__body">
                          <div className="issue__message">{notice.message}</div>
                          <div className="issue__path">
                            {notice.code} · {notice.path}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </EditorContext.Provider>
  );
}
