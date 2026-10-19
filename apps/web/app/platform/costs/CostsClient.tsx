"use client";

import { useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../../components/Icon";
import { useToast } from "../../../components/Toast";
import { getServerUserSnapshot, getUserSnapshot, subscribeToUser } from "../../../lib/auth";
import {
  downloadCostsCsv,
  getOrganizationCosts,
  getPlatformCosts,
  type CostActor,
  type CostEvent,
  type CostRow,
  type CostTotals,
  type OrganizationCosts,
  type PlatformCosts,
} from "../../../lib/costs";
import { compare, divRound, format, HUNDRED, isZero, mul, parseDecimal, ZERO } from "../../../lib/decimal";
import { text, useLang, useText, type Lang } from "../../../lib/i18n";
import { formatUsd } from "../../../lib/money";
import { currentMonth, shiftMonth } from "../../../lib/months";
import QualityTab from "./QualityTab";

/** A share of the largest value, 0-100, for a CSS bar. Digit arithmetic, like the amounts themselves. */
function share(value: string, max: string): number {
  const amount = parseDecimal(value);
  const largest = parseDecimal(max);
  if (!amount || !largest || isZero(largest)) return 0;
  return Number(format(divRound(mul(amount, HUNDRED), largest, 0)));
}

function largest(rows: CostRow[]): string {
  return format(
    rows.reduce((max, row) => {
      const value = parseDecimal(row.totals.costUsd) ?? ZERO;
      return compare(value, max) > 0 ? value : max;
    }, ZERO),
  );
}

function count(value: number, lang: Lang): string {
  return value.toLocaleString(lang === "en" ? "en-GB" : "nl-NL");
}

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

/**
 * Model cost per month, for the platform administrator only: totals, per day,
 * per model and per organisation, and one organisation's month per user with
 * its most expensive calls. Customers never see this; they see conversions.
 */
export default function CostsClient() {
  const showToast = useToast();
  const t = useText();
  const lang = useLang();
  const c = t.costs;
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const isAdmin = Boolean(user?.isPlatformAdmin);

  const [month, setMonth] = useState(() => currentMonth());
  const [tab, setTab] = useState<"costs" | "quality">("costs");
  const [organizationId, setOrganizationId] = useState<string | null>(null);
  const [downloading, setDownloading] = useState(false);

  // Kept with the month (and organisation) they belong to, so a change shows "loading" without resetting state in an effect.
  const [platformLoaded, setPlatformLoaded] = useState<{ month: string; data: PlatformCosts | null } | null>(null);
  const [organizationLoaded, setOrganizationLoaded] = useState<{ key: string; data: OrganizationCosts | null } | null>(null);

  useEffect(() => {
    if (!isAdmin) return;
    let cancelled = false;
    getPlatformCosts(month)
      .then((data) => !cancelled && setPlatformLoaded({ month, data }))
      .catch((cause: unknown) => {
        if (cancelled) return;
        setPlatformLoaded({ month, data: null });
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [isAdmin, month, showToast]);

  useEffect(() => {
    if (!isAdmin || !organizationId) return;
    let cancelled = false;
    const key = `${organizationId}|${month}`;
    getOrganizationCosts(organizationId, month)
      .then((data) => !cancelled && setOrganizationLoaded({ key, data }))
      .catch((cause: unknown) => {
        if (cancelled) return;
        setOrganizationLoaded({ key, data: null });
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [isAdmin, organizationId, month, showToast]);

  const platform = platformLoaded?.month === month ? platformLoaded.data : undefined;
  const organization =
    organizationId && organizationLoaded?.key === `${organizationId}|${month}` ? organizationLoaded.data : undefined;

  const thisMonth = currentMonth();
  const usd = (value: string) => formatUsd(value, lang);

  const csv = async () => {
    setDownloading(true);
    try {
      const file = await downloadCostsCsv(month);
      download(file.blob, file.fileName);
      showToast(c.downloaded(file.fileName), "success");
    } catch (cause) {
      showToast(cause instanceof Error ? cause.message : c.downloadFailed, "danger");
    } finally {
      setDownloading(false);
    }
  };

  if (user && !isAdmin) {
    return (
      <div className="alert alert--warning">
        <Icon name="info" />
        <div>{c.adminOnly}</div>
      </div>
    );
  }

  const stats = (totals: CostTotals) => (
    <div className="stats-row">
      <div className="stat-card">
        <div className="stat-card__label">{c.stats.cost}</div>
        <div className="stat-card__value cell-amount">{usd(totals.costUsd)}</div>
        <div className="stat-card__change">
          {c.stats.tokens(count(totals.inputTokens, lang), count(totals.outputTokens, lang))}
        </div>
      </div>
      <div className="stat-card">
        <div className="stat-card__label">{c.stats.calls}</div>
        <div className="stat-card__value">{count(totals.calls, lang)}</div>
      </div>
      <div className="stat-card">
        <div className="stat-card__label">{c.stats.average}</div>
        <div className="stat-card__value cell-amount">{usd(totals.averageCostUsd)}</div>
      </div>
      <div className="stat-card">
        <div className="stat-card__label">{c.stats.failed}</div>
        <div className="stat-card__value">{count(totals.failedCalls, lang)}</div>
        <div className="stat-card__change">{c.stats.failedShare(totals.failedCalls)}</div>
      </div>
    </div>
  );

  const totalsCells = (totals: CostTotals) => (
    <>
      <td className="cell-amount">{count(totals.calls, lang)}</td>
      <td className="cell-amount">{count(totals.failedCalls, lang)}</td>
      <td className="cell-amount">{count(totals.inputTokens, lang)}</td>
      <td className="cell-amount">{count(totals.outputTokens, lang)}</td>
      <td className="cell-amount">
        {count(totals.cacheWriteTokens, lang)} / {count(totals.cacheReadTokens, lang)}
      </td>
      <td className="cell-amount">{usd(totals.costUsd)}</td>
      <td className="cell-amount">{usd(totals.averageCostUsd)}</td>
    </>
  );

  const totalsHeaders = (
    <>
      <th className="cell-amount">{c.columns.calls}</th>
      <th className="cell-amount">{c.columns.failed}</th>
      <th className="cell-amount">{c.columns.inputTokens}</th>
      <th className="cell-amount">{c.columns.outputTokens}</th>
      <th className="cell-amount">{c.columns.cacheTokens}</th>
      <th className="cell-amount">{c.columns.cost}</th>
      <th className="cell-amount">{c.columns.average}</th>
    </>
  );

  const card = (title: string, body: React.ReactNode) => (
    <div className="card" style={{ marginBottom: "var(--space-4)" }}>
      <div className="card__header">
        <h3>{title}</h3>
      </div>
      <div className="card__body card__body--flush">
        <div className="table-wrap">{body}</div>
      </div>
    </div>
  );

  const actorName = (actor: Pick<CostActor, "actor" | "name" | "email">) =>
    actor.name ?? actor.email ?? (actor.actor === "email" ? c.byEmail : actor.actor);

  const eventActor = (event: CostEvent) =>
    event.actorName ?? (event.actor === "email" ? c.byEmail : event.actor);

  const sourceName = (source: string) => (c.sources as Record<string, string>)[source] ?? source;

  const loading = organizationId ? organization === undefined : platform === undefined;
  const data = organizationId ? organization : platform;
  // Quality is for the whole platform; one organisation's month shows its costs.
  const showQuality = !organizationId && tab === "quality";

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="barChart" />{" "}
            {organizationId ? (organization?.name ?? platform?.organizations.find((o) => o.key === organizationId)?.name ?? c.unknownOrganization) : c.title}
          </h1>
          <p className="page-header__subtitle">
            {showQuality ? c.quality.subtitle(month) : loading ? t.common.loading : c.subtitle(month)}
          </p>
        </div>
        <div className="page-header__actions">
          {organizationId && (
            <button type="button" className="btn btn--ghost" onClick={() => setOrganizationId(null)}>
              <Icon name="chevronLeft" /> {c.backToMonth}
            </button>
          )}
          <div className="btn-group">
            <button
              type="button"
              className="btn btn--secondary btn--icon"
              onClick={() => setMonth(shiftMonth(month, -1))}
              aria-label={c.previousMonth}
              title={c.previousMonth}
            >
              <Icon name="chevronLeft" />
            </button>
            <button type="button" className="btn btn--secondary" disabled>
              {month}
            </button>
            <button
              type="button"
              className="btn btn--secondary btn--icon"
              onClick={() => setMonth(shiftMonth(month, 1))}
              disabled={month >= thisMonth}
              aria-label={c.nextMonth}
              title={c.nextMonth}
            >
              <Icon name="chevronRight" />
            </button>
          </div>
          {!organizationId && !showQuality && (
            <button type="button" className="btn btn--primary" onClick={() => void csv()} disabled={downloading}>
              <Icon name="download" /> {downloading ? t.common.busy : c.downloadCsv}
            </button>
          )}
        </div>
      </div>

      {!organizationId && (
        <div className="tabs">
          <button type="button" className={`tab ${tab === "costs" ? "tab--active" : ""}`.trim()} onClick={() => setTab("costs")}>
            {c.tabs.costs}
          </button>
          <button type="button" className={`tab ${tab === "quality" ? "tab--active" : ""}`.trim()} onClick={() => setTab("quality")}>
            {c.tabs.quality}
          </button>
        </div>
      )}

      {showQuality ? (
        <QualityTab month={month} />
      ) : (
        <>
          {data && stats(data.total)}

          {data && data.total.calls === 0 && (
            <div className="alert alert--info" style={{ marginBottom: "var(--space-4)" }}>
              <Icon name="info" />
              <div>{c.empty}</div>
            </div>
          )}

          {!organizationId && platform && platform.total.calls > 0 && (
            <>
              {card(
                c.perDay,
                <table className="table">
                  <thead>
                    <tr>
                      <th>{c.columns.day}</th>
                      <th style={{ width: "50%" }} />
                      <th className="cell-amount">{c.columns.calls}</th>
                      <th className="cell-amount">{c.columns.cost}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {platform.days.map((day) => (
                      <tr key={day.key}>
                        <td>{day.key}</td>
                        <td>
                          <div className="progress">
                            <div className="progress__bar" style={{ width: `${share(day.totals.costUsd, largest(platform.days))}%` }} />
                          </div>
                        </td>
                        <td className="cell-amount">{count(day.totals.calls, lang)}</td>
                        <td className="cell-amount">{usd(day.totals.costUsd)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}

              {card(
                c.perModel,
                <table className="table">
                  <thead>
                    <tr>
                      <th>{c.columns.model}</th>
                      {totalsHeaders}
                    </tr>
                  </thead>
                  <tbody>
                    {platform.models.map((model) => (
                      <tr key={model.key}>
                        <td>{model.key}</td>
                        {totalsCells(model.totals)}
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}

              {card(
                c.perOrganization,
                <table className="table table--clickable">
                  <thead>
                    <tr>
                      <th>{c.columns.organization}</th>
                      {totalsHeaders}
                    </tr>
                  </thead>
                  <tbody>
                    {platform.organizations.map((row) => (
                      <tr key={row.key} onClick={() => setOrganizationId(row.key)}>
                        <td>
                          {row.name ?? <span className="text-soft">{c.unknownOrganization}</span>}
                          <div className="text-soft text-sm">{row.key}</div>
                        </td>
                        {totalsCells(row.totals)}
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}

              <p className="text-soft text-sm">{c.priceNote(platform.priceVersion)}</p>
            </>
          )}

          {organizationId && organization && organization.total.calls > 0 && (
            <>
              {card(
                c.perUser,
                <table className="table">
                  <thead>
                    <tr>
                      <th>{c.columns.user}</th>
                      {totalsHeaders}
                    </tr>
                  </thead>
                  <tbody>
                    {organization.users.map((actor) => (
                      <tr key={actor.actor}>
                        <td>
                          {actorName(actor)}
                          {actor.name && actor.email && <div className="text-soft text-sm">{actor.email}</div>}
                        </td>
                        {totalsCells(actor.totals)}
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}

              {card(
                c.perModel,
                <table className="table">
                  <thead>
                    <tr>
                      <th>{c.columns.model}</th>
                      {totalsHeaders}
                    </tr>
                  </thead>
                  <tbody>
                    {organization.models.map((model) => (
                      <tr key={model.key}>
                        <td>{model.key}</td>
                        {totalsCells(model.totals)}
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}

              {card(
                c.topEvents,
                <table className="table">
                  <thead>
                    <tr>
                      <th>{c.columns.when}</th>
                      <th>{c.columns.user}</th>
                      <th>{c.columns.source}</th>
                      <th>{c.columns.model}</th>
                      <th className="cell-amount">{c.columns.tokens}</th>
                      <th className="cell-amount">{c.columns.cacheTokens}</th>
                      <th className="cell-amount">{c.columns.cost}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {organization.topEvents.map((event) => (
                      <tr key={event.eventId}>
                        <td style={{ whiteSpace: "nowrap" }}>{event.at.slice(0, 16).replace("T", " ")} UTC</td>
                        <td>{eventActor(event)}</td>
                        <td>{sourceName(event.source)}</td>
                        <td>
                          {event.model}{" "}
                          {!event.succeeded && <span className="badge badge--danger">{c.failed}</span>}{" "}
                          {!event.priceKnown && <span className="badge badge--warning">{c.priceUnknown}</span>}
                        </td>
                        <td className="cell-amount">
                          {count(event.inputTokens, lang)} / {count(event.outputTokens, lang)}
                        </td>
                        <td className="cell-amount">
                          {count(event.cacheWriteTokens, lang)} / {count(event.cacheReadTokens, lang)}
                        </td>
                        <td className="cell-amount">{usd(event.costUsd)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>,
              )}
            </>
          )}
        </>
      )}
    </>
  );
}
