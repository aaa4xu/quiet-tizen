#!/bin/sh
# Run via tools/tv.py exec --script recipes/snapshot.sh --arg UNIQUE_PHASE_NAME
# tv.py passes the profile's launcher container and entry DLL. Until Quiet Home is
# installed there, the snapshot also keeps the original container for install and restore.
set -eu
test "$(id -u)" = 0
name=${1:?Give this snapshot a unique phase name}
container=${QUIET_LAUNCHER_APP:?Run through tools/tv.py, which passes the profile launcher}
dll=${QUIET_LAUNCHER_DLL:?Run through tools/tv.py, which passes the profile launcher}
case "$name" in ''|*[!A-Za-z0-9_-]*) echo 'Invalid snapshot name' >&2; exit 1;; esac
for value in "$container" "$dll"; do
    case "$value" in .*|*[!A-Za-z0-9_.-]*) echo 'Invalid container or DLL' >&2; exit 1;; esac
done
D=/opt/usr/share/quiet-tizen-backups/$name
A=/opt/usr/apps/$container
umask 077
test ! -e "$D" || { echo 'Snapshot already exists; never overwrite the original.' >&2; exit 1; }
# Back up Samsung's container, not an installed launcher; later snapshots skip it.
backup=
if [ ! -e "$A/res/quiet-home" ]; then
    test -s "$A/bin/$dll"
    backup=1
fi
mkdir -p "$D"
for db in .pkgmgr_parser .app-context .webappservice; do
    test -f "/opt/dbspace/$db.db"
    sqlite3 -readonly "/opt/dbspace/$db.db" ".backup '$D/$db.db'"
    test "$(sqlite3 -readonly "$D/$db.db" 'PRAGMA quick_check;')" = ok
done
for key in db/appfw/home_appid db/appfw/startup_appid \
    db/was/preload_by_country db/menu/autorun_tv_plus \
    db/menu/voice/voice_activated db/menu/voice/wakeupless_status \
    db/si_service/voice_recog_enable db/multi-assistant/prelaunch_mode; do
    printf '%s\n' "$key"
    vconftool get "$key"
done > "$D/preferences-before.txt"
if [ -n "$backup" ]; then
    original=$D/${container##*.}-original
    cp -a "$A" "$original"
    # install-launcher.sh checks the copy against this digest and restores the entry DLL's mode.
    sha256sum "$original/bin/$dll" > "$D/original-assembly.sha256"
    test "$(sha256sum "$A/bin/$dll" | cut -d ' ' -f 1)" = "$(cut -d ' ' -f 1 "$D/original-assembly.sha256")"
    stat -c '%a %u:%g %s' "$A/bin/$dll" > "$D/original-metadata.txt"
    # One archive to copy into .local/ with tv pull.
    tar -czf "$original.tar.gz" -C "$D" "${original##*/}"
    # install-launcher.sh and restore-launcher.sh read the container and DLL from here.
    printf '%s %s\n' "$container" "$dll" > "$D/launcher-container.txt"
    echo "Original container: $original (archive $original.tar.gz)"
else
    echo "Quiet Home is already installed in $container; this snapshot keeps no container copy."
fi
echo "Consistent snapshots: $D"
echo 'Record the main-operation home app with Probe.dll before changing launcher routing.'
