#!/usr/bin/env bash
# Validates UBL files against the official PEPPOL BIS Billing 3.0 rules,
# release 3.0.21: the CEN EN 16931 rules (artefacts 1.3.16) and the PEPPOL
# rules, including the Dutch national ones.
#
#   tools/peppol-validation/validate.sh file.xml [file.xml ...]
#
# Prints every failed assert and exits 1 if any of them is fatal. Needs Java 8
# or later, curl, unzip and sha256sum; everything else is downloaded once into
# .cache next to this script, pinned and checked by SHA-256.
#
# Why these artefacts (see README.md):
#   - CEN: the official compiled XSLT from ConnectingEurope, version 1.3.16,
#     which is the version PEPPOL 3.0.21 adopts.
#   - PEPPOL: OpenPeppol publishes only Schematron for 3.0.21, no compiled
#     XSLT. phive-rules-peppol 4.6.3 contains a compiled copy whose rule texts
#     are identical to the 3.0.21 Schematron.
#   - Saxon-HE 9.9: XSLT 2.0, and still runs on Java 8.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
cache="$here/.cache"
mkdir -p "$cache"

SAXON_URL="https://repo1.maven.org/maven2/net/sf/saxon/Saxon-HE/9.9.1-8/Saxon-HE-9.9.1-8.jar"
SAXON_SHA="504ad5c71b55a3fd04c5609419b5e51d51b419f26628b4b6254c38766487abdf"
CEN_URL="https://github.com/ConnectingEurope/eInvoicing-EN16931/releases/download/validation-1.3.16/en16931-ubl-1.3.16.zip"
CEN_SHA="bafada015efbc5248bf5e05ad2191e1d9833ef96e9dd5f4bce420a747342da85"
PHIVE_URL="https://repo1.maven.org/maven2/com/helger/phive/rules/phive-rules-peppol/4.6.3/phive-rules-peppol-4.6.3.jar"
PHIVE_SHA="8d746eed379b5e9e8cd58530e1b8a7931786d089fb94d785e99b9be142c0431b"
PEPPOL_XSLT_IN_JAR="external/schematron/openpeppol/2026.5/xslt/PEPPOL-EN16931-UBL.xslt"

if [ "$#" -eq 0 ]; then
  echo "usage: $0 file.xml [file.xml ...]" >&2
  exit 2
fi

# Java on Windows (Git Bash) wants Windows paths.
native() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

fetch() {
  local url="$1" file="$2" sha="$3"
  if [ ! -f "$file" ]; then
    curl -sSfL -o "$file.part" "$url"
    mv "$file.part" "$file"
  fi
  if ! echo "$sha  $file" | sha256sum -c --status; then
    echo "checksum mismatch for $file (from $url)" >&2
    rm -f "$file"
    exit 1
  fi
}

fetch "$SAXON_URL" "$cache/saxon-he-9.9.1-8.jar" "$SAXON_SHA"
fetch "$CEN_URL" "$cache/en16931-ubl-1.3.16.zip" "$CEN_SHA"
fetch "$PHIVE_URL" "$cache/phive-rules-peppol-4.6.3.jar" "$PHIVE_SHA"

cen_xslt="$cache/cen/EN16931-UBL-validation.xslt"
peppol_xslt="$cache/peppol/PEPPOL-EN16931-UBL.xslt"

[ -f "$cen_xslt" ] || unzip -q -o -j "$cache/en16931-ubl-1.3.16.zip" "xslt/EN16931-UBL-validation.xslt" -d "$cache/cen"
[ -f "$peppol_xslt" ] || unzip -q -o -j "$cache/phive-rules-peppol-4.6.3.jar" "$PEPPOL_XSLT_IN_JAR" -d "$cache/peppol"

saxon() {
  java -jar "$(native "$cache/saxon-he-9.9.1-8.jar")" "$@"
}

fatal_total=0
for file in "$@"; do
  echo "== $file"
  for xslt in "$cen_xslt" "$peppol_xslt"; do
    svrl="$cache/report.svrl"
    saxon -s:"$(native "$file")" -xsl:"$(native "$xslt")" -o:"$(native "$svrl")"
    failures="$(saxon -s:"$(native "$svrl")" -xsl:"$(native "$here/report.xsl")")"
    if [ -n "$failures" ]; then
      printf '%s\n' "$failures" | sed 's/^/   /'
      fatal=$(printf '%s\n' "$failures" | grep -c '^fatal' || true)
      fatal_total=$((fatal_total + fatal))
    fi
  done
done

if [ "$fatal_total" -gt 0 ]; then
  echo "$fatal_total fatal rule violation(s)." >&2
  exit 1
fi

echo "No fatal rule violations."
