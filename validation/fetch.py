"""Downloads what the official validation needs, pinned and verified.

    python validation/fetch.py <destination>

Reads validation/rulesets.json (the official rules per ruleset) and
validator/dependencies.json (the validator's jars). Every download is checked
against its SHA-256 and cached in validation/.cache, so a second run is
offline. Writes into <destination>:

    lib/<jar>                          the validator's dependencies
    rulesets/<ruleset>/<file>.xslt     the compiled official rules
    rulesets/index.txt                 one line per ruleset, tab-separated:
                                       id, release, then name=file per artefact

The index is plain text so the Java side needs no JSON library. Standard
library only.
"""

import hashlib
import json
import shutil
import sys
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CACHE = ROOT / "validation" / ".cache"


def download(url: str, sha256: str) -> Path:
    CACHE.mkdir(parents=True, exist_ok=True)
    target = CACHE / f"{sha256[:16]}-{url.rsplit('/', 1)[-1]}"
    if not target.exists():
        partial = target.with_suffix(target.suffix + ".part")
        with urllib.request.urlopen(url) as response, open(partial, "wb") as out:
            shutil.copyfileobj(response, out)
        partial.replace(target)

    actual = hashlib.sha256(target.read_bytes()).hexdigest()
    if actual != sha256:
        target.unlink()
        sys.exit(f"checksum mismatch for {url}: expected {sha256}, got {actual}")
    return target


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    destination = Path(sys.argv[1])

    lib = destination / "lib"
    lib.mkdir(parents=True, exist_ok=True)
    for dependency in json.loads((ROOT / "validator" / "dependencies.json").read_text(encoding="utf-8")):
        shutil.copyfile(download(dependency["url"], dependency["sha256"]), lib / dependency["file"])

    rulesets = json.loads((ROOT / "validation" / "rulesets.json").read_text(encoding="utf-8"))
    index_lines = []
    for ruleset_id, ruleset in rulesets.items():
        folder = destination / "rulesets" / ruleset_id
        folder.mkdir(parents=True, exist_ok=True)
        entries = []
        for artefact in ruleset["artefacts"]:
            archive = download(artefact["url"], artefact["sha256"])
            with zipfile.ZipFile(archive) as zipped, zipped.open(artefact["entry"]) as source:
                (folder / artefact["file"]).write_bytes(source.read())
            entries.append(f"{artefact['name']}={artefact['file']}")
        index_lines.append("\t".join([ruleset_id, ruleset["release"], *entries]))

    (destination / "rulesets" / "index.txt").write_text("\n".join(index_lines) + "\n", encoding="utf-8")
    print(f"fetched {len(rulesets)} ruleset(s) into {destination}")


if __name__ == "__main__":
    main()
