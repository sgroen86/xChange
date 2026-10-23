"use client";

import Link from "next/link";

import { Icon } from "../../../../components/Icon";
import type { LocalIssue } from "../../../../lib/canonical";
import { useText } from "../../../../lib/i18n";
import type { RuleViolation } from "../../../../lib/invoices";
import type { RulesetInfo } from "../../../../lib/settings";

export const CANONICAL_FORMAT = "canonical";

interface Props {
  rulesets: RulesetInfo[];
  format: string;
  onFormat: (format: string) => void;
  /** Why the menu opened on this format, when it is not the organisation's. */
  defaultNote: string | null;
  /** Set when the buyer is on a network and does not accept the chosen format. */
  receiverWarning: string | null;
  /** False without a running plan or credits: everything is checked, but the file cannot be downloaded. */
  canExport: boolean;
  /** Numbers the server cannot read yet; they come before anything else. */
  localIssues: LocalIssue[];
  violations: RuleViolation[];
  /** Official warnings from the last download; they did not block it. */
  officialWarnings: RuleViolation[];
  checking: boolean;
  checkFailed: boolean;
  /** Booked, and nothing changed since: only then is there a record to export. */
  exportable: boolean;
  apiConfigured: boolean;
  downloading: boolean;
  onDownload: () => void;
  onGoTo: (path: string) => void;
}

/**
 * The format menu (the customer's or else the organisation's default
 * preselected, a choice applies to this download only) and, for a ruleset,
 * what is still missing for it.
 */
export function ExportPanel(props: Props) {
  const { rulesets, format, onFormat, localIssues, violations, checking, checkFailed } = props;
  const ruleset = rulesets.find((r) => r.id === format) ?? null;
  const t = useText();
  const text = t.review.export;

  const blockedReason = !props.canExport
    ? text.blockedNoPlan
    : ruleset
    ? !props.apiConfigured
      ? text.blockedNoApi
      : !props.exportable
        ? text.blockedNotBooked
        : localIssues.length + violations.length > 0
          ? text.blockedIncomplete
          : null
    : null;

  const issues = [
    ...localIssues.map((issue) => ({ key: `local-${issue.path}`, path: issue.path, code: text.numberCode, message: issue.message })),
    ...violations.map((v) => ({
      key: `${v.ruleId}-${v.path}`,
      path: v.path,
      code: [v.ruleId, v.businessTerm].filter(Boolean).join(" · "),
      message: v.message,
    })),
  ];

  return (
    // .form gives the format menu and its row the same styling as the invoice fields.
    <div className="card form">
      <div className="card__header">
        <h3>{text.title}</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <div className="form__group">
            <label htmlFor="export-format">{text.format}</label>
            <select id="export-format" value={format} onChange={(event) => onFormat(event.target.value)}>
              {rulesets.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.displayName}
                </option>
              ))}
              <option value={CANONICAL_FORMAT}>{t.review.canonicalXml}</option>
            </select>
            {props.defaultNote && <div className="form-help">{props.defaultNote}</div>}
          </div>
          <div className="form__group" style={{ justifyContent: "flex-end" }}>
            <button
              type="button"
              className="btn btn--primary"
              onClick={props.onDownload}
              disabled={blockedReason !== null || props.downloading}
            >
              <Icon name="download" /> {ruleset && !props.exportable ? text.bookFirst : props.downloading ? t.common.busy : text.download}
            </button>
          </div>
        </div>
        {blockedReason && (
          <p className="text-soft text-sm">
            {blockedReason}
            {!props.canExport && (
              <>
                {" "}
                <Link href="/settings/#billing">{text.choosePlan}</Link>
              </>
            )}
          </p>
        )}
        {props.receiverWarning && (
          <div className="alert alert--warning">
            <Icon name="info" />
            <div>{props.receiverWarning}</div>
          </div>
        )}
      </div>

      {ruleset && props.apiConfigured && (
        <div className="card__body card__body--flush">
          {issues.length > 0 ? (
            <>
              <div className="card__header">
                <h3>{text.completeFor(ruleset.displayName)}</h3>
                <span className="badge badge--warning">{issues.length}</span>
              </div>
              <div className="issue-list">
                {issues.map((issue) => (
                  <button type="button" className="issue issue--action" key={issue.key} onClick={() => props.onGoTo(issue.path)}>
                    <span className="badge badge--danger">{text.missing}</span>
                    <span className="issue__body">
                      <span className="issue__message">{issue.message}</span>
                      <span className="issue__path">{issue.code}</span>
                    </span>
                  </button>
                ))}
              </div>
            </>
          ) : (
            <div className="card__body">
              {checkFailed ? (
                <div className="alert alert--warning">
                  <Icon name="info" />
                  <div>{text.checkFailed}</div>
                </div>
              ) : (
                <div className="alert alert--success">
                  <Icon name="check" />
                  <div>{checking ? text.checking : text.complete(ruleset.displayName)}</div>
                </div>
              )}
            </div>
          )}
          {props.officialWarnings.length > 0 && (
            <>
              <div className="card__header">
                <h3>{text.lastWarnings}</h3>
                <span className="badge badge--warning">{props.officialWarnings.length}</span>
              </div>
              <div className="issue-list">
                {props.officialWarnings.map((warning) => (
                  <div className="issue" key={`${warning.ruleId}-${warning.path}`}>
                    <span className="badge badge--warning">{text.warning}</span>
                    <div className="issue__body">
                      <div className="issue__message">{warning.message}</div>
                      <div className="issue__path">
                        {warning.ruleId} · {warning.path}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
          <div className="card__body">
            <p className="text-muted text-xs">{text.footnote(ruleset.rulesRelease)}</p>
          </div>
        </div>
      )}
    </div>
  );
}
