/**
 * Two-step verification helpers without browser or API dependencies, so they
 * can be tested on their own. The calls themselves live in auth.ts (during
 * login) and accountMfa.ts (from the settings).
 */

/** What a sign-in call answered: a session, or the second step it still needs. */
export type SignInStep =
  | { kind: "session" }
  | { kind: "mfa"; challenge: string }
  | { kind: "mfaSetup"; challenge: string };

/** The parts of a login response that decide the next step. */
export interface SignInPayload {
  token?: string | null;
  mfaRequired?: boolean;
  mfaSetupRequired?: boolean;
  challenge?: string | null;
}

export function signInStep(payload: SignInPayload): SignInStep {
  if (payload.challenge && payload.mfaSetupRequired) return { kind: "mfaSetup", challenge: payload.challenge };
  if (payload.challenge && payload.mfaRequired) return { kind: "mfa", challenge: payload.challenge };
  return { kind: "session" };
}

export const CODE_LENGTH = 6;

/** Digits only, at most six: what an authenticator app shows, however it was typed or pasted. */
export function normalizeCode(input: string): string {
  return input.replace(/\D/g, "").slice(0, CODE_LENGTH);
}

export function isCompleteCode(code: string): boolean {
  return /^\d{6}$/.test(code);
}

/** The secret in groups of four, as people type it into an app by hand. */
export function formatSecret(secret: string): string {
  return (secret.match(/.{1,4}/g) ?? []).join(" ");
}

export const RECOVERY_FILE_NAME = "xchange-recovery-codes.txt";

/** The text file offered for download: a heading, who it is for (when known), the codes one per line. */
export function recoveryCodesText(heading: string, account: string, codes: readonly string[]): string {
  return [heading, ...(account ? [account] : []), "", ...codes, ""].join("\n");
}
