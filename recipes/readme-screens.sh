#!/bin/sh
# Capture the README screenshots in one theme: Quiet Home's demo mode on its three pages,
# plus a "toolbar" frame of Home with its first toolbar button focused, for comparing builds.
# One theme per run keeps a run within the 30-second command budget.
# Usage: readme-screens.sh CONTAINER_APP_ID dark|light; needs Probe.dll in the staging
# directory, which tools/screens.py removes after the last run.
set -eu
test "$(id -u)" = 0
app=$1
theme=$2
case "$theme" in dark|light) ;; *) echo "theme must be dark or light" >&2; exit 2 ;; esac
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$S/Probe.dll"
# The OLED screen saver starts after idle time without remote keys; launches do not count.
# Close it before each frame rather than turning the burn-in protection off.
saver=com.samsung.tv.glscreensaver-launcher
for shot in home apps settings toolbar; do
    if app_launcher -r "$saver" | grep -q 'result: running'; then
        app_launcher -u 5001 -k "$saver" >/dev/null
        echo "closed the screen saver before $shot-$theme"
    fi
    if [ "$shot" = toolbar ]; then
        app_launcher -u 5001 -s "$app" quiet_demo 1 quiet_theme "$theme" quiet_page home quiet_focus toolbar >/dev/null
    else
        app_launcher -u 5001 -s "$app" quiet_demo 1 quiet_theme "$theme" quiet_page "$shot" >/dev/null
    fi
    sleep 1
    /usr/bin/dotnet "$S/Probe.dll" screenshot "/tmp/quiet-readme-$shot-$theme.png" 2>/dev/null | grep '^screenshot='
    # build_ms and the page state of this frame; the next launch overwrites the file.
    cp /tmp/quiet-home-state.txt "/tmp/quiet-readme-$shot-$theme.state"
done
# A launch without quiet_demo returns to the owner's catalog, inputs and saved theme.
app_launcher -u 5001 -s "$app" >/dev/null
