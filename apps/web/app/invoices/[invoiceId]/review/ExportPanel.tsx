"use client";

import { Icon } from "../../../../components/Icon";
import type { LocalIssue } from "../../../../lib/canonical";
import type { RuleViolation } from "../../../../lib/invoices";
import type { RulesetInfo } from "../../../../lib/settings";

export const CANONICAL_FORMAT = "canonical";

interface Props {
  rulesets: RulesetInfo[];
  format: string;
  onFormat: (format: string) => void;
  /** Numbers the server cannot read yet; they come before anything else. */
  localIssues: LocalIssue[];
  violations: RuleViolation[];
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
 * The format menu (organisation default preselected, a choice applies to this
 * download only) and, for a ruleset, what is still missing for it.
 */
export function ExportPanel(props: Props) {
  const { rulesets, format, onFormat, localIssues, violations, checking, checkFailed } = props;
  const ruleset = rulesets.find((r) => r.id === format) ?? null;

  const blockedReason = ruleset
    ? !props.apiConfigured
      ? "Zonder API is er geen export mogelijk."
      : !props.exportable
        ? "Boek de factuur eerst: het bestand wordt gemaakt uit de geboekte versie."
        : localIssues.length + violations.length > 0
          ? "Vul eerst aan wat hieronder staat."
          : null
    : null;

  const issues = [
    ...localIssues.map((issue) => ({ key: `local-${issue.path}`, path: issue.path, code: "Getal", message: issue.message })),
    ...violations.map((v) => ({
      key: `${v.ruleId}-${v.path}`,
      path: v.path,
      code: [v.ruleId, v.businessTerm].filter(Boolean).join(" · "),
      message: v.message,
    })),
  ];

  return (
    <div className="card">
      <div className="card__header">
        <h3>Exporteren</h3>
      </div>
      <div className="card__body">
        <div className="form__row">
          <div className="form__group">
            <label htmlFor="export-format">Formaat</label>
            <select id="export-format" value={format} onChange={(event) => onFormat(event.target.value)}>
              {rulesets.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.displayName}
                </option>
              ))}
              <option value={CANONICAL_FORMAT}>Canonieke XML</option>
            </select>
          </div>
          <div className="form__group" style={{ justifyContent: "flex-end" }}>
            <button
              type="button"
              className="btn btn--primary"
              onClick={props.onDownload}
              disabled={blockedReason !== null || props.downloading}
            >
              <Icon name="download" /> {ruleset && !props.exportable ? "Eerst boeken" : props.downloading ? "Bezig…" : "Downloaden"}
            </button>
          </div>
        </div>
        {blockedReason && <p className="text-soft text-sm">{blockedReason}</p>}
      </div>

      {ruleset && props.apiConfigured && (
        <div className="card__body card__body--flush">
          {issues.length > 0 ? (
            <>
              <div className="card__header">
                <h3>Aanvullen voor {ruleset.displayName}</h3>
                <span className="badge badge--warning">{issues.length}</span>
              </div>
              <div className="issue-list">
                {issues.map((issue) => (
                  <button type="button" className="issue issue--action" key={issue.key} onClick={() => props.onGoTo(issue.path)}>
                    <span className="badge badge--danger">Ontbreekt</span>
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
                  <div>De controle kon niet worden uitgevoerd. Probeer het opnieuw.</div>
                </div>
              ) : (
                <div className="alert alert--success">
                  <Icon name="check" />
                  <div>{checking ? "Controleren…" : `Compleet voor ${ruleset.displayName}.`}</div>
                </div>
              )}
            </div>
          )}
          <div className="card__body">
            <p className="text-muted text-xs">
              Gecontroleerd op verplichte velden en kernregels (release {ruleset.rulesRelease}). De
              ontvanger kan op de volledige officiële regelset controleren.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
