#!/usr/bin/env bash
# Validates UBL files against the official rules of a ruleset, with the same
# validator and the same pinned artefacts the API uses in production:
#
#   tools/peppol-validation/validate.sh <ruleset-id> file.xml [file.xml ...]
#   tools/peppol-validation/validate.sh peppol-bis-billing-3.0 tests/.../Expected/*.xml
#
# Prints every failed assert and exits 1 if any is fatal. Builds the validator
# first (validator/build.sh); needs a JDK 21 (set JAVA_HOME if the java on
# PATH is older) and Python 3. The artefacts per ruleset are listed in
# validation/rulesets.json.
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "usage: $0 <ruleset-id> file.xml [file.xml ...]" >&2
  exit 2
fi

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"

"$root/validator/build.sh" >/dev/null

native() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) separator=';' ;;
  *) separator=':' ;;
esac

package="$root/validator/build/package"
classpath="$(native "$package")${separator}$(native "$package/lib")/*"

ruleset="$1"
shift
files=()
for file in "$@"; do files+=("$(native "$file")"); done

"${JAVA_HOME:+$JAVA_HOME/bin/}java" -cp "$classpath" xchange.validator.Cli "$ruleset" "${files[@]}"
