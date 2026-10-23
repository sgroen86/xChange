"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";

import { Icon } from "../../../../components/Icon";
import { useToast } from "../../../../components/Toast";
import { ApiError, isApiConfigured } from "../../../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../../../lib/auth";
import { useLang, useText } from "../../../../lib/i18n";
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
  saveInvoiceDraft,
  checkInvoice,
  lookupReceiver,
  type ReceiverLookup,
  documentBlobUrl,
  exportInvoice,
  getInvoice,
  getUsage,
  type RuleViolation,
} from "../../../../lib/invoices";
import { formatMoney } from "../../../../lib/money";
import { getPdf } from "../../../../lib/pdfStore";
import { chooseFormat } from "../../../../lib/formatChoice";
import { dismissImport } from "../../../../lib/imports";
import { matchRelation } from "../../../../lib/relationKeys";
import { listRelations, type Relation } from "../../../../lib/relations";
import { defaultRulesetOf, FALLBACK_SETTINGS, getSettings, type OrganizationSettings } from "../../../../lib/settings";
import { loadDraft, loadServerResult, saveDraft } from "../../../../lib/store";
import { CANONICAL_FORMAT, ExportPanel } from "./ExportPanel";
import { EditorContext } from "./fields";
import { InvoiceDetailsCard } from "./InvoiceDetailsCard";
import { LinesCard } from "./LinesCard";
import { PartyCard } from "./PartyCard";
import { ReceiverStatus } from "./ReceiverStatus";
import { PaymentCard } from "./PaymentCard";
import { TotalsCard } from "./TotalsCard";

type TabKey = "invoice" | "xml" | "notices";

type PdfState =
  | { status: "loading" }
  | { status: "ready"; url: string; name: string }
  | { status: "missing" };

