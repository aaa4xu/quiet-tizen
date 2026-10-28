#!/usr/bin/env python3
"""Compare demo-mode screenshots with a reference run of another build.

Demo mode draws the same pixels on every run of one build, so frames match
only when the PNG files are byte-identical. Each frame's state file gives
build_ms and the page state; changes in the state are listed, but only the
frames decide the exit code. Pillow is optional: with it, a differing frame
also shows how many pixels changed and where.

  python3 tools/screen_compare.py REFERENCE_DIR CURRENT_DIR
"""
import argparse
import hashlib
from pathlib import Path
import sys

PAGES = ("home", "apps", "settings")
THEMES = ("dark", "light")
# Frames kept only for comparing builds, not README images: "toolbar" is Home with its first toolbar button focused.
SHOTS = PAGES + ("toolbar",)
FRAMES = tuple(f"{shot}-{theme}" for theme in THEMES for shot in SHOTS)
# These differ between two runs of one build: the process, timing and artwork that loads asynchronously.
VOLATILE = {"process_id", "build_ms", "activity", "artwork_ready", "artwork_pending"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None


def parse_state(path):
    """Key/value lines of /tmp/quiet-home-state.txt; the first line is a timestamp."""
    if not path.is_file():
        return {}
    return dict(line.split("=", 1) for line in path.read_text(encoding="utf-8").splitlines() if "=" in line)


def pixel_difference(reference, current):
    """How many pixels differ and their bounding box, or None without Pillow."""
    try:
        from PIL import Image, ImageChops
    except ImportError:
        return None
    try:
        with Image.open(reference) as a, Image.open(current) as b:
            if a.size != b.size:
                return f"size {a.size[0]}x{a.size[1]} -> {b.size[0]}x{b.size[1]}"
            mask = ImageChops.difference(a.convert("RGB"), b.convert("RGB")).convert("L").point(lambda v: 255 if v else 0)
    except OSError as error:
        # A pull that stopped early leaves a truncated file.
        return f"unreadable image ({error})"
    box = mask.getbbox()
    if box is None:
        return "same pixels, different encoding"
    return f"{mask.histogram()[255]} px in ({box[0]}, {box[1]})-({box[2]}, {box[3]})"


def compare(reference, current, pixels=pixel_difference):
    """Return the report lines and whether every frame matched."""
    lines, matched = [], True
    width = max(map(len, FRAMES))
    for frame in FRAMES:
        before, after = reference / f"{frame}.png", current / f"{frame}.png"
        old, new = digest(before), digest(after)
        if old is None or new is None:
            verdict = "missing in " + ("reference" if old is None else "current")
        elif old == new:
            verdict = "same"
        else:
            detail = pixels(before, after) if pixels else None
            verdict = "DIFFERENT" + (f": {detail}" if detail else "")
        matched = matched and verdict == "same"
        old_state, new_state = parse_state(reference / f"{frame}.state"), parse_state(current / f"{frame}.state")
        timing = f"build_ms {old_state.get('build_ms', '-')} -> {new_state.get('build_ms', '-')}"
        lines.append(f"{frame:<{width}}  {timing:<24}  {verdict}")
        if old_state and new_state:
            for key in sorted((old_state.keys() | new_state.keys()) - VOLATILE):
                if old_state.get(key) != new_state.get(key):
                    lines.append(f"{'':<{width}}  state {key}: {old_state.get(key, '-')} -> {new_state.get(key, '-')}")
    return lines, matched


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("reference", type=Path, help="Frames of the build to compare against")
    parser.add_argument("current", type=Path, help="Frames of the new build")
    args = parser.parse_args(argv)
    lines, matched = compare(args.reference, args.current)
    print("\n".join(lines))
    print("All frames match." if matched else "Frames differ.")
    return 0 if matched else 1


if __name__ == "__main__":
    sys.exit(main())
