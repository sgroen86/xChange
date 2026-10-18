"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { createApiKey, listApiKeys, revokeApiKey, type ApiKeyItem, type CreatedApiKey } from "../../lib/apiKeys";

const when = (value: string | null) =>
  value ? new Date(value).toLocaleString("nl-NL", { dateStyle: "short", timeStyle: "short" }) : "Nog niet";

/**
 * The organisation's API keys, for admins: another system sends PDFs to the
 * conversion API with one and fetches the e-invoice. A new key is shown once;
 * xChange keeps only its fingerprint, so a lost key is revoked and replaced.
 */
export function ApiKeys() {
  const showToast = useToast();
  const [items, setItems] = useState<ApiKeyItem[] | null>(null);
  const [name, setName] = useState("");
  const [created, setCreated] = useState<CreatedApiKey | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    listApiKeys()
      .then((loaded) => {
        if (!cancelled) setItems(loaded);
      })
      .catch(() => {
        if (!cancelled) setItems([]);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const create = async (event: React.FormEvent) => {
    event.preventDefault();
    if (busy || !name.trim()) return;
    setBusy("new");
    try {
      const key = await createApiKey(name.trim());
      setCreated(key);
      setItems((current) => [...(current ?? []), key]);
      setName("");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Aanmaken is mislukt.", "danger");
    } finally {
      setBusy(null);
    }
  };

  const copy = async () => {
    if (!created) return;
    try {
      await navigator.clipboard.writeText(created.key);
      showToast("Sleutel gekopieerd.", "success");
    } catch {
      showToast("Kopiëren lukte niet; selecteer de sleutel en kopieer hem zelf.", "warning");
    }
  };

  const revoke = async (item: ApiKeyItem) => {
    if (!window.confirm(`Sleutel "${item.name}" intrekken? Systemen die hem gebruiken, krijgen daarna geen toegang meer.`)) return;
    setBusy(item.keyId);
    try {
      await revokeApiKey(item.keyId);
      setItems((current) => current?.filter((i) => i.keyId !== item.keyId) ?? null);
      if (created?.keyId === item.keyId) setCreated(null);
      showToast("Sleutel ingetrokken.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Intrekken is mislukt.", "danger");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="card form" style={{ marginTop: "var(--space-4)" }}>
      <div className="card__header">
        <h3>API-sleutels</h3>
        {items !== null && <span className="badge badge--neutral">{items.length}</span>}
      </div>
      <div className="card__body">
        <p className="text-soft text-sm">
          Met een API-sleutel stuurt een ander systeem, zoals uw ERP of boekhoudpakket, PDF&apos;s naar
          xChange en haalt het de e-factuur op. Een sleutel geeft alleen toegang tot het omzetten; niet
          tot uw facturen, relaties of instellingen.
        </p>

        {created && (
          <div className="alert alert--warning">
            <Icon name="info" />
            <div style={{ flex: 1 }}>
              <strong>Kopieer deze sleutel nu.</strong> Hij wordt maar één keer getoond: xChange bewaart
              alleen een vingerafdruk ervan. Kwijt? Trek hem in en maak een nieuwe.
              <div className="form__row" style={{ marginTop: "var(--space-3)" }}>
                <div className="form__group">
                  <label htmlFor="createdApiKey">{created.name}</label>
                  <input id="createdApiKey" type="text" value={created.key} readOnly onFocus={(e) => e.target.select()} />
                </div>
                <div className="form__group" style={{ justifyContent: "flex-end" }}>
                  <button type="button" className="btn btn--secondary" onClick={() => void copy()}>
                    Kopiëren
                  </button>
                </div>
              </div>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setCreated(null)}>
                <Icon name="check" /> Ik heb hem bewaard
              </button>
            </div>
          </div>
        )}

        <form className="form__row" onSubmit={(event) => void create(event)}>
          <div className="form__group">
            <label htmlFor="apiKeyName">Naam van een nieuwe sleutel</label>
            <input
              id="apiKeyName"
              type="text"
              placeholder="Bijvoorbeeld: Exact koppeling"
              maxLength={80}
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </div>
          <div className="form__group" style={{ justifyContent: "flex-end" }}>
            <button type="submit" className="btn btn--secondary" disabled={busy !== null || !name.trim()}>
              <Icon name="sparkles" /> Sleutel aanmaken
            </button>
          </div>
        </form>
      </div>

      {items !== null && items.length > 0 && (
        <div className="card__body card__body--flush">
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Naam</th>
                  <th>Sleutel</th>
                  <th>Aangemaakt</th>
                  <th>Laatst gebruikt</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.keyId}>
                    <td>{item.name}</td>
                    <td>
                      <code>{item.prefix}…</code>
                    </td>
                    <td>{when(item.createdAt)}</td>
                    <td>{when(item.lastUsedAt)}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn--ghost btn--sm"
                        disabled={busy === item.keyId}
                        onClick={() => void revoke(item)}
                      >
                        <Icon name="trash" /> Intrekken
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