/** Wait this long after the last keystroke before asking the server to check. */
const CHECK_DELAY_MS = 600;
/** The PEPPOL lookup waits a little longer: numbers are typed in full before they mean anything. */
const LOOKUP_DELAY_MS = 900;

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
  const lang = useLang();
  const t = useText();
  const text = t.review;
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
  const [saving, setSaving] = useState(false);

  const [settings, setSettings] = useState<OrganizationSettings>(FALLBACK_SETTINGS);
  const [chosenFormat, setChosenFormat] = useState<string | null>(null);
  const [relations, setRelations] = useState<Relation[]>([]);
  // Assumed until the usage arrives; the server refuses with 402 either way.
  const [canExport, setCanExport] = useState(true);

  /* A known customer with its own format opens on that one (piece 4); the
     buyer is matched as the server does it, so editing the VAT number counts. */
  const buyer = draft?.invoice.buyer ?? null;
  const customer = useMemo(() => matchRelation(relations, buyer), [relations, buyer]);
  const customerFormat = settings.rulesets.find((r) => r.id === customer?.defaultRulesetId) ?? null;

  /* Whether the buyer can receive e-invoices, looked up on PEPPOL by its
     numbers shortly after they stop changing. Never blocks anything. */
  const [receiver, setReceiver] = useState<ReceiverLookup | null>(null);
  const [lookingUp, setLookingUp] = useState(false);
  const [endpointFilled, setEndpointFilled] = useState(false);
  const lookupKey = buyer
    ? JSON.stringify([
        buyer.vatIdentifier,
        buyer.legalRegistrationId,
        buyer.legalRegistrationScheme,
        buyer.electronicAddress,
        buyer.electronicAddressScheme,
        buyer.address?.countryCode ?? null,
      ])
    : null;

  const receiverAccepts =
    receiver?.status === "found"
      ? settings.rulesets.map((r) => r.id).filter((id) => receiver.acceptedRulesetIds.includes(id))
      : null;
  const formatChoice = chooseFormat({
    chosen: chosenFormat,
    customerFormat: customerFormat?.id ?? null,
    receiverAccepts,
    organizationDefault: defaultRulesetOf(settings).id,
  });
  const format = formatChoice.id;
  /** Tax categories, tax name and tax currency follow the chosen format; the canonical XML uses the default's. */
  const taxRuleset = settings.rulesets.find((r) => r.id === format) ?? defaultRulesetOf(settings);
  const taxName = taxRuleset.taxName === "btw" ? text.vat : taxRuleset.taxName;
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
        // A saved concept is on the server but not booked.
        if (stored.status === "Booked") setBookedFingerprint(editableFingerprint(invoice));
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
    if (isApiConfigured) {
      getUsage()
        .then((usage) => {
          if (!cancelled) setCanExport(usage.canExport);
        })
        .catch(() => undefined);
    }
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

  useEffect(() => {
    if (!lookupKey || !isApiConfigured) return;

    const [vatIdentifier, legalRegistrationId, legalRegistrationScheme, electronicAddress, electronicAddressScheme, countryCode] =
      JSON.parse(lookupKey) as (string | null)[];
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setLookingUp(true);
      lookupReceiver(
        { vatIdentifier, legalRegistrationId, legalRegistrationScheme, electronicAddress, electronicAddressScheme, countryCode },
        controller.signal,
      )
        .then((found) => {
          setReceiver(found);
          // An empty electronic address is filled from the network; one the invoice gives is never overwritten.
          if (found.status === "found" && found.participant && !electronicAddress?.trim() && !readOnly) {
            const participant = found.participant;
            setDraft((current) =>
              current && current.invoice.buyer && !current.invoice.buyer.electronicAddress?.trim()
                ? {
                    ...current,
                    invoice: {
                      ...current.invoice,
                      buyer: {
                        ...current.invoice.buyer,
                        electronicAddress: participant.value,
                        electronicAddressScheme: participant.scheme,
                      },
                    },
                  }
                : current,
            );
            setEndpointFilled(true);
          }
        })
        .catch(() => {
          if (!controller.signal.aborted) setReceiver(null);
        })
        .finally(() => {
          if (!controller.signal.aborted) setLookingUp(false);
        });
    }, LOOKUP_DELAY_MS);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [lookupKey, readOnly]);

  const invoice = draft?.invoice ?? null;
  // localIssues reads its messages in the current language, so a switch recomputes them.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const local = useMemo(() => (invoice ? localIssues(invoice) : []), [invoice, lang]);
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

  const onSave = async () => {
    if (!draft || saving) return;
    saveDraft(draft);

    if (!isApiConfigured) {
      showToast(text.toast.savedInBrowser, "success");
      return;
    }

    if (booked) {
      // Saving a booked invoice as a concept would undo the booking; the server refuses it.
      showToast(text.toast.alreadyBooked, "danger");
      return;
    }

    if (pdf.status !== "ready") {
      showToast(text.toast.savedInBrowserOnly, "danger");
      return;
    }

    setSaving(true);
    try {
      const blob = await (await fetch(pdf.url)).blob();
      await saveInvoiceDraft({
        invoiceId,
        canonicalInvoice: draft.invoice,
        pdf: blob,
        sourceFileName: draft.source.fileName,
        providerModel: draft.source.engine,
      });
      if (draft.fromImport) {
        // Saved under Facturen: it no longer needs to wait in the list.
        void dismissImport(invoiceId).catch(() => undefined);
      }
      showToast(text.toast.saved, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.common.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  const onBook = async () => {
    if (!draft || booking) return;

    if (local.length > 0) {
      showToast(text.toast.fixNumbersFirst, "danger");
      goTo(local[0].path);
      return;
    }

    if (pdf.status !== "ready") {
      showToast(text.toast.pdfMissing, "danger");
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
        extractionId: draft.extractionId,
      });
      if (draft.fromImport) {
        // Booked: it no longer waits in the list. If this fails it can be removed there by hand.
        void dismissImport(invoiceId).catch(() => undefined);
      }
      setBookedFingerprint(editableFingerprint(normalizeInvoice(stored.canonicalInvoice)));
      saveDraft(draft);
      showToast(text.toast.booked, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : text.toast.bookFailed, "danger");
    } finally {
      setBooking(false);
    }
  };

  const onDownload = async () => {
    if (!draft) return;

    if (!isRuleset) {
      const xml = toCanonicalXml(withCalculatedTotals(draft.invoice));
      download(new Blob([xml], { type: "application/xml;charset=utf-8" }), `${draft.invoice.invoiceNumber || text.fileNameFallback}.xml`);
      showToast(text.toast.canonicalDownloaded, "success");
      return;
    }

    setDownloading(true);
    try {
      const result = await exportInvoice(invoiceId, format);
      if (result.kind === "incomplete") {
        setViolations(result.violations);
        setOfficialWarnings([]);
        showToast(text.toast.notCompliant, "warning");
        return;
      }
      download(result.blob, result.fileName);
      setOfficialWarnings(result.warnings);
      showToast(
        result.warnings.length > 0
          ? text.toast.downloadedWithWarnings(result.fileName, result.warnings.length)
          : text.toast.downloaded(result.fileName),
        result.warnings.length > 0 ? "warning" : "success",
      );
    } catch (cause) {
      // No plan or credits (or a plan that ended since the page loaded): show the way to one.
      if (cause instanceof ApiError && cause.status === 402) setCanExport(false);
      showToast(cause instanceof Error ? cause.message : text.toast.exportFailed, "danger");
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
              <Icon name="fileText" /> {text.title}
            </h1>
            <p className="page-header__subtitle">{text.notFound.subtitle}</p>
          </div>
        </div>
        <div className="card">
          <div className="card__body">
            <div className="alert alert--warning">
              <Icon name="info" />
              <div>
                {text.notFound.before} <code>{invoiceId}</code> {text.notFound.after}
              </div>
            </div>
            <div className="form__actions">
              <Link className="btn btn--primary" href="/upload">
                <Icon name="fileUp" /> {text.notFound.toUpload}
              </Link>
            </div>
          </div>
        </div>
      </>
    );
  }

  const notices = server?.warnings ?? [];
  const statusBadge = !booked ? (
    <span className="badge badge--warning">{text.status.notBooked}</span>
  ) : dirty ? (
    <span className="badge badge--warning">{text.status.changed}</span>
  ) : (
    <span className="badge badge--success">{text.status.booked}</span>
  );

  return (
    <EditorContext.Provider value={editor}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileText" /> {invoice.invoiceNumber || text.untitled}
          </h1>
          <p className="page-header__subtitle">
            {draft.source.fileName} · {draft.source.engine} · {text.payable}{" "}
            {formatMoney(totals.payable, invoice.currencyCode)}
          </p>
        </div>
        {!readOnly && (
          <div className="page-header__actions">
            <button type="button" className="btn btn--secondary" onClick={() => void onSave()} disabled={saving}>
              <Icon name="save" /> {saving ? t.common.busy : t.common.save}
            </button>
            <button type="button" className="btn btn--primary" onClick={() => void onBook()} disabled={booking}>
              <Icon name="check" /> {booking ? t.common.busy : booked && !dirty ? text.bookAgain : text.book}
            </button>
          </div>
        )}
      </div>

      <div className="review-split">
        <div className="pdf-pane">
          <div className="card">
            <div className="card__header">
              <h3>{text.pdf.title}</h3>
              <span className="badge badge--neutral">{draft.source.fileName}</span>
            </div>
            <div className="card__body card__body--flush">
              {pdf.status === "ready" && <iframe className="pdf-frame" src={pdf.url} title={text.pdf.title} />}
              {pdf.status === "loading" && (
                <div className="pdf-empty">
                  <span className="text-soft text-sm">{t.common.loading}</span>
                </div>
              )}
              {pdf.status === "missing" && (
                <div className="pdf-empty">
                  <Icon name="eye" />
                  <div>
                    {text.pdf.missing}
                    <br />
                    {text.pdf.missingHint}
                  </div>
                  <Link className="btn btn--secondary btn--sm" href="/upload">
                    {text.pdf.reupload}
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
              formatChoice.source === "customer"
                ? text.formatNote.customer(customer?.name ?? text.formatNote.unknownCustomer)
                : formatChoice.source === "receiver"
                  ? text.formatNote.receiver
                  : null
            }
            canExport={canExport}
            receiverWarning={
              isRuleset && receiver?.status === "found" && !receiver.acceptedRulesetIds.includes(format)
                ? receiver.acceptedRulesetIds.length > 0
                  ? text.receiverWarning.otherFormats(
                      receiver.acceptedRulesetIds
                        .map((id) => settings.rulesets.find((r) => r.id === id)?.displayName ?? id)
                        .join(", "),
                    )
                  : text.receiverWarning.noFormats
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
              {text.tabs.invoice}
            </button>
            <button type="button" className={`tab ${tab === "xml" ? "tab--active" : ""}`.trim()} onClick={() => setTab("xml")}>
              {text.canonicalXml}
            </button>
            <button type="button" className={`tab ${tab === "notices" ? "tab--active" : ""}`.trim()} onClick={() => setTab("notices")}>
              {text.tabs.notices}{notices.length > 0 ? ` (${notices.length})` : ""}
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
              <PartyCard side="seller" title={text.party.seller} />
              <PartyCard side="buyer" title={text.party.buyer}>
                <ReceiverStatus
                  lookup={receiver}
                  looking={lookingUp}
                  rulesets={settings.rulesets}
                  current={
                    invoice.buyer?.electronicAddress
                      ? `${invoice.buyer.electronicAddressScheme ?? ""}:${invoice.buyer.electronicAddress}`
                      : null
                  }
                  filled={endpointFilled}
                  readOnly={readOnly}
                  onAdopt={() => {
                    const participant = receiver?.participant;
                    if (!participant) return;
                    update((current) =>
                      setAtPath(setAtPath(current, "buyer.electronicAddress", participant.value), "buyer.electronicAddressScheme", participant.scheme),
                    );
                  }}
                />
              </PartyCard>
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
                <h3>{text.canonicalXml}</h3>
                <span className="badge badge--neutral">{text.xml.badge}</span>
              </div>
              <div className="card__body card__body--flush">
                <pre className="xml-view">{toCanonicalXml(withCalculatedTotals(invoice))}</pre>
              </div>
            </div>
          )}

          {tab === "notices" && (
            <div className="card">
              <div className="card__header">
                <h3>{text.notices.title}</h3>
              </div>
              <div className="card__body card__body--flush">
                {notices.length === 0 ? (
                  <div className="card__body">
                    <div className="alert alert--success">
                      <Icon name="check" />
                      <div>{text.notices.none}</div>
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
                          {notice.severity === "error"
                            ? text.notices.error
                            : notice.severity === "info"
                              ? text.notices.info
                              : text.notices.warning}
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
