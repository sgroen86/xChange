"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { ModelSetting } from "./ModelSetting";
import { useToast } from "../../components/Toast";
import { text, useText } from "../../lib/i18n";
import { getServerUserSnapshot, getUserSnapshot, listCountries, subscribeToUser, type CountryOption } from "../../lib/auth";
import {
  createOrganization,
  invitationLink,
  listOrganizations,
  reissueInvitation,
  updateOrganization,
  type PlatformOrganization,
} from "../../lib/platform";

type Panel =
  | { kind: "none" }
  | { kind: "create" }
  | { kind: "edit"; organization: PlatformOrganization }
  | { kind: "invite"; organization: PlatformOrganization };

/**
 * Platform administration: the organisations on xChange and the invitation
 * for each one's first user. Only the platform administrator sees this page,
 * and it shows nothing of an organisation's invoices or relations.
 */
export default function PlatformClient() {
  const showToast = useToast();
  const t = useText();
  const pl = t.platform;
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);

  const [organizations, setOrganizations] = useState<PlatformOrganization[] | null>(null);
  const [countries, setCountries] = useState<CountryOption[]>([]);
  const [panel, setPanel] = useState<Panel>({ kind: "none" });
  const [link, setLink] = useState<{ organization: string; url: string } | null>(null);
  const [saving, setSaving] = useState(false);

  // Form fields, shared by the panels.
  const [name, setName] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [userName, setUserName] = useState("");
  const [userEmail, setUserEmail] = useState("");

  const reload = () =>
    listOrganizations()
      .then(setOrganizations)
      .catch((cause: unknown) => {
        setOrganizations([]);
        showToast(cause instanceof Error ? cause.message : t.common.loadFailed, "danger");
      });

  useEffect(() => {
    let cancelled = false;
    Promise.all([listOrganizations(), listCountries()])
      .then(([loaded, loadedCountries]) => {
        if (cancelled) return;
        setOrganizations(loaded);
        setCountries(loadedCountries);
      })
      .catch((cause: unknown) => {
        if (cancelled) return;
        setOrganizations([]);
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [showToast]);

  const open = (next: Panel) => {
    setLink(null);
    setPanel(next);
    if (next.kind === "edit") {
      setName(next.organization.name);
      setCountryCode(next.organization.countryCode);
    } else if (next.kind === "invite") {
      setUserName("");
      setUserEmail(next.organization.pendingInvitationEmail ?? "");
    } else if (next.kind === "create") {
      setName("");
      setCountryCode("");
      setUserName("");
      setUserEmail("");
    }
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving || panel.kind === "none") return;

    setSaving(true);
    try {
      if (panel.kind === "create") {
        const result = await createOrganization({ name, countryCode, firstUser: { name: userName, email: userEmail } });
        setLink({ organization: result.organization.name, url: invitationLink(result.invitationCode!) });
      } else if (panel.kind === "invite") {
        const result = await reissueInvitation(panel.organization.organizationId, { name: userName, email: userEmail });
        setLink({ organization: result.organization.name, url: invitationLink(result.invitationCode!) });
      } else {
        await updateOrganization(panel.organization.organizationId, { name, countryCode });
        showToast(pl.saved, "success");
      }
      setPanel({ kind: "none" });
      await reload();
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : pl.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  const copy = async () => {
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link.url);
      showToast(pl.linkCopied, "success");
    } catch {
      showToast(pl.copyFailed, "warning");
    }
  };

  const countryName = (code: string) => countries.find((c) => c.code === code)?.name ?? code;

  if (user && !user.isPlatformAdmin) {
    return (
      <div className="alert alert--warning">
        <Icon name="info" />
        <div>{pl.adminOnly}</div>
      </div>
    );
  }

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="users" /> {pl.title}
          </h1>
          <p className="page-header__subtitle">
            {organizations === null ? t.common.loading : pl.count(organizations.length)}
          </p>
        </div>
        <div className="page-header__actions">
          <button type="button" className="btn btn--primary" onClick={() => open({ kind: "create" })}>
            <Icon name="filePlus" /> {pl.create}
          </button>
        </div>
      </div>

      {link && (
        <div className="alert alert--success" style={{ marginBottom: "var(--space-4)" }}>
          <Icon name="check" />
          <div style={{ minWidth: 0, flex: 1 }}>
            <div className="alert__title">{pl.linkTitle(link.organization)}</div>
            <p className="text-sm">{pl.linkHelp}</p>
            <div className="form" style={{ display: "flex", gap: "var(--space-3)" }}>
              <input type="text" readOnly value={link.url} aria-label={pl.linkLabel} onFocus={(e) => e.target.select()} />
              <button type="button" className="btn btn--secondary" onClick={() => void copy()}>
                <Icon name="save" /> {pl.copy}
              </button>
            </div>
          </div>
        </div>
      )}

      {panel.kind !== "none" && (
        <form className="form card" onSubmit={save} style={{ marginBottom: "var(--space-4)" }}>
          <div className="card__header">
            <h3>
              {panel.kind === "create"
                ? pl.create
                : panel.kind === "edit"
                  ? pl.editTitle(panel.organization.name)
                  : pl.inviteTitle(panel.organization.name)}
            </h3>
          </div>
          <div className="card__body">
            {(panel.kind === "create" || panel.kind === "edit") && (
              <div className="form__row">
                <div className="form__group">
                  <label htmlFor="org-name">{pl.organizationName}</label>
                  <input id="org-name" type="text" value={name} onChange={(e) => setName(e.target.value)} required />
                </div>
                <div className="form__group">
                  <label htmlFor="org-country">{pl.country}</label>
                  <select id="org-country" value={countryCode} onChange={(e) => setCountryCode(e.target.value)} required>
                    <option value="">{pl.chooseCountry}</option>
                    {countries.map((country) => (
                      <option key={country.code} value={country.code}>
                        {country.name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            )}

            {(panel.kind === "create" || panel.kind === "invite") && (
              <div className="form__row">
                <div className="form__group">
                  <label htmlFor="user-name">{pl.firstUserName}</label>
                  <input id="user-name" type="text" value={userName} onChange={(e) => setUserName(e.target.value)} required />
                </div>
                <div className="form__group">
                  <label htmlFor="user-email">{pl.email}</label>
                  <input id="user-email" type="email" value={userEmail} onChange={(e) => setUserEmail(e.target.value)} required />
                </div>
              </div>
            )}

            {panel.kind === "invite" && (
              <p className="text-soft text-sm">{pl.inviteHelp}</p>
            )}

            <div className="form__actions">
              <button type="submit" className="btn btn--primary" disabled={saving}>
                <Icon name="save" /> {saving ? t.common.busy : panel.kind === "edit" ? t.common.save : pl.createAndShowLink}
              </button>
              <button type="button" className="btn btn--secondary" onClick={() => setPanel({ kind: "none" })}>
                {t.common.cancel}
              </button>
            </div>
          </div>
        </form>
      )}

      {organizations !== null && organizations.length > 0 && (
        <div className="card">
          <div className="card__body card__body--flush">
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>{pl.columns.organization}</th>
                    <th>{pl.columns.country}</th>
                    <th>{pl.columns.created}</th>
                    <th>{pl.columns.users}</th>
                    <th>{pl.columns.invitation}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {organizations.map((organization) => (
                    <tr key={organization.organizationId}>
                      <td>{organization.name}</td>
                      <td>{countryName(organization.countryCode)}</td>
                      <td>{organization.createdAt.slice(0, 10)}</td>
                      <td>{organization.userCount ?? "—"}</td>
                      <td>
                        {organization.invitationPending ? (
                          <span className="badge badge--warning">{pl.pending(organization.pendingInvitationEmail)}</span>
                        ) : (
                          "—"
                        )}
                      </td>
                      <td style={{ whiteSpace: "nowrap" }}>
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => open({ kind: "edit", organization })}>
                          <Icon name="edit" /> {pl.edit}
                        </button>
                        <button type="button" className="btn btn--ghost btn--sm" onClick={() => open({ kind: "invite", organization })}>
                          <Icon name="user" /> {pl.newLink}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      <ModelSetting />
    </>
  );
}
