"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../lib/auth";
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

const STATUS: Record<ImportItem["status"], { label: string; badge: string }> = {
  awaitingApproval: { label: "Wacht op goedkeuring", badge: "badge--warning" },
  queued: { label: "In de wachtrij", badge: "badge--neutral" },
  processing: { label: "Wordt gelezen", badge: "badge--info" },
  ready: { label: "Klaar om te beoordelen", badge: "badge--success" },
  failed: { label: "Mislukt", badge: "badge--danger" },
};

/**
 * PDFs uploaded in a batch (and, later, received by email), read in the
 * background. A ready one opens in the normal review screen; booking it takes
 * it off this list.
 */
export default function ImportsClient() {
  const router = useRouter();
  const showToast = useToast();
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin" || user?.role === "User";

  const [items, setItems] = useState<ImportItem[] | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    try {
      setItems(await listImports());
    } catch (cause) {
      setItems((current) => current ?? []);
      showToast(cause instanceof Error ? cause.message : "Laden is mislukt.", "danger");
    }
  }, [showToast]);

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
      showToast(cause instanceof Error ? cause.message : "Dat is mislukt.", "danger");
    } finally {
      setBusy(null);
    }
  };

  const waiting = items?.filter((item) => item.status === "awaitingApproval") ?? [];

  const approve = (ids: string[]) =>
    act(ids.length === 1 ? ids[0] : "all", async () => {
      for (const id of ids) await approveImport(id);
      await refresh();
    }, ids.length === 1 ? "Wordt nu gelezen." : `${ids.length} facturen worden nu gelezen.`);

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
            <Icon name="clock" /> Te beoordelen
          </h1>
          <p className="page-header__subtitle">
            {items === null ? "Laden…" : `${items.length} factu${items.length === 1 ? "ur" : "ren"} in de lijst`}
          </p>
        </div>
        <div className="page-header__actions">
          {canEdit && waiting.length > 1 && (
            <button type="button" className="btn btn--secondary" disabled={busy !== null} onClick={() => void approve(waiting.map((i) => i.importId))}>
              <Icon name="check" /> Alles verwerken ({waiting.length})
            </button>
          )}
          <Link className="btn btn--primary" href="/upload">
            <Icon name="fileUp" /> Facturen uploaden
          </Link>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>Er is geen API geconfigureerd: importeren in de achtergrond is niet beschikbaar.</div>
        </div>
      )}

      {waiting.length > 0 && (
        <div className="alert alert--warning">
          <Icon name="info" />
          <div>
            Per e-mail ontvangen PDF&apos;s worden pas gelezen als u ze goedkeurt. Bekijk ze eerst;
            verwijder wat geen factuur van u is.
          </div>
        </div>
      )}

      {items !== null && items.length === 0 && isApiConfigured && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">
              Niets te beoordelen. Upload meerdere PDF&apos;s tegelijk; ze worden in de achtergrond
              gelezen en verschijnen hier.
            </p>
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
                    <th>Bestand</th>
                    <th>Factuur</th>
                    <th>Status</th>
                    <th>Ontvangen</th>
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
                          {item.source === "email" && item.sender ? ` · van ${item.sender}` : ""}
                          {item.source === "api" ? " · via API" : ""}
                        </div>
                        {item.subject && <div className="text-xs text-muted">{item.subject}</div>}
                      </td>
                      <td>
                        {item.invoiceNumber ?? "—"}
                        {item.buyerName && <div className="text-xs text-muted">{item.buyerName}</div>}
                      </td>
                      <td>
                        <span className={`badge ${STATUS[item.status].badge}`}>{STATUS[item.status].label}</span>
                        {item.error && <div className="text-xs text-muted">{item.error}</div>}
                      </td>
                      <td>{new Date(item.createdAt).toLocaleString("nl-NL", { dateStyle: "short", timeStyle: "short" })}</td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        {item.status === "awaitingApproval" && (
                          <>
                            <button
                              type="button"
                              className="btn btn--ghost btn--sm"
                              disabled={busy === item.importId}
                              onClick={() => void act(item.importId, () => viewImportDocument(item.importId))}
                            >
                              <Icon name="eye" /> Bekijken
                            </button>
                            {canEdit && (
                              <button
                                type="button"
                                className="btn btn--primary btn--sm"
                                disabled={busy !== null}
                                onClick={() => void approve([item.importId])}
                              >
                                <Icon name="check" /> Verwerken
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
                            <Icon name="eye" /> Beoordelen
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
                              }, "Opnieuw in de wachtrij gezet.")
                            }
                          >
                            Opnieuw
                          </button>
                        )}
                        {canEdit && (
                          <button
                            type="button"
                            className="btn btn--ghost btn--icon"
                            aria-label="Uit de lijst verwijderen"
                            disabled={busy === item.importId}
                            onClick={() =>
                              void act(item.importId, async () => {
                                await dismissImport(item.importId);
                                setItems((current) => current?.filter((i) => i.importId !== item.importId) ?? null);
                              }, "Uit de lijst verwijderd.")
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
