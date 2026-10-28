#!/usr/bin/env python3
"""Explicit-target access for TVs with a documented profile. See docs/02-access.md."""
import argparse
import asyncio
import base64
import gzip
import hashlib
import ipaddress
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import sys
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "vendor"))
from samsung_tv_root.archive_root import probe, _check_target, _capture
from samsung_tv_root.sdb import SdbClient, find_sdb

PROFILES = [json.loads(path.read_text(encoding="utf-8")) for path in sorted((ROOT / "profiles").glob("*.json"))]
STAGE = "/home/owner/share/tmp/sdk_tools/quiet-tizen"
AGENT_COMMAND_LIMIT = 64 * 1024  # MaximumCommandBytes in the TV-side root agent
# The four private references tools/build.py compiles Quiet Home against.
FRAMEWORK = ("Tizen.NUI.dll", "Tizen.Applications.Common.dll", "Tizen.System.dll", "Tizen.dll")
FRAMEWORK_DIR = "/usr/share/dotnet.tizen/framework"


def private_path(path):
    path = Path(path).expanduser().resolve()
    private = (ROOT / ".local").resolve()
    if path == private or private not in path.parents:
        raise ValueError("Private files must be inside this checkout's .local/ directory.")
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    return path


def save_private(path, data):
    path = private_path(path)
    path.write_bytes(data)
    path.chmod(0o600)


def observed_model(client):
    client.connect()
    client.require_device()
    rows = [line.split() for line in client.run(("devices",), check=True).stdout.splitlines()]
    rows = [row for row in rows if row and row[0] == client.serial]
    if len(rows) != 1 or len(rows[0]) < 3:
        raise ValueError("Cannot identify the explicitly selected device from SDB.")
    return rows[0][-1]


def parse_firmware(prd_info):
    match = re.search(r"^Version=([^;\s]+);?\s*$", prd_info, re.MULTILINE)
    return match.group(1) if match else None


def observed_firmware(client, callback_host=None):
    # The same string About This TV shows as Software Version; readable without root.
    # A truncated reply parses as "not detected", which the identity gate refuses.
    result = client.capture("cat /etc/prd_info.ini 2>/dev/null || true",
                            callback_host=callback_host, timeout=15)
    return parse_firmware(result.output)


def check_model(model):
    if not any(profile["model"] == model for profile in PROFILES):
        raise ValueError(f"Unsupported model {model!r}; stop and investigate a separate profile.")


def check_identity(model, firmware):
    check_model(model)
    if firmware is None:
        raise ValueError("Firmware not detected; supply --firmware with the exact Software Version "
                         "from About This TV. Do not guess this value.")
    if firmware not in [profile["firmware"] for profile in PROFILES if profile["model"] == model]:
        raise ValueError(f"Unsupported firmware {firmware!r}; only a documented build is accepted.")
    return next(profile for profile in PROFILES if (profile["model"], profile["firmware"]) == (model, firmware))


def launcher_environment(profile):
    # Recipes read Quiet Home's host container from these instead of positional arguments.
    launcher = profile["launcher"]
    environment = {"QUIET_LAUNCHER_APP": launcher["app_id"], "QUIET_LAUNCHER_DLL": launcher["dll"],
                   "QUIET_SETTINGS_ROUTE": launcher.get("settings_route", "launcher")}
    for value in environment.values():
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*", value):
            raise ValueError(f"Invalid launcher value {value!r} in the profile.")
    return environment


def script_command(script, arguments, environment=None):
    # Base64 keeps the script intact through the command channel; decode it before sh reads it.
    encoded = base64.b64encode(script).decode()
    assignments = "".join(f"{name}={shlex.quote(value)} " for name, value in (environment or {}).items())
    return (f"printf '%s' {shlex.quote(encoded)} | base64 -d | {assignments}/bin/sh -s -- "
            + shlex.join(arguments))


def literal_command(text):
    # The TV-side agent wraps the text in double quotes for sh -c and doubles every
    # backslash, so a backslash typed here would reach the shell twice. Base64 keeps
    # the wrapper free of backslashes; the decoded text is passed to a fresh sh -c,
    # which then applies normal quoting, expansion and exit-status semantics.
    encoded = base64.b64encode(text.encode("utf-8")).decode("ascii")
    return f'/bin/sh -c "$(printf %s {encoded} | base64 -d)"'


