"use client";

import { useRouter } from "next/navigation";
import { useCallback, useRef, useState } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { extractInvoice, isApiConfigured, toDraft } from "../../lib/api";
import { ApiError } from "../../lib/api";
import { mockDraftFromFile } from "../../lib/canonical";
import { checkPdf, formatBytes, MAX_PDF_BYTES } from "../../lib/pdf";
import { putPdf } from "../../lib/pdfStore";
import { newInvoiceId, saveDraft, saveServerResult } from "../../lib/store";

/** Mock pipeline stages, named after the real flow in CLAUDE.md. */
const STAGES = [
  { key: "extraction", label: "Documentextractie", ms: 900 },
  { key: "interpretation", label: "Factuurinterpretatie", ms: 1100 },
  { key: "draft", label: "Concept opstellen", ms: 700 },
];

export default function UploadPage() {
  const router = useRouter();
  const showToast = useToast();
  const inputRef = useRef<HTMLInputElement>(null);

  const [file, setFile] = useState<File | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
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
        showToast("PDF afgekeurd — controleer de meldingen.", "danger");
        return;
      }
      showToast(`${candidate.name} is klaar om te converteren.`, "success");
    },
    [showToast],
  );

  const onDrop = useCallback(
    (event: React.DragEvent<HTMLDivElement>) => {
      event.preventDefault();
      setDragging(false);
      const dropped = event.dataTransfer.files?.[0];
      if (dropped) void acceptFile(dropped);
    },
    [acceptFile],
  );

  const clearFile = () => {
    setFile(null);
    setErrors([]);
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
            ? `Factuur gelezen met ${errorCount} fout(en). Controleer de gegevens.`
            : "Factuur gelezen. Controleer de gegevens.",
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
        saveDraft(mockDraftFromFile(invoiceId, file.name, file.size));
        showToast("Concept aangemaakt (mockdata). Controleer de gegevens.", "success");
      }

      router.push(`/invoices/${invoiceId}/review`);
    } catch (cause) {
      setStageIndex(-1);
      setProgress(0);
      const message =
        cause instanceof ApiError ? cause.message : "De conversie is onverwacht mislukt.";
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
            <Icon name="fileUp" /> Factuur uploaden
          </h1>
          <p className="page-header__subtitle">
            Upload een PDF-factuur. De verwerking is in deze fase gesimuleerd.
          </p>
        </div>
      </div>

      <div className="grid-2">
        <div className="stack">
          <div className="card">
            <div className="card__header">
              <h3>PDF selecteren</h3>
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
                  Sleep een PDF hierheen of <strong>blader</strong>
                </div>
                <div className="dropzone__text text-muted text-xs">
                  Alleen PDF, maximaal {formatBytes(MAX_PDF_BYTES)}
                </div>
              </div>

              <input
                ref={inputRef}
                type="file"
                accept="application/pdf,.pdf"
                hidden
                onChange={(event) => {
                  const chosen = event.target.files?.[0];
                  if (chosen) void acceptFile(chosen);
                }}
              />

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
                        aria-label="Bestand verwijderen"
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
                    <div className="alert__title">PDF-validatie mislukt</div>
                    <ul className="alert__list">
                      {errors.map((error) => (
                        <li key={error}>{error}</li>
                      ))}
                    </ul>
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
                  {processing ? "Bezig met converteren…" : "Factuur converteren"}
                </button>
                <button
                  type="button"
                  className="btn btn--secondary"
                  onClick={clearFile}
                  disabled={!file || processing}
                >
                  Wissen
                </button>
              </div>
            </div>
          </div>
        </div>

        <div className="stack">
          <div className="card">
            <div className="card__header">
              <h3>Verwerking</h3>
              {processing && <span className="badge badge--info">Bezig</span>}
            </div>
            <div className="card__body">
              {!processing && progress === 0 ? (
                <p className="text-soft text-sm">
                  Nog niets te verwerken. Selecteer een PDF en start de conversie.
                </p>
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
                          <span>{stage.label}</span>
                        </div>
                      );
                    })}
                  </div>
                </>
              )}
            </div>
          </div>

          <div className="card">
            <div className="card__header">
              <h3>Wat er nog niet gebeurt</h3>
            </div>
            <div className="card__body">
              <div className="alert alert--info">
                <Icon name="info" />
                <div>
                  Er wordt geen OCR, taalmodel, AWS-dienst of PEPPOL-verzending aangeroepen. De
                  gegevens op het beoordelingsscherm zijn mockdata.
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}
