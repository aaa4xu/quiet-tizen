#!/bin/sh
# Capture the README screenshots: Quiet Home's demo mode, three pages in both themes.
# Usage: readme-screens.sh CONTAINER_APP_ID; needs Probe.dll in the staging directory.
set -eu
test "$(id -u)" = 0
app=$1
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$S/Probe.dll"
# The OLED screen saver starts after idle time without remote keys; launches do not count.
# Close it before each frame rather than turning the burn-in protection off.
saver=com.samsung.tv.glscreensaver-launcher
for theme in dark light; do
    for page in home apps settings; do
        if app_launcher -r "$saver" | grep -q 'result: running'; then
            app_launcher -u 5001 -k "$saver" >/dev/null
            echo "closed the screen saver before $page-$theme"
        fi
        app_launcher -u 5001 -s "$app" quiet_demo 1 quiet_theme "$theme" quiet_page "$page" >/dev/null
        sleep 1
        /usr/bin/dotnet "$S/Probe.dll" screenshot "/tmp/quiet-readme-$page-$theme.png" 2>/dev/null | grep '^screenshot='
        # build_ms and the page state of this frame; the next launch overwrites the file.
        cp /tmp/quiet-home-state.txt "/tmp/quiet-readme-$page-$theme.state"
    done
done
# A launch without quiet_demo returns to the owner's catalog, inputs and saved theme.
app_launcher -u 5001 -s "$app" >/dev/null
rm -f "$S/Probe.dll"
