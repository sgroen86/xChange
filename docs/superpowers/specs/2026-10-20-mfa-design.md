# xChange: tweestapsverificatie (TOTP)

**Datum:** 2026-10-20
**Status:** gevraagd door de eigenaar; beslissingen hieronder zijn de zijne, de uitwerking niet
besproken. **Beleid gewijzigd op 2026-10-23**, zie hieronder.

## Wijziging 2026-10-23: optioneel voor klanten

Besluit van de eigenaar: tweestapsverificatie is **optioneel voor klanten** (bedrijfsbeheerders én
gebruikers) en blijft **verplicht alleen voor platformbeheerders** (`IsPlatformAdmin`). Reden: de
zelfaanmelding moet lopen als website → aanmelden → bevestigen → betalen → app, zonder
authenticator-app ertussen; het platformbeheerdersaccount (toegang tot elke organisatie) blijft
beschermd.

- `UserMfa.IsMandatoryFor(user)` is nu `user.IsPlatformAdmin`, niet meer ook rol `Admin`.
- Inloggen: wie geen platformbeheerder is en MFA niet aan heeft, krijgt meteen een sessie; met MFA
  aan blijft de codestap.
- `/signup/confirm` en `/invitations/{code}/accept` geven een sessie in plaats van de
  setup-challenge (die maken een bedrijfsbeheerder). `/auth/setup` maakt de platformbeheerder en
  geeft dus nog steeds de setup-challenge.
- Uitzetten mag voor iedereen behalve platformbeheerders; `mandatory` in `GET /account/mfa` volgt
  de nieuwe regel.
- Web: de kaart in Instellingen toont "Aanbevolen" in plaats van "Verplicht" en biedt uitzetten
  aan. Wie ingelogd is zonder MFA ziet boven de inhoud een melding "Beveilig je account met
  tweestapsverificatie" met een link naar `/settings/#two-step`; wegklikken geldt 30 dagen per
  browser (localStorage). De status wordt één keer per sessie opgevraagd, niet in mock-modus.
- Ongewijzigd: versleuteling, replay-bescherming, challenges, herstelcodes, lockout.

Waar hieronder "verplicht voor beheerders" staat, lees "verplicht voor platformbeheerders".

## Beslissingen van de eigenaar

- ~~**Optioneel voor iedereen, verplicht voor beheerders**~~ (vervangen op 2026-10-23, zie boven): rol `Admin` en platformbeheerders
  (`IsPlatformAdmin`) stellen het in bij hun volgende login, en meteen na het bevestigen van een
  aanmelding, het aannemen van een uitnodiging en de eerste-account-setup (die maken allemaal een
  Admin).
- **Methode**: authenticator-app (TOTP, RFC 6238: HMAC-SHA1, 6 cijfers, 30 s, ±1 stap) plus 10
  herstelcodes voor eenmalig gebruik. Geen e-mailcodes, geen passkeys.
- Bestaande sessies blijven geldig; afdwingen gebeurt bij de volgende login.

## Inloggen

```
POST /auth/login (juist wachtwoord)
  MFA aan                 → 200 { mfaRequired: true,      challenge, expiresAt }
  verplicht, nog niet aan → 200 { mfaSetupRequired: true, challenge, expiresAt }
  anders                  → 200 { token, expiresAt, user }   (ongewijzigd)

POST /auth/login/mfa         { challenge, code } of { challenge, recoveryCode } → sessie
                             (+ recoveryCodesLeft na een herstelcode)
POST /auth/mfa/setup         { challenge }        → { secret, otpauthUri }
POST /auth/mfa/setup/confirm { challenge, code }  → sessie + recoveryCodes[]
```

Ook `/auth/setup`, `/signup/confirm` en `/invitations/{code}/accept` geven de setup-challenge in
plaats van een sessie.

