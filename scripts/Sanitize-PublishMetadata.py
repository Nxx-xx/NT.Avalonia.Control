"""Remove machine-specific paths from tracked publication metadata.

Run without arguments to check; pass --write to sanitize text files in place.
Ignored files and binary assets are not edited. Secrets are never printed.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
HOME = re.compile(rb"(?:[A-Za-z]:[\\/]+Users[\\/]+[^\s\\/\"'<>;]+|/(?:home|Users)/[^\s/\"'<>;]+)")


def sanitize(data):
    data = HOME.sub(b"<USER_HOME>", data)
    # Handle both ordinary paths and JSON-escaped paths without reformatting JSON.
    for path in (str(ROOT), str(ROOT).replace("\\", "/")):
        for form in (path, json.dumps(path)[1:-1]):
            data = re.sub(re.escape(form.encode()), b"<REPO_ROOT>", data, flags=re.I)
    hostname = os.environ.get("COMPUTERNAME", "")
    if hostname and len(hostname) >= 5:
        data = re.sub(rb"(?<![\w-])" + re.escape(hostname.encode()) + rb"(?![\w-])", b"<MACHINE>", data, flags=re.I)
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    paths = subprocess.check_output(["git", "-C", str(ROOT), "ls-files", "-z", "--cached", "--others", "--exclude-standard"])
    changed = []
    for name in sorted(set(p.decode("utf-8") for p in paths.split(b"\0") if p)):
        path = ROOT / name
        if not path.is_file():
            continue
        data = path.read_bytes()
        if b"\0" in data[:8192]:
            continue
        clean = sanitize(data)
        if clean != data:
            # Validate JSON before writing so masking cannot silently corrupt reports.
            if path.suffix.lower() == ".json":
                json.loads(clean.decode("utf-8-sig"))
            changed.append(name)
            if args.write:
                path.write_bytes(clean)
    print(json.dumps({"mode": "write" if args.write else "check", "files": changed, "count": len(changed)}, ensure_ascii=False))
    return 0 if args.write or not changed else 1


if __name__ == "__main__":
    sys.exit(main())
