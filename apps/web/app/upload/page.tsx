"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useRef, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { extractInvoice, isApiConfigured, toDraft } from "../../lib/api";
import { ApiError } from "../../lib/api";
import { mockDraft } from "../../lib/canonical";
import { useText } from "../../lib/i18n";
import { checkPdf, formatBytes, MAX_PDF_BYTES } from "../../lib/pdf";
import { submitImport } from "../../lib/imports";
import { putPdf } from "../../lib/pdfStore";
import { newInvoiceId, saveDraft, saveServerResult } from "../../lib/store";

/** Mock pipeline stages, named after the real flow in CLAUDE.md. Labels: t.upload.stages[key]. */
const STAGES = [
  { key: "extraction", ms: 900 },
  { key: "interpretation", ms: 1100 },
  { key: "draft", ms: 700 },
] as const;

export default function UploadPage() {
  const router = useRouter();
  const showToast = useToast();
  const t = useText();
  const inputRef = useRef<HTMLInputElement>(null);

  const [file, setFile] = useState<File | null>(null);
  /** Several PDFs at once: imported and read in the background. */
  const [batch, setBatch] = useState<File[]>([]);
  const [batchDone, setBatchDone] = useState(0);
  const [importing, setImporting] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  // The API refused with 402: no conversions left, so the way on is a plan or credits.
  const [outOfConversions, setOutOfConversions] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [stageIndex, setStageIndex] = useState(-1);
  const [progress, setProgress] = useState(0);

  const processing = stageIndex >= 0;

  const acceptFile = useCallback(
    async (candidate: File) => {
      setFile(candidate);
      setErrors([]);

      const result = await checkPdf(candidate);
      if (!result.ok) {
        setErrors(result.errors);
        showToast(t.upload.pdfRejected, "danger");
        return;
      }
      showToast(t.upload.readyToConvert(candidate.name), "success");
    },
    [showToast, t],
  );

  /** One file: read now, as before. Several: queued for the background. */
  const acceptFiles = useCallback(
    (files: FileList | null) => {
      const chosen = Array.from(files ?? []);
      if (chosen.length > 1 && isApiConfigured) {
        setFile(null);
        setErrors([]);
        setBatch(chosen);
        setBatchDone(0);
      } else if (chosen[0]) {
        setBatch([]);
        void acceptFile(chosen[0]);
      }
    },
    [acceptFile],
  );

  const onDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();
      setDragging(false);
      acceptFiles(event.dataTransfer.files);
    },
    [acceptFiles],
  );

  const importBatch = async () => {
    if (importing || batch.length === 0) return;
    setImporting(true);
    const refused: string[] = [];

    for (const candidate of batch) {
      const check = await checkPdf(candidate);
      if (!check.ok) {
        refused.push(`${candidate.name}: ${check.errors.join(" ")}`);
      } else {
        try {
          await submitImport(candidate);
        } catch (cause) {
          refused.push(`${candidate.name}: ${cause instanceof Error ? cause.message : t.upload.notAccepted}`);
        }
      }
      setBatchDone((done) => done + 1);
    }

    setImporting(false);
    if (refused.length > 0) {
      setErrors(refused);
      showToast(t.upload.batchPartial(batch.length - refused.length, batch.length), "warning");
    } else {
      showToast(t.upload.batchQueued(batch.length), "success");
      router.push("/imports");
    }
  };

  const clearFile = () => {
    setFile(null);
    setErrors([]);
    setOutOfConversions(false);
    if (inputRef.current) inputRef.current.value = "";
  };

  const convert = async () => {
    if (!file || errors.length > 0 || processing) return;

    const invoiceId = newInvoiceId();

    try {
      if (isApiConfigured) {
        // Real extraction. The stages are indicative only - the API does the
        // whole pipeline in one call, so progress cannot be reported per stage.
        setStageIndex(0);
        setProgress(15);

        const response = await extractInvoice(file);

        setStageIndex(STAGES.length - 1);
        setProgress(100);

        await putPdf(invoiceId, file);
        saveDraft({ ...toDraft(response, file.name, file.size), id: invoiceId });
        saveServerResult(invoiceId, {
          canonicalXml: response.canonicalXml,
          warnings: response.warnings,
          providerExecution: response.providerExecution,
        });

        const errorCount = response.warnings.filter((w) => w.severity === "error").length;
        showToast(
          errorCount > 0
            ? t.upload.readWithErrors(errorCount)
            : t.upload.read,
          errorCount > 0 ? "warning" : "success",
        );
      } else {
        // No API configured: the simulated pipeline, unchanged.
        for (let index = 0; index < STAGES.length; index++) {
          setStageIndex(index);
          setProgress(Math.round(((index + 1) / STAGES.length) * 100));
          await new Promise((resolve) => window.setTimeout(resolve, STAGES[index].ms));
        }

        await putPdf(invoiceId, file);
        saveDraft(mockDraft(invoiceId, file.name, file.size));
        showToast(t.upload.mockDraft, "success");
      }

      router.push(`/invoices/${invoiceId}/review`);
    } catch (cause) {
      setStageIndex(-1);
      setProgress(0);
      const message =
        cause instanceof ApiError ? cause.message : t.upload.unexpectedFailure;
      setOutOfConversions(cause instanceof ApiError && cause.status === 402);
      setErrors([message]);
      showToast(message, "danger");
    }
  };

  const canConvert = Boolean(file) && errors.length === 0 && !processing;

  return (
    <>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="fileUp" /> {t.upload.title}
          </h1>
          <p className="page-header__subtitle">{t.upload.subtitle}</p>
        </div>
      </div>

      <div className="grid-2">
        <div className="stack">
          <div className="card">
            <div className="card__header">
              <h3>{t.upload.selectPdf}</h3>
            </div>
            <div className="card__body">
              <div
                className={`dropzone ${dragging ? "dropzone--active" : ""} ${
                  errors.length > 0 ? "dropzone--invalid" : ""
                }`.trim()}
                onClick={() => inputRef.current?.click()}
                onDragOver={(event) => {
                  event.preventDefault();
                  setDragging(true);
                }}
                onDragLeave={() => setDragging(false)}
                onDrop={onDrop}
                role="button"
                tabIndex={0}
                onKeyDown={(event) => {
                  if (event.key === "Enter" || event.key === " ") inputRef.current?.click();
                }}
              >
                <div className="dropzone__icon">
                  <Icon name="upload" />
                </div>
                <div className="dropzone__text">
                  {t.upload.dropPrefix}
                  <strong>{t.upload.browse}</strong>
                </div>
                <div className="dropzone__text text-muted text-xs">
                  {t.upload.limit(formatBytes(MAX_PDF_BYTES))}
                </div>
              </div>

              <input
                ref={inputRef}
                type="file"
                accept="application/pdf,.pdf"
                multiple={isApiConfigured}
                hidden
                onChange={(event) => acceptFiles(event.target.files)}
              />

              {batch.length > 0 && (
                <div style={{ marginTop: "var(--space-4)" }}>
                  <div className="alert alert--info">
                    <Icon name="info" />
                    <div>
                      {t.upload.batchSelectedBefore(batch.length)}
                      <strong>{t.imports.title}</strong>
                      {t.upload.batchSelectedAfter}
                      {importing && t.upload.batchProgress(batchDone, batch.length)}
                    </div>
                  </div>
                  <ul className="alert__list">
                    {batch.map((candidate) => (
                      <li key={`${candidate.name}-${candidate.size}`}>
                        {candidate.name} · {formatBytes(candidate.size)}
                      </li>
                    ))}
                  </ul>
                  <div className="form__actions">
                    <button type="button" className="btn btn--primary" onClick={() => void importBatch()} disabled={importing}>
                      <Icon name="zap" /> {importing ? t.common.busy : t.upload.importBatch(batch.length)}
                    </button>
                    <button
                      type="button"
                      className="btn btn--secondary"
                      onClick={() => {
                        setBatch([]);
                        setErrors([]);
                      }}
                      disabled={importing}
                    >
                      {t.upload.clear}
                    </button>
                  </div>
                </div>
              )}

              {file && (
                <div style={{ marginTop: "var(--space-4)" }}>
                  <div className="file-card">
                    <div className="file-card__icon">
                      <Icon name="fileText" />
                    </div>
                    <div className="file-card__info">
                      <div className="file-card__name">{file.name}</div>
                      <div className="file-card__meta">
                        {formatBytes(file.size)}
                        {file.type ? ` · ${file.type}` : ""}
                      </div>
                    </div>
                    <div className="file-card__actions">
                      <button
                        type="button"
                        className="btn btn--ghost btn--icon"
                        onClick={clearFile}
                        disabled={processing}
                        aria-label={t.upload.removeFile}
                      >
                        <Icon name="trash" />
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {errors.length > 0 && (
                <div className="alert alert--danger" style={{ marginTop: "var(--space-4)" }}>
                  <Icon name="x" />
                  <div>
                    <div className="alert__title">{t.upload.validationFailed}</div>
                    <ul className="alert__list">
                      {errors.map((error) => (
                        <li key={error}>{error}</li>
                      ))}
                    </ul>
                    {outOfConversions && (
                      <Link href="/settings/#billing" className="btn btn--primary btn--sm" style={{ marginTop: "var(--space-3)" }}>
                        {t.upload.choosePlan}
                      </Link>
                    )}
                  </div>
                </div>
              )}

              <div className="form__actions" style={{ marginTop: "var(--space-5)" }}>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => void convert()}
                  disabled={!canConvert}
                >
                  <Icon name="zap" />
                  {processing ? t.upload.converting : t.upload.convert}
                </button>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={clearFile}
                  disabled={!file || processing}
                >
                  {t.upload.clear}
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="stack">
          <div className="card">
            <div className="card__header">
              <h3>{t.upload.processingTitle}</h3>
              {processing && <span className="badge badge--info">{t.upload.processingBadge}</span>}
            </div>
            <div className="card__body">
              {!processing && progress === 0 ? (
                <p className="text-soft text-sm">{t.upload.nothingYet}</p>
              ) : (
                <>
                  <div className="progress">
                    <div className="progress__bar" style={{ width: `${progress}%` }} />
                  </div>
                  <div className="progress-stages">
                    {STAGES.map((stage, index) => {
                      const state =
                        index < stageIndex || (!processing && progress === 100)
                          ? "done"
                          : index === stageIndex
                            ? "active"
                            : "";
                      return (
                        <div
                          key={stage.key}
                          className={`progress-stage ${state ? `progress-stage--${state}` : ""}`.trim()}
                        >
                          <span className="progress-stage__marker">
                            <Icon name={state === "done" ? "check" : "clock"} />
                          </span>
                          <span>{t.upload.stages[stage.key]}</span>
                        </div>
                      );
                    })}
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