**Challenge**: 32 willekeurige bytes (hex), alleen de SHA-256 opgeslagen, 5 minuten geldig, aan
één gebruiker en één doel (`SignIn` of `Setup`) gebonden, eenmalig (voorwaardelijke delete), na
5 foute codes dood. Een foute code telt óók mee voor de bestaande lockout per adres (5 fouten =
15 minuten). Het juiste wachtwoord wist die teller pas als de sessie er echt is; anders zou elke
nieuwe challenge het raden opnieuw laten beginnen.

## Ingelogd

```
GET  /account/mfa                 → { enabled, mandatory, recoveryCodesLeft }
POST /account/mfa/setup           → { secret, otpauthUri }
POST /account/mfa/confirm         { code } → { recoveryCodes }
POST /account/mfa/recovery-codes  { code } → { recoveryCodes }   (nieuwe set)
POST /account/mfa/disable         { code }  (geweigerd als het verplicht is)
POST /platform/users/mfa/reset    { email } (alleen platformbeheerder)
```

`code` bij uitzetten en vernieuwen mag een TOTP-code of een herstelcode zijn: wie de telefoon
kwijt is maar optioneel MFA heeft, kan het dan toch uitzetten. Foute codes tellen ook hier mee
voor de lockout per adres.

**Telefoon kwijt en geen herstelcodes**: de platformbeheerder zet MFA van die gebruiker uit (op
e-mailadres; er is geen gebruikerslijst). Is MFA verplicht, dan stelt de gebruiker het bij de
volgende login opnieuw in.

## Opslag (`xchange-identity`)

```
ORG#<org>              / MFA#<userId>   secret (versleuteld), pendingSecret (versleuteld),
                                         lastUsedStep, recoveryCodes (SS van SHA-256), enabledAt
MFACHALLENGE#<sha256>  / CHALLENGE      userId, organizationId, purpose, failedAttempts, TTL
```

- **Geheim**: nooit in klare tekst. `ISecretProtector` (Application) met een KMS-implementatie
  (Infrastructure): sleutel `alias/xchange-mfa` uit `service.yml`, encryption context bindt de
  ciphertext aan `<org>/<userId>`, zodat hij niet naar een andere gebruiker te kopiëren is. Tests
  gebruiken een protector in het geheugen.
- **Replay**: `lastUsedStep` per gebruiker, bijgewerkt met een voorwaardelijke update
  (`lastUsedStep < :step`), dus dezelfde stap werkt nooit twee keer, ook niet bij gelijktijdige
  verzoeken.
- **Herstelcodes**: 12 tekens base32 (60 bits), getoond als `xxxx-xxxx-xxxx`, opgeslagen als
  SHA-256; verbruikt met een voorwaardelijke `DELETE` uit de set.
- Geheimen en codes worden nooit gelogd.

## Web

- Login: na het wachtwoord een codestap (`autocomplete="one-time-code"`, link "herstelcode
  gebruiken").
- Instellen (login, aanmelding, uitnodiging, instellingen): uitleg, QR-code van de otpauth-URI
  (client-side met `qrcode`, statische export), het geheim als tekst, code bevestigen, daarna de
  herstelcodes één keer met kopiëren/downloaden en een vinkje "ik heb ze bewaard".
- Instellingen: kaart "Tweestapsverificatie" met status, aanzetten, codes vernieuwen, uitzetten
  (niet als het verplicht is).
- Platform: kaart om MFA van een gebruiker te herstellen op e-mailadres.

## Infra

- `service.yml`: KMS-sleutel (Retain, rotatie aan) + alias, `kms:Encrypt`/`kms:Decrypt` voor
  de API-rol, `dynamodb:UpdateItem` op de identity-tabel, `Mfa__KmsKeyId` als omgevingsvariabele.
- `bootstrap.yml`: de deployrol mag een sleutel met tag `Project=xchange` aanmaken en beheren
  en `alias/xchange-*` aanmaken. **Met de hand toepassen vóór de push.**

## Buiten scope

Onthoud-dit-apparaat, e-mail/SMS, passkeys, een bedrijfsbeheerder die MFA van eigen gebruikers
herstelt (er is nog geen gebruikersbeheer).
