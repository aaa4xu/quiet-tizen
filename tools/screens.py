#!/usr/bin/env python3
"""Refresh the README screenshots from the owner's TV.

Quiet Home and Probe must already be built (tools/build.py launcher) and
installed. Quiet Home's demo mode shows the public catalog, fixed inputs,
settings and clock, so the images carry no personal data and only change
when the interface does. Review the images before committing them.

  python3 tools/screens.py --host 10.0.0.5 --sdb PATH/TO/sdb
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

from PIL import Image, ImageStat

ROOT = Path(__file__).resolve().parents[1]
PAGES = ("home", "apps", "settings")
THEMES = ("dark", "light")
WIDTH = 1280


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", required=True, help="The owner's TV IPv4 address")
    parser.add_argument("--sdb", help="Path to Samsung's separately installed SDB")
    parser.add_argument("--firmware", help="Software Version from About This TV, if the TV does not report it")
    parser.add_argument("--profile", type=Path, default=ROOT / "profiles/un75u8000ffxza-1300.9.json",
                        help="TV profile whose launcher container holds Quiet Home")
    args = parser.parse_args()
    app_id = json.loads(args.profile.read_text(encoding="utf-8"))["launcher"]["app_id"]
    probe = ROOT / ".local/build/Probe/Probe.dll"
    if not probe.is_file():
        parser.error("Build Probe first: python3 tools/build.py launcher ...")
    tv = [sys.executable, str(ROOT / "tools/tv.py"), "--host", args.host]
    if args.sdb:
        tv += ["--sdb", args.sdb]
    if args.firmware:
        tv += ["--firmware", args.firmware]

    def run(*command):
        subprocess.run([*tv, *command], check=True)

    raw = ROOT / ".local/screens/readme"
    remote = [f"/tmp/quiet-readme-{page}-{theme}.png" for theme in THEMES for page in PAGES]
    run("push", str(probe), "Probe.dll")
    try:
        run("exec", "--script", str(ROOT / "recipes/readme-screens.sh"), "--arg", app_id)
        for path in remote:
            run("pull", path, str(raw / Path(path).name))
    finally:
        # The recipe removes Probe.dll itself; this also covers a run that stopped early.
        run("exec", "--command", "rm -f /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll " + " ".join(remote))
    images = {}
    for theme in THEMES:
        for page in PAGES:
            image = Image.open(raw / f"quiet-readme-{page}-{theme}.png").convert("RGB")
            # A sleeping screen or the screensaver captures as an almost black frame.
            if sum(ImageStat.Stat(image.convert("L")).mean) < 8:
                sys.exit(f"{page}-{theme} came out black; wake the TV and run again. docs/screens/ was not changed.")
            images[page, theme] = image
    out = ROOT / "docs/screens"
    out.mkdir(exist_ok=True)
    for (page, theme), image in images.items():
        image = image.resize((WIDTH, round(image.height * WIDTH / image.width)), Image.LANCZOS)
        # Lossy WebP at q90 looks the same as PNG here at a fifth of the size; a palette PNG bands.
        target = out / f"{page}-{theme}.webp"
        image.save(target, quality=90, method=6)
        print(f"{target.relative_to(ROOT)}: {target.stat().st_size // 1024} KiB")
    print("Review docs/screens/ before committing.")


if __name__ == "__main__":
    main()
