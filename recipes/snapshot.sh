#!/bin/sh
# Run via tools/tv.py exec --script recipes/snapshot.sh --arg UNIQUE_PHASE_NAME
# Before installing Quiet Home, also pass the profile's launcher CONTAINER_APP_ID and
# ENTRY_DLL: the snapshot then keeps the original container for install and restore.
set -eu
test "$(id -u)" = 0
name=${1:?Give this snapshot a unique phase name}
container=${2:-}
case "$name" in ''|*[!A-Za-z0-9_-]*) echo 'Invalid snapshot name' >&2; exit 1;; esac
D=/opt/usr/share/quiet-tizen-backups/$name
umask 077
test ! -e "$D" || { echo 'Snapshot already exists; never overwrite the original.' >&2; exit 1; }
if [ -n "$container" ]; then
    dll=${3:?Entry DLL required with the container}
    for value in "$container" "$dll"; do
        case "$value" in *[!A-Za-z0-9_.-]*) echo 'Invalid container or DLL' >&2; exit 1;; esac
    done
    A=/opt/usr/apps/$container
    test -s "$A/bin/$dll"
    test ! -e "$A/res/quiet-home" # Back up Samsung's container, not an installed launcher.
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
if [ -n "$container" ]; then
    original=$D/${container##*.}-original
    cp -a "$A" "$original"
    # install-launcher.sh checks the copy against this digest and restores the entry DLL's mode.
    sha256sum "$original/bin/$dll" > "$D/original-assembly.sha256"
    test "$(sha256sum "$A/bin/$dll" | cut -d ' ' -f 1)" = "$(cut -d ' ' -f 1 "$D/original-assembly.sha256")"
    stat -c '%a %u:%g %s' "$A/bin/$dll" > "$D/original-metadata.txt"
    # One archive to copy into .local/ with tv pull.
    tar -czf "$original.tar.gz" -C "$D" "${original##*/}"
    echo "Original container: $original (archive $original.tar.gz)"
fi
echo "Consistent snapshots: $D"
echo 'Record the main-operation home app with Probe.dll before changing launcher routing.'
