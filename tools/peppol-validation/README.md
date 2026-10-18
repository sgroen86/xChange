# PEPPOL validation

`validate.sh` checks UBL files against the official PEPPOL BIS Billing 3.0 rules, release
**3.0.21** (mandatory from 17 August 2026):

```
tools/peppol-validation/validate.sh tests/InvoicePlatform.Domain.Tests/EInvoicing/Ubl/Expected/*.xml
```

It prints every failed assert and exits 1 when any is `fatal`. CI runs it on the writer's
reference files before deploying the API (`.github/workflows/deploy-api.yml`). The writer
tests require the writer's output to equal those files, so a pass means the writer produces
valid documents for those invoices.

It needs Java 8 or later, `curl`, `unzip` and `sha256sum`. Artefacts are downloaded once into
`.cache/` (gitignored), pinned by version and SHA-256:

| Artefact | Source | Why |
|---|---|---|
| Saxon-HE 9.9.1-8 | Maven Central | Runs XSLT 2.0, still on Java 8 |
| `EN16931-UBL-validation.xslt` 1.3.16 | ConnectingEurope `validation-1.3.16` release | The CEN rules, the version PEPPOL 3.0.21 adopts |
| `PEPPOL-EN16931-UBL.xslt` | `phive-rules-peppol` 4.6.3, Maven Central | OpenPeppol publishes only Schematron for 3.0.21; this compiled copy has rule texts identical to it |

## Moving to a new release

PEPPOL publishes in May and November. For a new release:

1. Update the URLs and hashes in `validate.sh` (and the release in this file).
2. Update `PeppolBisBilling30Ruleset.RulesRelease` and any changed runtime rules.
3. Regenerate the reference files (`XCHANGE_UPDATE_REFERENCE_FILES=1 dotnet test`), review
   the diff, and run this script on them.
