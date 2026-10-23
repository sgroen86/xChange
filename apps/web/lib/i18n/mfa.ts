/** Texts for two-step verification. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  codeStep: {
    title: "Tweestapsverificatie",
    subtitle: "Voer de code van zes cijfers uit je authenticator-app in.",
    code: "Code",
    verify: "Verifiëren",
    useRecovery: "Een herstelcode gebruiken",
    useApp: "De code uit de app gebruiken",
    recoveryCode: "Herstelcode",
    recoveryHelp: "Elke herstelcode werkt één keer.",
    back: "Terug naar inloggen",
    recoveryLeft: (left: number) =>
      `Herstelcode gebruikt. Je hebt er nog ${left}. Maak nieuwe aan via Instellingen voordat ze op zijn.`,
  },
  setup: {
    title: "Tweestapsverificatie instellen",
    requiredIntro:
      "Als platformbeheerder log je in met je wachtwoord én een code uit een authenticator-app, zoals Google Authenticator, Microsoft Authenticator of 1Password. Stel het nu in; het duurt een minuut.",
    optionalIntro:
      "Log voortaan in met je wachtwoord én een code uit een authenticator-app, zoals Google Authenticator, Microsoft Authenticator of 1Password.",
    scan: "1. Scan deze QR-code met de app.",
    manual: "Lukt scannen niet? Voer dan deze sleutel handmatig in:",
    qrAlt: "QR-code voor de authenticator-app",
    enterCode: "2. Voer de code van zes cijfers in die de app nu toont.",
    confirm: "Bevestigen",
    preparing: "Sleutel wordt aangemaakt…",
    expired: "Dit duurde te lang. Log opnieuw in.",
  },
  codes: {
    title: "Je herstelcodes",
    intro:
      "Ben je je telefoon kwijt, dan log je in met een van deze codes. Elke code werkt één keer. Ze worden maar één keer getoond: bewaar ze op een veilige plek, bijvoorbeeld in je wachtwoordmanager.",
    copy: "Kopiëren",
    download: "Downloaden",
    copied: "Herstelcodes gekopieerd.",
    copyFailed: "Kopiëren lukte niet; selecteer de codes en kopieer ze zelf.",
    saved: "Ik heb de herstelcodes bewaard",
    continue: "Doorgaan",
    fileHeading: "xChange-herstelcodes",
  },
  settings: {
    title: "Tweestapsverificatie",
    on: "Aan",
    off: "Uit",
    mandatory: "Verplicht",
    recommended: "Aanbevolen",
    help: "Aanbevolen: naast je wachtwoord een code uit een authenticator-app bij elke login. Je kunt het altijd weer uitzetten.",
    statusOn: (left: number) => `Staat aan. Je hebt nog ${left} herstelcode(s).`,
    statusOff: "Staat uit: je logt in met alleen je wachtwoord.",
    mandatoryHelp: "Voor platformbeheerders is tweestapsverificatie verplicht en kan het niet uit.",
    enable: "Aanzetten",
    regenerate: "Nieuwe herstelcodes",
    regenerateHelp: "Voer een code uit de app of een herstelcode in. De oude herstelcodes werken daarna niet meer.",
    disable: "Uitzetten",
    disableHelp: "Voer een code uit de app of een herstelcode in om tweestapsverificatie uit te zetten.",
    codeOrRecovery: "Code of herstelcode",
    enabled: "Tweestapsverificatie staat aan.",
    disabled: "Tweestapsverificatie staat uit.",
    renewed: "Nieuwe herstelcodes gemaakt.",
  },
  reset: {
    title: "Tweestapsverificatie herstellen",
    help:
      "Voor een gebruiker die telefoon én herstelcodes kwijt is. Tweestapsverificatie gaat uit; voor platformbeheerders is het verplicht, dus die stellen het bij de volgende login opnieuw in. Ga eerst na wie erom vraagt.",
    email: "E-mailadres van de gebruiker",
    submit: "Herstellen",
    confirm: (email: string) => `Tweestapsverificatie van ${email} uitzetten?`,
    done: (email: string) => `Tweestapsverificatie van ${email} staat uit.`,
  },
  nudge: {
    text: "Beveilig je account met tweestapsverificatie.",
    action: "Nu instellen",
    dismiss: "Verbergen",
  },
};

const en: typeof nl = {
  codeStep: {
    title: "Two-step verification",
    subtitle: "Enter the six-digit code from your authenticator app.",
    code: "Code",
    verify: "Verify",
    useRecovery: "Use a recovery code instead",
    useApp: "Use the code from the app",
    recoveryCode: "Recovery code",
    recoveryHelp: "Each recovery code works once.",
    back: "Back to login",
    recoveryLeft: (left: number) =>
      `Recovery code used. You have ${left} left. Create new ones in Settings before they run out.`,
  },
  setup: {
    title: "Set up two-step verification",
    requiredIntro:
      "As a platform administrator you log in with your password and a code from an authenticator app, such as Google Authenticator, Microsoft Authenticator or 1Password. Set it up now; it takes a minute.",
    optionalIntro:
      "From now on, log in with your password and a code from an authenticator app, such as Google Authenticator, Microsoft Authenticator or 1Password.",
    scan: "1. Scan this QR code with the app.",
    manual: "Can't scan it? Enter this key by hand instead:",
    qrAlt: "QR code for the authenticator app",
    enterCode: "2. Enter the six-digit code the app shows now.",
    confirm: "Confirm",
    preparing: "Creating your key…",
    expired: "This took too long. Please log in again.",
  },
  codes: {
    title: "Your recovery codes",
    intro:
      "If you lose your phone, log in with one of these codes. Each code works once. They are shown only once: keep them somewhere safe, such as your password manager.",
    copy: "Copy",
    download: "Download",
    copied: "Recovery codes copied.",
    copyFailed: "Copying did not work; select the codes and copy them yourself.",
    saved: "I have saved the recovery codes",
    continue: "Continue",
    fileHeading: "xChange recovery codes",
  },
  settings: {
    title: "Two-step verification",
    on: "On",
    off: "Off",
    mandatory: "Mandatory",
    recommended: "Recommended",
    help: "Recommended: a code from an authenticator app at every login, in addition to your password. You can turn it off again at any time.",
    statusOn: (left: number) => `On. You have ${left} recovery ${left === 1 ? "code" : "codes"} left.`,
    statusOff: "Off: you log in with your password only.",
    mandatoryHelp: "Two-step verification is mandatory for platform administrators and cannot be turned off.",
    enable: "Turn on",
    regenerate: "New recovery codes",
    regenerateHelp: "Enter a code from the app or a recovery code. The old recovery codes stop working.",
    disable: "Turn off",
    disableHelp: "Enter a code from the app or a recovery code to turn two-step verification off.",
    codeOrRecovery: "Code or recovery code",
    enabled: "Two-step verification is on.",
    disabled: "Two-step verification is off.",
    renewed: "New recovery codes created.",
  },
  reset: {
    title: "Reset two-step verification",
    help:
      "For a user who lost both phone and recovery codes. Two-step verification is turned off; it is mandatory for platform administrators, so they set it up again at their next login. Check first who is asking.",
    email: "User's email address",
    submit: "Reset",
    confirm: (email: string) => `Turn off two-step verification for ${email}?`,
    done: (email: string) => `Two-step verification for ${email} is off.`,
  },
  nudge: {
    text: "Protect your account with two-step verification.",
    action: "Set it up now",
    dismiss: "Hide",
  },
};

export const mfa = { nl, en };
