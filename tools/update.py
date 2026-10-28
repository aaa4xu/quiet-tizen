#!/usr/bin/env python3
"""Build Quiet Home and install it over the installed build on the owner's TV.

One run builds the launcher for the profile, packs .local/package/, uploads it,
installs it with recipes/update-launcher.sh apply under a new name
dev-YYYYMMDD-HHMMSS and takes the demo-mode frames with tools/screens.py.
The name, snapshot and DLL digest go to .local/updates.log for a later rollback.

The run restarts Quiet Home and shows its demo pages over the picture, so run
it only while the owner agrees. Quiet Home must already be installed from
SNAPSHOT (recipes/install-launcher.sh).

  python3 tools/update.py --host 10.0.0.5 --sdb PATH/TO/sdb \\
      --profile profiles/MODEL.json --snapshot home-01 --compare .local/baseline/OLD
"""
import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / ".local/package"
ARCHIVE = ROOT / ".local/package.tar.gz"
LOG = ROOT / ".local/updates.log"


def update_name(now):
    """Returns the update name for a run started at now; update-launcher.sh accepts only [A-Za-z0-9_-]."""
    return now.strftime("dev-%Y%m%d-%H%M%S")


def pack(source, archive):
    """Packs the files of source into a gzip tar the way update-launcher.sh unpacks it, owned by root and without macOS metadata."""
    def owned_by_root(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ""
        return info

    with tarfile.open(archive, "w:gz") as package:
        package.add(source, arcname=".", filter=owned_by_root)


def log_line(time, snapshot, name, digest, result):
    """Returns one tab-separated line of .local/updates.log."""
    return "\t".join((time.isoformat(timespec="seconds"), snapshot, name, digest, result)) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", required=True, help="The owner's TV IPv4 address")
    parser.add_argument("--sdb", help="Path to Samsung's separately installed SDB")
    parser.add_argument("--firmware", help="Software Version from About This TV, if the TV does not report it")
    parser.add_argument("--profile", type=Path, required=True, help="TV profile whose launcher container holds Quiet Home")
    parser.add_argument("--snapshot", required=True, help="Snapshot Quiet Home was installed from")
    parser.add_argument("--compare", type=Path, metavar="DIR", help="Compare the new frames with an earlier run's frames")
    parser.add_argument("--keep", type=int, metavar="N",
                        help="After the install, remove all but the newest N updates (update-launcher.sh prune)")
    args = parser.parse_args()
    if args.keep is not None and args.keep < 1:
        parser.error("--keep must be at least 1: the installed update is always kept")
    dll = json.loads(args.profile.read_text(encoding="utf-8"))["launcher"]["dll"]
    started = datetime.now().astimezone()
    name = update_name(started)
    tv = [sys.executable, str(ROOT / "tools/tv.py"), "--host", args.host]
    if args.sdb:
        tv += ["--sdb", args.sdb]
    if args.firmware:
        tv += ["--firmware", args.firmware]
    recipe = ["exec", "--script", str(ROOT / "recipes/update-launcher.sh")]

    subprocess.run([sys.executable, str(ROOT / "tools/build.py"), "launcher", "--profile", str(args.profile)], check=True)
    pack(PACKAGE, ARCHIVE)
    digest = hashlib.sha256((PACKAGE / dll).read_bytes()).hexdigest()
    print(f"Update {name}: {dll} {digest}", flush=True)
    subprocess.run([*tv, "push", str(ARCHIVE), "package.tar.gz"], check=True)
    applied = subprocess.run([*tv, *recipe, "--arg", "apply", "--arg", args.snapshot, "--arg", name]).returncode == 0
    with LOG.open("a", encoding="utf-8") as log:
        log.write(log_line(started, args.snapshot, name, digest, "applied" if applied else "apply-failed"))
    if not applied:
        sys.exit(f"Installing {name} failed. A failed script may have done part of its work: "
                 f"inspect updates/{name} in the snapshot before retrying.")
    if args.keep is not None:
        subprocess.run([*tv, *recipe, "--arg", "prune", "--arg", args.snapshot, "--arg", str(args.keep)], check=True)
    screens = [sys.executable, str(ROOT / "tools/screens.py"), *tv[2:], "--profile", str(args.profile),
               "--out", str(ROOT / ".local/baseline" / name)]
    if args.compare:
        screens += ["--compare", str(args.compare)]
    result = subprocess.run(screens).returncode
    print(f"Roll back with: tools/tv.py ... exec --script recipes/update-launcher.sh "
          f"--arg rollback --arg {args.snapshot} --arg {name}")
    sys.exit(result)


if __name__ == "__main__":
    main()
