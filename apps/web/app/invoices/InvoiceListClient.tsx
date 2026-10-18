"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { normalizeInvoice } from "../../lib/canonical";
import { listInvoices, type StoredInvoice } from "../../lib/invoices";
import { formatMoney } from "../../lib/money";

type State =
  | { status: "loading" }
  | { status: "ready"; invoices: StoredInvoice[] }
  | { status: "error"; message: string };

export default function InvoiceListClient() {
  const [state, setState] = useState<State>({ status: "loading" });

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
            message: cause instanceof Error ? cause.message : "Laden is mislukt.",
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
            <Icon name="fileText" /> Facturen
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready"
              ? `${state.invoices.length} geboekte factuur(en)`
              : "Laden…"}
          </p>
        </div>
        <div className="page-header__actions">
          <Link className="btn btn--primary" href="/upload">
            <Icon name="fileUp" /> Nieuwe factuur
          </Link>
        </div>
      </div>

      {state.status === "error" && (
        <div className="alert alert--danger">
          <Icon name="x" />
          <div>{state.message}</div>
        </div>
      )}

      {state.status === "ready" && state.invoices.length === 0 && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">
              Er zijn nog geen facturen geboekt. Upload er een en klik op Boeken.
            </p>
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
                    <th>Nummer</th>
                    <th>Afnemer</th>
                    <th>Datum</th>
                    <th className="cell-amount">Bedrag</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {state.invoices.map((invoice) => {
                    const summary = normalizeInvoice(invoice.canonicalInvoice);
                    return (
                      <tr key={invoice.invoiceId}>
                        <td>
                          <Link href={`/invoices/${invoice.invoiceId}/review`}>
                            {summary.invoiceNumber ?? "zonder nummer"}
                          </Link>
                        </td>
                        <td>{summary.buyer?.name ?? "—"}</td>
                        <td>{summary.issueDate ?? "—"}</td>
                        <td className="cell-amount">
                          {formatMoney(summary.totals?.payableAmount, summary.currencyCode)}
                        </td>
                        <td>
                          <span className="badge badge--success">Geboekt</span>
                        </td>
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
