/** PDF validation for the upload page. Structural checks only — no parsing. */

export const MAX_PDF_BYTES = 15 * 1024 * 1024;

export interface PdfCheckResult {
  ok: boolean;
  errors: string[];
}

/** Format a byte count the way the file card shows it. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} kB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}

/**
 * Reads the first bytes of the file and confirms the %PDF- header. Extension
 * and MIME type are both spoofable, so the magic number is the real check.
 */
export async function checkPdf(file: File): Promise<PdfCheckResult> {
  const errors: string[] = [];

  const hasPdfExtension = /\.pdf$/i.test(file.name);
  const hasPdfType = file.type === "application/pdf" || file.type === "";

  if (!hasPdfExtension) {
    errors.push("Het bestand heeft geen .pdf-extensie.");
  }
  if (!hasPdfType) {
    errors.push(`Onverwacht bestandstype: ${file.type}. Alleen PDF wordt ondersteund.`);
  }
  if (file.size === 0) {
    errors.push("Het bestand is leeg (0 bytes).");
  }
  if (file.size > MAX_PDF_BYTES) {
    errors.push(
      `Het bestand is ${formatBytes(file.size)}. De limiet is ${formatBytes(MAX_PDF_BYTES)}.`,
    );
  }

  if (file.size > 0) {
    const header = await readHeader(file);
    if (header !== "%PDF-") {
      errors.push("Het bestand begint niet met een geldige PDF-header (%PDF-).");
    }
  }

  return { ok: errors.length === 0, errors };
}

async function readHeader(file: File): Promise<string> {
  try {
    const slice = file.slice(0, 5);
    const buffer = await slice.arrayBuffer();
    return new TextDecoder().decode(buffer);
  } catch {
    return "";
  }
}
