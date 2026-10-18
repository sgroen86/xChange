"use client";

import Link from "next/link";
import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../lib/auth";
import { normalizeInvoice } from "../../lib/canonical";
import { useText } from "../../lib/i18n";
import { deleteInvoice, listInvoices, type StoredInvoice } from "../../lib/invoices";
import { formatMoney } from "../../lib/money";

type State =
  | { status: "loading" }
  | { status: "ready"; invoices: StoredInvoice[] }
  /** message null: no message from the error itself, show the generic one in the current language. */
  | { status: "error"; message: string | null };

export default function InvoiceListClient() {
  const [state, setState] = useState<State>({ status: "loading" });
  const [deleting, setDeleting] = useState<string | null>(null);
  const showToast = useToast();
  const t = useText();
  const user =useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin" || user?.role === "User";

  const onDelete = async (invoice: StoredInvoice, number: string) => {
    const what = invoice.status === "Booked" ? t.invoices.bookedInvoice : t.invoices.draftInvoice;
    if (!window.confirm(t.invoices.deleteConfirm(what, number))) return;

    setDeleting(invoice.invoiceId);
    try {
      await deleteInvoice(invoice.invoiceId);
      setState((current) =>
        current.status === "ready"
          ? { status: "ready", invoices: current.invoices.filter((i) => i.invoiceId !== invoice.invoiceId) }
          : current,
      );
      showToast(t.invoices.deleted, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.invoices.deleteFailed, "danger");
    } finally {
      setDeleting(null);
    }
  };

  useEffect(() => {
    let cancelled = false;

    listInvoices()
      .then((invoices) => {
        if (!cancelled) setState({ status: "ready", invoices });
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setState({
            status: "error",
            message: cause instanceof Error ? cause.message : null,
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileText" /> {t.invoices.title}
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready" ? t.invoices.count(state.invoices.length) : t.common.loading}
          </p>
        </div>
        <div className="page-header__actions">
          <Link className="btn btn--primary" href="/upload">
            <Icon name="fileUp" /> {t.invoices.newInvoice}
          </Link>
        </div>
      </div>

      {state.status === "error" && (
        <div className="alert alert--danger">
          <Icon name="x" />
          <div>{state.message ?? t.common.loadFailed}</div>
        </div>
      )}

      {state.status === "ready" && state.invoices.length === 0 && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">{t.invoices.empty}</p>
          </div>
        </div>
      )}

      {state.status === "ready" && state.invoices.length > 0 && (
        <div className="card">
          <div className="card__body card__body--flush">
            <div className="table-wrap">
              <table className="table table--clickable">
                <thead>
                  <tr>
                    <th>{t.invoices.columns.number}</th>
                    <th>{t.invoices.columns.buyer}</th>
                    <th>{t.invoices.columns.date}</th>
                    <th className="cell-amount">{t.invoices.columns.amount}</th>
                    <th>{t.invoices.columns.status}</th>
                    {canEdit && <th aria-label={t.invoices.columns.actions} />}
                  </tr>
                </thead>
                <tbody>
                  {state.invoices.map((invoice) => {
                    const summary = normalizeInvoice(invoice.canonicalInvoice);
                    return (
                      <tr key={invoice.invoiceId}>
                        <td>
                          <Link href={`/invoices/${invoice.invoiceId}/review`}>
                            {summary.invoiceNumber ?? t.invoices.noNumber}
                          </Link>
                        </td>
                        <td>{summary.buyer?.name ?? "—"}</td>
                        <td>{summary.issueDate ?? "—"}</td>
                        <td className="cell-amount">
                          {formatMoney(summary.totals?.payableAmount, summary.currencyCode)}
                        </td>
                        <td>
                          {invoice.status === "Booked" ? (
                            <span className="badge badge--success">{t.invoices.booked}</span>
                          ) : (
                            <span className="badge badge--warning">{t.invoices.draft}</span>
                          )}
                        </td>
                        {canEdit && (
                          <td className="cell-actions">
                            <button
                              type="button"
                              className="btn btn--ghost btn--icon"
                              aria-label={t.invoices.deleteInvoice}
                              disabled={deleting === invoice.invoiceId}
                              onClick={() => void onDelete(invoice, summary.invoiceNumber ?? t.invoices.noNumber)}
                            >
                              <Icon name="trash" />
                            </button>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