def pull_command(remote):
    if not remote.startswith("/") or "\0" in remote or "\n" in remote:
        raise ValueError("Use an absolute, single-line TV file path.")
    # The upstream command channel captures at most 1 MiB per stream.
    # A compressed transfer is bounded before emitting any base64.
    return f"""set -eu
test -f {shlex.quote(remote)}
test "$(stat -c %s {shlex.quote(remote)})" -le 33554432
t=$(mktemp /run/quiet-tizen-pull.XXXXXX)
trap 'rm -f "$t"' EXIT HUP INT TERM
gzip -c {shlex.quote(remote)} > "$t"
test "$(wc -c < "$t")" -le 760000 || {{ echo 'File too large; split a snapshot into smaller files first.' >&2; exit 1; }}
sha256sum {shlex.quote(remote)} | cut -d ' ' -f 1
base64 "$t"
"""


def framework_pull_plan(source, destination):
    """Pair each reference's TV path with its private copy; never replace an earlier copy."""
    if not source.startswith("/") or "\0" in source or "\n" in source:
        raise ValueError("Use an absolute, single-line TV directory.")
    plan = [(str(PurePosixPath(source) / name), private_path(Path(destination) / name)) for name in FRAMEWORK]
    existing = [str(local) for _, local in plan if local.exists()]
    if existing:
        raise ValueError(f"Already pulled: {', '.join(existing)}. Inspect and move them aside first.")
    return plan


