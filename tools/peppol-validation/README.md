# Official validation

Every PEPPOL file xChange produces is checked against the **complete official rules** before
it can be downloaded. The same check runs in CI on the writer's reference files.

```
tools/peppol-validation/validate.sh peppol-bis-billing-3.0 tests/InvoicePlatform.Domain.Tests/EInvoicing/Ubl/Expected/*.xml
```

It prints every failed assert and exits 1 when any is `fatal`. It needs a JDK 21 (set
`JAVA_HOME` if the `java` on `PATH` is older) and Python 3.

## How it fits together

| Piece | What it is |
|---|---|
| `validation/rulesets.json` | Per ruleset: the release and the official artefacts (URL, SHA-256, file inside the archive). The single source for CI and production. |
| `validator/dependencies.json` | The validator's own jars (Saxon-HE 12, xmlresolver, aws-lambda-java-core), pinned. |
| `validation/fetch.py` | Downloads and verifies both, cached in `validation/.cache/`. |
| `validator/` | Java 21: `OfficialRules` compiles the rules once and runs them; `Handler` is the `xchange-validator` Lambda; `Cli` is the command line above. `build.sh` builds both. |
| `src/.../OfficialValidation/` | The API calls the Lambda after writing a file and before returning it. A fatal failure blocks the download; an unreachable validator does too. |

## Current artefacts

| Ruleset | Release | Artefacts |
|---|---|---|
| `peppol-bis-billing-3.0` | 3.0.21 | CEN EN 16931 UBL 1.3.16 (official ConnectingEurope XSLT); PEPPOL BIS 3.0.21 (compiled XSLT from `phive-rules-peppol` 4.6.3 - OpenPeppol publishes only Schematron for 3.0.21, and these rule texts are identical to it) |

## Adding a ruleset or moving to a new release

1. Add or change the entry in `validation/rulesets.json` (URL, SHA-256, entry, file name).
2. For a new ruleset: add its C# ruleset class (as `PeppolBisBilling30Ruleset`) and register it.
3. Regenerate the writer's reference files (`XCHANGE_UPDATE_REFERENCE_FILES=1 dotnet test`),
   review the diff, and run this script on them.

PEPPOL publishes new BIS releases in May and November.
