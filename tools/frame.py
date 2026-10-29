#!/usr/bin/env python3
"""Capture what the owner's TV shows, UI and picture together, as one PNG.

Probe's screenshot holds only the UI layer: an HDMI or TV picture comes out
black. This pushes Frame.dll (tools/build.py launcher builds it), which also
captures the video plane with the call the WLED ambilight experiments used,
and lays the UI over the picture where the UI is see-through. The picture is
small (720x576 on the QE65S95F) and scaled up, so it is blurrier than the UI.

  python3 tools/frame.py --host 10.0.0.5 --sdb PATH/TO/sdb --out .local/frames/now.png

Next to the output it keeps both layers (-ui.png, -video.png) and Quiet Home's
diagnostics file at that moment (.state). Keep the images in .local/: they can
show account names, app lists and whatever is playing.
"""
import argparse
from pathlib import Path
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[1]
STAGING = "/home/owner/share/tmp/sdk_tools/quiet-tizen"
REMOTE = "/tmp/quiet-frame"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--host", required=True, help="The owner's TV IPv4 address")
    parser.add_argument("--sdb", help="Path to Samsung's separately installed SDB")
    parser.add_argument("--firmware", help="Software Version from About This TV, if the TV does not report it")
    parser.add_argument("--out", type=Path, default=ROOT / ".local/frames/frame.png", help="PNG to write")
    args = parser.parse_args()
    frame = ROOT / ".local/build/Frame/Frame.dll"
    if not frame.is_file():
        parser.error("Build Frame first: python3 tools/build.py launcher ...")
    tv = [sys.executable, str(ROOT / "tools/tv.py"), "--host", args.host]
    if args.sdb:
        tv += ["--sdb", args.sdb]
    if args.firmware:
        tv += ["--firmware", args.firmware]

    def run(*command):
        subprocess.run([*tv, *command], check=True)

    out = args.out
    out.parent.mkdir(parents=True, exist_ok=True)
    layers = {
        "ui.png": out.with_name(out.stem + "-ui.png"),
        "video.png": out.with_name(out.stem + "-video.png"),
        "state": out.with_suffix(".state"),
    }
    archive = out.with_suffix(".tar")
    run("push", str(frame), "Frame.dll")
    try:
        # One root session takes both layers a moment apart; the state file is empty without Quiet Home.
        # Each SDB session takes seconds, so the three files come back as one archive.
        run("exec", "--command",
            f"rm -rf {REMOTE} {REMOTE}.tar && mkdir {REMOTE} && /usr/bin/dotnet {STAGING}/Frame.dll {REMOTE} 2>/dev/null"
            f" | grep -E '^(ui|video)='; cp /tmp/quiet-home-state.txt {REMOTE}/state 2>/dev/null || : > {REMOTE}/state;"
            f" cd {REMOTE} && tar -cf {REMOTE}.tar $(ls {' '.join(layers)} 2>/dev/null)")
        run("pull", f"{REMOTE}.tar", str(archive))
    finally:
        run("exec", "--command", f"rm -rf {REMOTE} {REMOTE}.tar {STAGING}/Frame.dll")
    with tarfile.open(archive) as files:
        names = files.getnames()
        for name, local in layers.items():
            local.unlink(missing_ok=True)
            if name in names:
                local.write_bytes(files.extractfile(name).read())
    archive.unlink()
    if not layers["ui.png"].exists():
        sys.exit("The TV returned no UI layer.")

    # Only the composition needs Pillow.
    from PIL import Image, ImageStat
    ui = Image.open(layers["ui.png"]).convert("RGBA")
    # The video call fails (-1, -4) while the input is HDCP 2.x protected, as the PlayStation 5's
    # 4K picture was; the UI then lies over black, and its see-through parts show as black.
    if layers["video.png"].exists():
        video = Image.open(layers["video.png"]).convert("RGB")
    else:
        print("The TV returned no video layer; the picture under the UI is black.")
        video = Image.new("RGB", ui.size)
    picture = Image.alpha_composite(video.resize(ui.size, Image.BILINEAR).convert("RGBA"), ui).convert("RGB")
    picture.save(out)
    print(f"{out}: UI {ui.width}x{ui.height} over video {video.width}x{video.height}")
    # A sleeping screen captures as an almost black frame.
    if sum(ImageStat.Stat(picture.convert("L")).mean) < 8:
        print("The frame is almost black: the screen may be off or showing nothing.")


if __name__ == "__main__":
    main()
