"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../lib/auth";
import { text, useText, type Dictionary } from "../../lib/i18n";
import { listRelations, updateRelation, type Relation, type RelationUpdate } from "../../lib/relations";
import { FALLBACK_SETTINGS, getSettings, type RulesetInfo } from "../../lib/settings";
import { endpointSchemes, registrationSchemes } from "../invoices/[invoiceId]/review/fields";

type State =
  | { status: "loading" }
  | { status: "ready"; relations: Relation[] }
  | { status: "error"; message: string };

type FieldSpec = { key: keyof RelationUpdate; label: string; options?: { value: string; label: string }[]; emptyLabel?: string };

function relationFields(t: Dictionary): FieldSpec[] {
  const labels = t.relations.fields;
  return [
    { key: "name", label: labels.name },
    { key: "vatIdentifier", label: labels.vatIdentifier },
    { key: "legalRegistrationId", label: labels.legalRegistrationId },
    { key: "legalRegistrationScheme", label: labels.legalRegistrationScheme, options: registrationSchemes(t) },
    { key: "electronicAddress", label: labels.electronicAddress },
    { key: "electronicAddressScheme", label: labels.electronicAddressScheme, options: endpointSchemes(t) },
    { key: "countryCode", label: labels.countryCode },
  ];
}

function toUpdate(relation: Relation): RelationUpdate {
  return {
    name: relation.name,
    vatIdentifier: relation.vatIdentifier,
    legalRegistrationId: relation.legalRegistrationId,
    legalRegistrationScheme: relation.legalRegistrationScheme,
    electronicAddress: relation.electronicAddress,
    electronicAddressScheme: relation.electronicAddressScheme,
    countryCode: relation.countryCode,
    defaultRulesetId: relation.defaultRulesetId,
  };
}

/**
 * The organisation's customers. They are created when an invoice is booked
 * and fill the next invoice to the same customer; this page is for checking
 * and correcting them.
 */
export default function RelationsClient() {
  const showToast = useToast();
  const t = useText();
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin" || user?.role === "User";

  const [state, setState] = useState<State>({ status: "loading" });
  const [editing, setEditing] = useState<{ id: string; values: RelationUpdate } | null>(null);
  const [saving, setSaving] = useState(false);
  const [rulesets, setRulesets] = useState<RulesetInfo[]>(FALLBACK_SETTINGS.rulesets);

  useEffect(() => {
    let cancelled = false;
    getSettings()
      .then((settings) => {
        if (!cancelled) setRulesets(settings.rulesets);
      })
      .catch(() => {
        // The fallback lists BIS 3.0; saving still checks the choice on the server.
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const formatField: FieldSpec = {
    key: "defaultRulesetId",
    label: t.relations.fields.defaultRulesetId,
    options: rulesets.map((r) => ({ value: r.id, label: r.displayName })),
    emptyLabel: t.relations.organizationDefault,
  };
  const fields = relationFields(t);
  const formatName = (id: string | null) => (id ? (rulesets.find((r) => r.id === id)?.displayName ?? id) : "—");

  useEffect(() => {
    let cancelled = false;
    listRelations()
      .then((relations) => {
        if (!cancelled) setState({ status: "ready", relations });
      })
      .catch((cause: unknown) => {
        if (!cancelled) setState({ status: "error", message: cause instanceof Error ? cause.message : text().common.loadFailed });
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
      showToast(t.relations.saved, "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : t.relations.saveFailed, "danger");
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
            <Icon name="users" /> {t.relations.title}
          </h1>
          <p className="page-header__subtitle">
            {state.status === "ready" ? t.relations.count(state.relations.length) : t.common.loading}
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
            <h3>{t.relations.editTitle}</h3>
          </div>
          <div className="card__body">
            <div className="form__row">
              {fields.slice(0, 3).map((field) => (
                <Field key={field.key} field={field} value={editing.values[field.key]} onChange={setValue} />
              ))}
            </div>
            <div className="form__row">
              {fields.slice(3).map((field) => (
                <Field key={field.key} field={field} value={editing.values[field.key]} onChange={setValue} />
              ))}
            </div>
            <div className="form__row">
              <Field field={formatField} value={editing.values.defaultRulesetId} onChange={setValue} />
            </div>
            <div className="form-help">{t.relations.formatHelp}</div>
            <div className="form__actions">
              <button type="submit" className="btn btn--primary" disabled={saving}>
                <Icon name="save" /> {saving ? t.common.busy : t.common.save}
              </button>
              <button type="button" className="btn btn--secondary" onClick={() => setEditing(null)}>
                {t.common.cancel}
              </button>
            </div>
          </div>
        </form>
      )}

      {state.status === "ready" && state.relations.length === 0 && (
        <div className="card">
          <div className="card__body">
            <p className="text-soft text-sm">{t.relations.empty}</p>
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
                    <th>{t.relations.columns.name}</th>
                    <th>{t.relations.columns.vatIdentifier}</th>
                    <th>{t.relations.columns.legalRegistrationId}</th>
                    <th>{t.relations.columns.electronicAddress}</th>
                    <th>{t.relations.columns.country}</th>
                    <th>{t.relations.columns.format}</th>
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
                      <td>{formatName(relation.defaultRulesetId)}</td>
                      <td>
                        {canEdit && (
                          <button
                            type="button"
                            className="btn btn--ghost btn--sm"
                            onClick={() => setEditing({ id: relation.relationId, values: toUpdate(relation) })}
                          >
                            <Icon name="edit" /> {t.relations.edit}
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
  field: FieldSpec;
  value: string | null;
  onChange: (key: keyof RelationUpdate, value: string) => void;
}) {
  const id = `relation-${field.key}`;
  return (
    <div className="form__group">
      <label htmlFor={id}>{field.label}</label>
      {field.options ? (
        <select id={id} value={value ?? ""} onChange={(event) => onChange(field.key, event.target.value)}>
          <option value="">{field.emptyLabel ?? "—"}</option>
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
