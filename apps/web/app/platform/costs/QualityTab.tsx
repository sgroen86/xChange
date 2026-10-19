"use client";

import { useEffect, useState } from "react";

import { Icon } from "../../../components/Icon";
import { useToast } from "../../../components/Toast";
import { getPlatformQuality, type PlatformQuality, type QualityCounts } from "../../../lib/costs";
import { text, useLang, useText, type Lang } from "../../../lib/i18n";
import { formatUsd } from "../../../lib/money";
import { fieldLabel, formatPercent } from "../../../lib/qualityFields";

function count(value: number, lang: Lang): string {
  return value.toLocaleString(lang === "en" ? "en-GB" : "nl-NL");
}

/**
 * How well the model read the invoices reviewers booked in a month, for the
 * platform administrator only: accuracy and perfect invoices in total, per
 * model (with what a perfect invoice cost), per organisation, and the fields
 * that go wrong most. Percentages arrive as decimal strings and are shown as
 * they come.
 */
export default function QualityTab({ month }: { month: string }) {
  const showToast = useToast();
  const t = useText();
  const lang = useLang();
  const q = t.costs.quality;

  // Kept with the month it belongs to, so a change shows "loading" without resetting state in an effect.
  const [loaded, setLoaded] = useState<{ month: string; data: PlatformQuality | null } | null>(null);

  useEffect(() => {
    let cancelled = false;
    getPlatformQuality(month)
      .then((data) => !cancelled && setLoaded({ month, data }))
      .catch((cause: unknown) => {
        if (cancelled) return;
        setLoaded({ month, data: null });
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [month, showToast]);

  const quality = loaded?.month === month ? loaded.data : undefined;
  if (quality === undefined) return <p className="text-soft">{t.common.loading}</p>;
  if (quality === null) return null;

  const usd = (value: string | null) => formatUsd(value, lang);
  const percent = (value: string | null) => formatPercent(value, lang);
  const read = quality.total.read;

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

  const countsCells = (counts: QualityCounts, shown: QualityCounts) => (
    <>
      <td className="cell-amount">{percent(counts.fieldAccuracyPercent)}</td>
      <td className="cell-amount">{percent(shown.fieldAccuracyPercent)}</td>
      <td className="cell-amount">{percent(counts.perfectPercent)}</td>
      <td className="cell-amount">{count(counts.invoices, lang)}</td>
    </>
  );

  const countsHeaders = (
    <>
      <th className="cell-amount">{q.columns.accuracy}</th>
      <th className="cell-amount">{q.columns.accuracyAfterLearning}</th>
      <th className="cell-amount">{q.columns.perfect}</th>
      <th className="cell-amount">{q.columns.measured}</th>
    </>
  );

  return (
    <>
      <div className="stats-row">
        <div className="stat-card">
          <div className="stat-card__label">{q.stats.accuracy}</div>
          <div className="stat-card__value">{percent(read.fieldAccuracyPercent)}</div>
          <div className="stat-card__change">
            {q.stats.fieldsCorrect(count(read.fields.correct, lang), count(read.fields.counted, lang))}
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-card__label">{q.stats.perfect}</div>
          <div className="stat-card__value">{percent(read.perfectPercent)}</div>
          <div className="stat-card__change">{q.stats.afterLearning(percent(quality.total.shown.perfectPercent))}</div>
        </div>
        <div className="stat-card">
          <div className="stat-card__label">{q.stats.measured}</div>
          <div className="stat-card__value">{count(read.invoices, lang)}</div>
          <div className="stat-card__change">
            {q.stats.perfectOf(count(read.perfectInvoices, lang), count(read.invoices, lang))}
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-card__label">{q.stats.costPerPerfect}</div>
          <div className="stat-card__value cell-amount">{usd(quality.costPerPerfectInvoiceUsd)}</div>
          <div className="stat-card__change">{q.stats.modelCost(usd(quality.costUsd))}</div>
        </div>
      </div>

      {read.invoices === 0 ? (
        <div className="alert alert--info" style={{ marginBottom: "var(--space-4)" }}>
          <Icon name="info" />
          <div>{q.empty}</div>
        </div>
      ) : (
        <>
          {card(
            q.perModel,
            <table className="table">
              <thead>
                <tr>
                  <th>{q.columns.model}</th>
                  {countsHeaders}
                  <th className="cell-amount">{q.columns.cost}</th>
                  <th className="cell-amount">{q.columns.costPerPerfect}</th>
                </tr>
              </thead>
              <tbody>
                {quality.models.map((row) => (
                  <tr key={row.key}>
                    <td>{row.key}</td>
                    {countsCells(row.sums.read, row.sums.shown)}
                    <td className="cell-amount">{usd(row.costUsd)}</td>
                    <td className="cell-amount">{usd(row.costPerPerfectInvoiceUsd)}</td>
                  </tr>
                ))}
              </tbody>
            </table>,
          )}

          {card(
            q.perOrganization,
            <table className="table">
              <thead>
                <tr>
                  <th>{q.columns.organization}</th>
                  {countsHeaders}
                </tr>
              </thead>
              <tbody>
                {quality.organizations.map((row) => (
                  <tr key={row.key}>
                    <td>
                      {row.name ?? <span className="text-soft">{t.costs.unknownOrganization}</span>}
                      <div className="text-soft text-sm">{row.key}</div>
                    </td>
                    {countsCells(row.sums.read, row.sums.shown)}
                  </tr>
                ))}
              </tbody>
            </table>,
          )}

          {card(
            q.fields,
            quality.fields.length === 0 ? (
              <p className="text-soft" style={{ padding: "var(--space-4)" }}>
                {q.noFields}
              </p>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>{q.columns.field}</th>
                    <th className="cell-amount">{q.columns.counted}</th>
                    <th className="cell-amount">{q.columns.corrected}</th>
                    <th className="cell-amount">{q.columns.missed}</th>
                    <th className="cell-amount">{q.columns.extra}</th>
                    <th className="cell-amount">{q.columns.wrongAfterLearning}</th>
                  </tr>
                </thead>
                <tbody>
                  {quality.fields.map((row) => (
                    <tr key={row.field}>
                      <td>
                        {fieldLabel(row.field, t)}
                        <div className="text-soft text-sm">{row.field}</div>
                      </td>
                      <td className="cell-amount">{count(row.read.counted, lang)}</td>
                      <td className="cell-amount">{count(row.read.corrected, lang)}</td>
                      <td className="cell-amount">{count(row.read.missed, lang)}</td>
                      <td className="cell-amount">{count(row.read.extra, lang)}</td>
                      <td className="cell-amount">
                        {count(row.shown.corrected + row.shown.missed + row.shown.extra, lang)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ),
          )}
        </>
      )}

      <p className="text-soft text-sm">{q.note}</p>
      <p className="text-soft text-sm">{q.costNote}</p>
    </>
  );
}
