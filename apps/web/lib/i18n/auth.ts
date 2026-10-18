/** Texts for the auth area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  genericError: "Er ging iets mis. Probeer het opnieuw.",
  createAccount: "Account aanmaken",
  login: "Inloggen",
  toLogin: "Naar inloggen",
  welcome: (name: string) => `Welkom, ${name}.`,
  fields: {
    name: "Naam",
    yourName: "Je naam",
    organization: "Organisatie",
    companyName: "Bedrijfsnaam",
    country: "Land",
    chooseCountry: "Kies een land",
    email: "E-mailadres",
    password: "Wachtwoord",
    passwordHelp: "Minimaal 12 tekens.",
  },
  loginPage: {
    subtitle: "Log in om facturen te verwerken.",
    setupSubtitle: "Er bestaat nog geen account. Maak het eerste beheerdersaccount aan.",
    noApi: "Er is geen API geconfigureerd, dus inloggen is niet nodig. De app draait op mockdata.",
    welcomeBack: (name: string) => `Welkom terug, ${name}.`,
    noAccount: "Nog geen account?",
    signupFree: "Gratis aanmelden",
  },
  invite: {
    title: "Uitnodiging",
    invited: (organization: string, country: string) => `U bent uitgenodigd voor ${organization} (${country}).`,
    invalid: "Deze uitnodiging is ongeldig of verlopen.",
    askAdmin: "Vraag de beheerder om een nieuwe link.",
  },
  signup: {
    title: "Aanmelden",
    trial: (conversions: number) => `Probeer xChange gratis: ${conversions} conversies, geen betaalgegevens nodig.`,
    closed: "Aanmelden is nog niet open. Heb je al een account?",
    sentTo: "We hebben een e-mail gestuurd naar",
    sentNext: "Open de link daarin om je account te activeren; hij is 24 uur geldig. Geen mail gezien? Kijk ook in je spammap.",
    haveAccount: "Al een account?",
  },
  confirm: {
    title: "Account activeren",
    welcome: (name: string) => `Welkom bij xChange, ${name}.`,
    signupAgain: "Opnieuw aanmelden",
    creating: "Je account wordt aangemaakt…",
  },
};

const en: typeof nl = {
  genericError: "Something went wrong. Please try again.",
  createAccount: "Create account",
  login: "Log in",
  toLogin: "Go to login",
  welcome: (name: string) => `Welcome, ${name}.`,
  fields: {
    name: "Name",
    yourName: "Your name",
    organization: "Organisation",
    companyName: "Company name",
    country: "Country",
    chooseCountry: "Choose a country",
    email: "Email address",
    password: "Password",
    passwordHelp: "At least 12 characters.",
  },
  loginPage: {
    subtitle: "Log in to process invoices.",
    setupSubtitle: "No account exists yet. Create the first administrator account.",
    noApi: "No API is configured, so no login is needed. The app runs on mock data.",
    welcomeBack: (name: string) => `Welcome back, ${name}.`,
    noAccount: "No account yet?",
    signupFree: "Sign up for free",
  },
  invite: {
    title: "Invitation",
    invited: (organization: string, country: string) => `You have been invited to ${organization} (${country}).`,
    invalid: "This invitation is invalid or has expired.",
    askAdmin: "Ask the administrator for a new link.",
  },
  signup: {
    title: "Sign up",
    trial: (conversions: number) =>
      `Try xChange for free: ${conversions} ${conversions === 1 ? "conversion" : "conversions"}, no payment details needed.`,
    closed: "Sign-up is not open yet. Already have an account?",
    sentTo: "We have sent an email to",
    sentNext: "Open the link in it to activate your account; it is valid for 24 hours. No email? Check your spam folder too.",
    haveAccount: "Already have an account?",
  },
  confirm: {
    title: "Activate account",
    welcome: (name: string) => `Welcome to xChange, ${name}.`,
    signupAgain: "Sign up again",
    creating: "Creating your account…",
  },
};

export const auth = { nl, en };
