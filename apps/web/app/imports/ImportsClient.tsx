"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../lib/auth";
import { useLang, useText } from "../../lib/i18n";
import {
  approveImport,
  dismissImport,
  listImports,
  openImport,
  retryImport,
  viewImportDocument,
  type ImportItem,
} from "../../lib/imports";
import { formatBytes } from "../../lib/pdf";

/** How often the list refreshes while something is still being read. */
const POLL_MS = 4000;

/** Badge per status; the label is t.imports.status[status]. */
const STATUS_BADGE: Record<ImportItem["status"], string> = {
  awaitingApproval: "badge--warning",
  queued: "badge--neutral",
  processing: "badge--info",
  ready: "badge--success",
  failed: "badge--danger",
};

/**
 * PDFs uploaded in a batch (and, later, received by email), read in the
 * background. A ready one opens in the normal review screen; booking it takes
 * it off this list.
 */
export default function ImportsClient() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();
  const locale = useLang() === "en" ? "en-GB" : "nl-NL";
  const user =useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin" || user?.role === "User";

  const [items, setItems] = useState<ImportItem[] | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setItems(await listImports());
    } catch (cause) {
      setItems((current) => current ?? []);
      showToast(cause instanceof Error ? cause.message : t.common.loadFailed, "danger");
    }
  }, [showToast, t]);

  useEffect(() => {
    const first = window.setTimeout(() => void refresh(), 0);
    return () => window.clearTimeout(first);
  }, [refresh]);

  // Keep refreshing while anything is still on its way.
  const pending = items?.some((item) => item.status === "queued" || item.status === "processing") ?? false;
  useEffect(() => {
    if (!pending) return;
    const timer = window.setInterval(() => void refresh(), POLL_MS);
    return () => window.clearInterval(timer);
  }, [pending, refresh]);

  const act = async (importId: string, action: () => Promise<void>, done?: string) => {
    setBusy(importId);
    try {
      await action();
      if (done) showToast(done, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.common.somethingFailed, "danger");
    } finally {
      setBusy(null);
    }
  };

  const waiting = items?.filter((item) => item.status === "awaitingApproval") ?? [];

  const approve = (ids: string[]) =>
    act(ids.length === 1 ? ids[0] : "all", async () => {
      for (const id of ids) await approveImport(id);
      await refresh();
    }, ids.length === 1 ? t.imports.approvedOne : t.imports.approvedMany(ids.length));

  const open = (item: ImportItem) =>
    act(item.importId, async () => {
      await openImport(item.importId);
      router.push(`/invoices/${item.importId}/review`);
    });

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="clock" /> {t.imports.title}
          </h1>
          <p className="page-header__subtitle">
            {items === null ? t.common.loading : t.imports.count(items.length)}
          </p>
        </div>
        <div className="page-header__actions">
          {canEdit && waiting.length > 1 && (
            <button type="button" className="btn btn--secondary" disabled={busy !== null} onClick={() => void approve(waiting.map((i) => i.importId))}>
              <Icon name="check" /> {t.imports.approveAll(waiting.length)}
            </button>
          )}
          <Link className="btn btn--primary" href="/upload">
            <Icon name="fileUp" /> {t.imports.uploadInvoices}
          </Link>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>{t.imports.noApi}</div>
        </div>
      )}

      {waiting.length > 0 && (
        <div className="alert alert--warning">
          <Icon name="info" />
          <div>{t.imports.emailNotice}</div>
        </div>
      )}

      {items !== null && items.length === 0 && isApiConfigured && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">{t.imports.empty}</p>
          </div>
        </div>
      )}

      {items !== null && items.length > 0 && (
        <div className="card">
          <div className="card__body card__body--flush">
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t.imports.columns.file}</th>
                    <th>{t.imports.columns.invoice}</th>
                    <th>{t.imports.columns.status}</th>
                    <th>{t.imports.columns.received}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.importId}>
                      <td>
                        {item.fileName}
                        <div className="text-xs text-muted">
                          {formatBytes(item.byteSize)}
                          {item.source === "email" && item.sender ? t.imports.from(item.sender) : ""}
                        </div>
                        {item.subject && <div className="text-xs text-muted">{item.subject}</div>}
                      </td>
                      <td>
                        {item.invoiceNumber ?? "—"}
                        {item.buyerName && <div className="text-xs text-muted">{item.buyerName}</div>}
                      </td>
                      <td>
                        <span className={`badge ${STATUS_BADGE[item.status]}`}>{t.imports.status[item.status]}</span>
                        {item.error && <div className="text-xs text-muted">{item.error}</div>}
                      </td>
                      <td>{new Date(item.createdAt).toLocaleString(locale, { dateStyle: "short", timeStyle: "short" })}</td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        {item.status === "awaitingApproval" && (
                          <>
                            <button
                              type="button"
                              className="btn btn--ghost btn--sm"
                              disabled={busy === item.importId}
                              onClick={() => void act(item.importId, () => viewImportDocument(item.importId))}
                            >
                              <Icon name="eye" /> {t.imports.view}
                            </button>
                            {canEdit && (
                              <button
                                type="button"
                                className="btn btn--primary btn--sm"
                                disabled={busy !== null}
                                onClick={() => void approve([item.importId])}
                              >
                                <Icon name="check" /> {t.imports.approve}
                              </button>
                            )}
                          </>
                        )}
                        {item.status === "ready" && (
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            disabled={busy === item.importId}
                            onClick={() => void open(item)}
                          >
                            <Icon name="eye" /> {t.imports.review}
                          </button>
                        )}
                        {item.status === "failed" && canEdit && (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            disabled={busy === item.importId}
                            onClick={() =>
                              void act(item.importId, async () => {
                                await retryImport(item.importId);
                                await refresh();
                              }, t.imports.requeued)
                            }
                          >
                            {t.common.retry}
                          </button>
                        )}
                        {canEdit && (
                          <button
                            type="button"
                            className="btn btn--ghost btn--icon"
                            aria-label={t.imports.dismiss}
                            disabled={busy === item.importId}
                            onClick={() =>
                              void act(item.importId, async () => {
                                await dismissImport(item.importId);
                                setItems((current) => current?.filter((i) => i.importId !== item.importId) ?? null);
                              }, t.imports.dismissed)
                            }
                          >
                            <Icon name="trash" />
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