def decode_pull(stdout):
    digest, encoded = stdout.split("\n", 1)
    if not re.fullmatch(r"[0-9a-f]{64}", digest):
        raise ValueError("Missing transfer digest.")
    compressed = base64.b64decode("".join(encoded.split()), validate=True)
    data = gzip.decompress(compressed)
    if len(data) > 33554432 or hashlib.sha256(data).hexdigest() != digest:
        raise ValueError("Transfer size or digest mismatch; file was not saved.")
    return data


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", required=True, help="The owner's TV IPv4 address; never auto-discovered")
    parser.add_argument("--firmware", help="Software Version from About This TV; "
                        "overrides the version the TV reports")
    parser.add_argument("--sdb", help="Path to Samsung's separately installed SDB")
    parser.add_argument("--callback-host", help="This computer's LAN IPv4 if route detection is wrong")
    sub = parser.add_subparsers(dest="action", required=True)
    sub.add_parser("doctor", help="Read SDB identity and archive-route capabilities; does not obtain root")
    sub.add_parser("root-check", help="Temporarily obtain root and run id; no configuration changes")
    run = sub.add_parser("exec", help="Execute one reviewed command or short script using temporary root")
    source = run.add_mutually_exclusive_group(required=True)
    source.add_argument("--command")
    source.add_argument("--script", type=Path)
    run.add_argument("--arg", action="append", default=[], help="Positional argument passed to --script")
    pull = sub.add_parser("pull", help="Read and verify one bounded file into ignored .local/")
    pull.add_argument("remote")
    pull.add_argument("local", type=Path)
    framework = sub.add_parser("framework-pull", help="Read and verify the four Tizen reference "
                               "assemblies the launcher build needs into ignored .local/")
    framework.add_argument("--from", dest="source", default=FRAMEWORK_DIR,
                           help="TV directory holding all four; check it with find first")
    framework.add_argument("--to", dest="destination", type=Path, default=ROOT / ".local/tv-framework",
                           help="Private directory inside .local/ for the copies")
    push = sub.add_parser("push", help="Upload one file to the SDK staging directory, not to system paths")
    push.add_argument("local", type=Path)
    push.add_argument("name", help="Simple staging filename")
    args = parser.parse_args()
    ipaddress.IPv4Address(args.host)
    if args.callback_host:
        ipaddress.IPv4Address(args.callback_host)
    if args.sdb:
        os.environ["SDB"] = str(Path(args.sdb).expanduser().resolve())
    client = SdbClient(find_sdb(), args.host, timeout=15)
    model = observed_model(client)
    print("SDB model:", model)
    if args.action != "doctor":
        # Reading the firmware already runs a command on the TV; refuse other models first.
        check_model(model)
    firmware = args.firmware or observed_firmware(client, args.callback_host)
    print("Firmware:", firmware or "not detected; must be read on the TV.")
    if args.action == "doctor":
        assessment = _check_target(client)
        print("Archive-route runtime family:", assessment.runtime)
        print(assessment.capabilities)
        print(_capture(client, "uname -srmo; readlink -f /usr/share/dotnet.tizen/netcoreapp"))
        print("Preflight only; root is not yet confirmed.")
        return
    profile = check_identity(model, firmware)
    if args.action == "push":
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,100}", args.name):
            raise ValueError("Use a simple staging filename without directory components.")
        if not args.local.is_file():
            raise ValueError("Upload source must be a file.")
        _capture(client, f"mkdir -p {STAGE}")
        remote = PurePosixPath(STAGE) / args.name
        client.push(args.local.resolve(), remote)
        digest = hashlib.sha256(args.local.read_bytes()).hexdigest()
        actual = _capture(client, f"sha256sum {shlex.quote(str(remote))}").split()[0]
        if digest != actual:
            raise ValueError("Uploaded file digest mismatch.")
        print(f"Verified staging file: {remote}. Remove it after deployment.")
        return
    if args.action == "root-check":
        commands = ("id",)
    elif args.action == "pull":
        private_path(args.local)
        commands = (pull_command(args.remote),)
    elif args.action == "framework-pull":
        plan = framework_pull_plan(args.source, args.destination)
        commands = tuple(pull_command(remote) for remote, _ in plan)
    elif args.script:
        script = args.script.read_bytes()
        if len(script) > 45000:
            raise ValueError("Script too large for the bounded command channel; split into phases.")
        if b"\r\n" in script:
            # The TV runs exactly the reviewed bytes, so convert the file rather than the upload.
            line = script[:script.index(b"\r\n")].count(b"\n") + 1
            raise ValueError(
                f"{args.script} has Windows (CRLF) line endings, first on line {line}. "
                "The TV's /bin/sh would keep a carriage return at the end of every line, so "
                "commands, paths and heredoc terminators would not match. Convert the file to LF "
                "line endings (set the editor's line endings to LF, or run dos2unix on it), "
                "review it and run it again. Files under recipes/ are checked out with LF "
                "by .gitattributes; re-checkout them if they were cloned before that rule.")
        commands = (script_command(script, args.arg, launcher_environment(profile)),)
    else:
        if args.arg:
            raise ValueError("--arg requires --script.")
        commands = (literal_command(args.command),)
        if len(commands[0].encode("utf-8")) > AGENT_COMMAND_LIMIT:
            raise ValueError("--command is too large for the root agent after encoding; "
                             "use --script.")
    result = asyncio.run(probe(
        args.host, model=model, callback_host=args.callback_host,
        payloads=ROOT / ".local/build/payloads", commands=commands, command_timeout=30,
    ))
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
    log = ROOT / ".local/sessions" / f"{stamp}.json"
    save_private(log, (json.dumps(result, indent=2) + "\n").encode())
    if result.get("uid") != 0 or result.get("euid") != 0:
        raise ValueError("The callback did not prove UID/EUID 0.")
    if not result.get("system_files_unchanged") or not result.get("staging_cleaned"):
        raise ValueError("Root session cleanup checks failed; inspect the private session log.")
    if len(result.get("commands", [])) != len(commands):
        raise RuntimeError(f"Not every command ran. Private log: {log.relative_to(ROOT)}")
    for number, response in enumerate(result["commands"], 1):
        if response["stderr"]:
            print(response["stderr"], file=sys.stderr, end="")
        if response["exit_code"] or response["timed_out"]:
            label = "Command" if len(commands) == 1 else f"Command {number} of {len(commands)}"
            raise RuntimeError(f"{label} failed ({response['exit_code']}, timeout={response['timed_out']}). "
                               f"Private log: {log.relative_to(ROOT)}")
    response = result["commands"][0]
    if args.action == "framework-pull":
        # Verify every transfer before writing any, so a failure leaves no partial set.
        files = [(local, decode_pull(response["stdout"]))
                 for (_, local), response in zip(plan, result["commands"])]
        for local, data in files:
            save_private(local, data)
        print(f"Verified {len(files)} references saved in {args.destination}.")
    elif args.action == "pull":
        save_private(args.local, decode_pull(response["stdout"]))
        print("Verified file saved inside .local/.")
    else:
        print(response["stdout"], end="")
    print("UID/EUID 0 verified; root staging cleaned; passwd and SDB key hashes unchanged.")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit(130)
    except Exception as error:
        print(f"Stopped: {error}", file=sys.stderr)
        sys.exit(1)
