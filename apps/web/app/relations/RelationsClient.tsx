"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../lib/auth";
import { listRelations, updateRelation, type Relation, type RelationUpdate } from "../../lib/relations";
import { ENDPOINT_SCHEMES, REGISTRATION_SCHEMES } from "../invoices/[invoiceId]/review/fields";

type State =
  | { status: "loading" }
  | { status: "ready"; relations: Relation[] }
  | { status: "error"; message: string };

const FIELDS: { key: keyof RelationUpdate; label: string; options?: { value: string; label: string }[] }[] = [
  { key: "name", label: "Naam" },
  { key: "vatIdentifier", label: "Btw-nummer" },
  { key: "legalRegistrationId", label: "KvK-nummer" },
  { key: "legalRegistrationScheme", label: "Schema registratienummer", options: REGISTRATION_SCHEMES },
  { key: "electronicAddress", label: "Elektronisch adres (PEPPOL-id)" },
  { key: "electronicAddressScheme", label: "Schema elektronisch adres", options: ENDPOINT_SCHEMES },
  { key: "countryCode", label: "Landcode" },
];

function toUpdate(relation: Relation): RelationUpdate {
  return {
    name: relation.name,
    vatIdentifier: relation.vatIdentifier,
    legalRegistrationId: relation.legalRegistrationId,
    legalRegistrationScheme: relation.legalRegistrationScheme,
    electronicAddress: relation.electronicAddress,
    electronicAddressScheme: relation.electronicAddressScheme,
    countryCode: relation.countryCode,
  };
}

/**
 * The organisation's customers. They are created when an invoice is booked
 * and fill the next invoice to the same customer; this page is for checking
 * and correcting them.
 */
export default function RelationsClient() {
  const showToast = useToast();
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin" || user?.role === "User";

  const [state, setState] = useState<State>({ status: "loading" });
  const [editing, setEditing] = useState<{ id: string; values: RelationUpdate } | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listRelations()
      .then((relations) => {
        if (!cancelled) setState({ status: "ready", relations });
      })
      .catch((cause: unknown) => {
        if (!cancelled) setState({ status: "error", message: cause instanceof Error ? cause.message : "Laden is mislukt." });
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!editing || saving) return;

    setSaving(true);
    try {
      const saved = await updateRelation(editing.id, editing.values);
      setState((current) =>
        current.status === "ready"
          ? { status: "ready", relations: current.relations.map((r) => (r.relationId === saved.relationId ? saved : r)) }
          : current,
      );
      setEditing(null);
      showToast("Relatie opgeslagen.", "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : "Opslaan mislukt.", "danger");
    } finally {
      setSaving(false);
    }
  };

  const setValue = (key: keyof RelationUpdate, value: string) =>
    setEditing((current) => (current ? { ...current, values: { ...current.values, [key]: value || null } } : current));

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="users" /> Relaties
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready" ? `${state.relations.length} relatie(s)` : "Laden…"}
          </p>
        </div>
      </div>

      {state.status === "error" && (
        <div className="alert alert--danger">
          <Icon name="x" />
          <div>{state.message}</div>
        </div>
      )}

      {editing && (
        <form className="form card" onSubmit={save} style={{ marginBottom: "var(--space-4)" }}>
          <div className="card__header">
            <h3>Relatie wijzigen</h3>
          </div>
          <div className="card__body">
            <div className="form__row">
              {FIELDS.slice(0, 3).map((field) => (
                <Field key={field.key} field={field} value={editing.values[field.key]} onChange={setValue} />
              ))}
            </div>
            <div className="form__row">
              {FIELDS.slice(3).map((field) => (
                <Field key={field.key} field={field} value={editing.values[field.key]} onChange={setValue} />
              ))}
            </div>
            <div className="form__actions">
              <button type="submit" className="btn btn--primary" disabled={saving}>
                <Icon name="save" /> {saving ? "Bezig…" : "Opslaan"}
              </button>
              <button type="button" className="btn btn--secondary" onClick={() => setEditing(null)}>
                Annuleren
              </button>
            </div>
          </div>
        </form>
      )}

      {state.status === "ready" && state.relations.length === 0 && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">
              Relaties worden vanzelf aangemaakt wanneer u een factuur boekt waarop de afnemer een
              btw- of KvK-nummer heeft.
            </p>
          </div>
        </div>
      )}

      {state.status === "ready" && state.relations.length > 0 && (
        <div className="card">
          <div className="card__body card__body--flush">
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Naam</th>
                    <th>Btw-nummer</th>
                    <th>KvK-nummer</th>
                    <th>Elektronisch adres</th>
                    <th>Land</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {state.relations.map((relation) => (
                    <tr key={relation.relationId}>
                      <td>{relation.name ?? "—"}</td>
                      <td>{relation.vatIdentifier ?? "—"}</td>
                      <td>{relation.legalRegistrationId ?? "—"}</td>
                      <td>
                        {relation.electronicAddress
                          ? `${relation.electronicAddressScheme ?? "?"}:${relation.electronicAddress}`
                          : "—"}
                      </td>
                      <td>{relation.countryCode ?? "—"}</td>
                      <td>
                        {canEdit && (
                          <button
                            type="button"
                            className="btn btn--ghost btn--sm"
                            onClick={() => setEditing({ id: relation.relationId, values: toUpdate(relation) })}
                          >
                            <Icon name="edit" /> Wijzigen
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

function Field({
  field,
  value,
  onChange,
}: {
  field: (typeof FIELDS)[number];
  value: string | null;
  onChange: (key: keyof RelationUpdate, value: string) => void;
}) {
  const id = `relation-${field.key}`;
  return (
    <div className="form__group">
      <label htmlFor={id}>{field.label}</label>
      {field.options ? (
        <select id={id} value={value ?? ""} onChange={(event) => onChange(field.key, event.target.value)}>
          <option value="">—</option>
          {field.options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      ) : (
        <input id={id} type="text" value={value ?? ""} onChange={(event) => onChange(field.key, event.target.value)} />
      )}
    </div>
  );
}
