#!/usr/bin/env python3
"""Build and install Quiet Home, or install one saved local package.

  python3 tools/update.py --host 192.0.2.50 --sdb PATH/TO/sdb \\
      --profile profiles/MODEL.json --snapshot home-01

After testing a build on the TV, copy .local/package.tar.gz to a working-package
file under .local/. Use --package PATH to reinstall it without rebuilding.
--compare DIR optionally compares demo frames, kept in .local/screens/update.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / ".local/package"
ARCHIVE = ROOT / ".local/package.tar.gz"


def pack(source, archive):
    """Pack a local build with root ownership and paths relative to the archive root."""
    def owned_by_root(info):
        info.uid = info.gid = 0
        info.uname = info.gname = ""
        return info

    with tarfile.open(archive, "w:gz") as package:
        package.add(source, arcname=".", filter=owned_by_root)


def check_package(archive, dll):
    """Reject incomplete packages and entries that could escape the staging directory."""
    with tarfile.open(archive, "r:gz") as package:
        files = {}
        for member in package.getmembers():
            path = PurePosixPath(member.name)
            if path.is_absolute() or ".." in path.parts or not (member.isdir() or member.isfile()):
                raise ValueError(f"Unsafe package entry: {member.name}")
            if path in files:
                raise ValueError(f"Duplicate package entry: {member.name}")
            files[path] = member
        for required in (dll, "quiet-home/apps.json"):
            member = files.get(PurePosixPath(required))
            if member is None or not member.isfile() or member.size == 0:
                raise ValueError(f"Package has no {required}; use the matching TV profile")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", required=True, help="The owner's TV IPv4 address")
    parser.add_argument("--sdb", help="Path to Samsung's separately installed SDB")
    parser.add_argument("--firmware", help="Software Version from About This TV, if the TV does not report it")
    parser.add_argument("--profile", type=Path, required=True, help="TV profile of the installed launcher")
    parser.add_argument("--snapshot", required=True, help="Snapshot Quiet Home was installed from")
    parser.add_argument("--package", type=Path, help="Install this saved package instead of building")
    parser.add_argument("--compare", type=Path, metavar="DIR", help="Optionally compare demo frames with this directory")
    args = parser.parse_args()
    frames = ROOT / ".local/screens/update"
    if args.compare and args.compare.resolve() == frames.resolve():
        parser.error("--compare must differ from .local/screens/update")
    dll = json.loads(args.profile.read_text(encoding="utf-8"))["launcher"]["dll"]
    archive = args.package or ARCHIVE
    if args.package is None:
        subprocess.run([sys.executable, str(ROOT / "tools/build.py"), "launcher", "--profile", str(args.profile)], check=True)
        pack(PACKAGE, archive)
    check_package(archive, dll)
    print(f"Package {archive}: {hashlib.sha256(archive.read_bytes()).hexdigest()}", flush=True)
    tv = [sys.executable, str(ROOT / "tools/tv.py"), "--host", args.host]
    if args.sdb:
        tv += ["--sdb", args.sdb]
    if args.firmware:
        tv += ["--firmware", args.firmware]
    subprocess.run([*tv, "push", str(archive), "package.tar.gz"], check=True)
    subprocess.run([*tv, "exec", "--script", str(ROOT / "recipes/update-launcher.sh"),
                    "--arg", "apply", "--arg", args.snapshot], check=True)
    if args.compare:
        subprocess.run([sys.executable, str(ROOT / "tools/screens.py"), *tv[2:],
                        "--profile", str(args.profile), "--out", str(frames), "--compare", str(args.compare)], check=True)


if __name__ == "__main__":
    main()
