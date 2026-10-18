#!/usr/bin/env bash
# Builds the official-rules validator:
#
#   validator/build.sh   ->  validator/build/package/          classes, lib/, rulesets/
#                            validator/build/xchange-validator.zip   the Lambda package
#
# Fetches the pinned jars and the official rules (validation/fetch.py, cached
# in validation/.cache), compiles the sources with javac and packages them.
# Needs a JDK 21 (javac and jar; set JAVA_HOME if the one on PATH is older) and
# Python 3. No Maven: three pinned jars do not need a build system.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/.." && pwd)"
out="$here/build"
package="$out/package"

# Native Windows tools (javac from Git Bash) want Windows paths.
native() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

case "$(uname -s)" in
  MINGW* | MSYS* | CYGWIN*) separator=';' ;;
  *) separator=':' ;;
esac

python=python3
if ! python3 -c 'import sys' >/dev/null 2>&1; then python=python; fi

java_bin="${JAVA_HOME:+$JAVA_HOME/bin/}"

rm -rf "$out"
mkdir -p "$package"

"$python" "$(native "$root/validation/fetch.py")" "$(native "$package")"

classpath=""
for jar in "$package"/lib/*.jar; do
  classpath="${classpath:+$classpath$separator}$(native "$jar")"
done

sources=()
while IFS= read -r source; do sources+=("$(native "$source")"); done < <(find "$here/src" -name '*.java')

"${java_bin}javac" --release 21 -encoding UTF-8 -d "$(native "$package")" -cp "$classpath" "${sources[@]}"

# A Lambda Java zip: classes and resources at the root, dependencies in lib/.
"${java_bin}jar" --create --no-manifest --file "$(native "$out/xchange-validator.zip")" -C "$(native "$package")" .

echo "built $out/xchange-validator.zip"
