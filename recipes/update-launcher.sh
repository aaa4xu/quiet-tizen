#!/bin/sh
# apply|recover SNAPSHOT_NAME. Stage package.tar.gz before apply.
# The only temporary copy is for recovering an interrupted file replacement.
# Original Samsung files and registry snapshots stay in the installation snapshot.
set -eu
test "$(id -u)" = 0
umask 077
test "$#" = 2 || { echo 'Usage: update-launcher.sh apply|recover SNAPSHOT' >&2; exit 1; }
action=$1; name=$2
case "$action" in apply|recover) ;; *) exit 1;; esac
case "$name" in ''|*[!A-Za-z0-9_-]*) exit 1;; esac
D=/opt/usr/share/quiet-tizen-backups/$name
T=$D/update-pending
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$D/launcher-container.txt"
read -r container dll < "$D/launcher-container.txt"
for value in "$container" "$dll"; do
    case "$value" in ''|.*|*[!A-Za-z0-9_.-]*) exit 1;; esac
done
if [ -n "${QUIET_LAUNCHER_APP:-}" ]; then
    test "$container $dll" = "$QUIET_LAUNCHER_APP ${QUIET_LAUNCHER_DLL:-}" || { echo 'Snapshot and profile name different containers' >&2; exit 1; }
fi
A=/opt/usr/apps/$container
label=User::Pkg::$container::RO
test ! -L "$T"
test ! -e "$A/bin/.native_image" || { echo 'A cached native image would shadow the DLL' >&2; exit 1; }

# Return a file digest or the sorted contents of a resource directory.
digest() { sha256sum "$1" | cut -d ' ' -f 1; }
tree() {
    test -d "$1" && test ! -L "$1" || return 1
    unusual=$(find "$1" ! -type f ! -type d -print) || return 1
    test -z "$unusual" || return 1
    (cd "$1" && contents=$(find . -type f -exec sha256sum {} +) && printf '%s\n' "$contents" | LC_ALL=C sort)
}
check_tree() {
    actual=$(tree "$1") || return 1
    expected=$(cat "$2") || return 1
    test "$actual" = "$expected"
}

# Finish a committed replacement, or return an interrupted one to its initial state.
# Every failure keeps the pending directory for inspection.
recover() {
    test -d "$T" || return 0
    if [ ! -e "$T/ready" ]; then
        sha256sum -c "$D/installed-home.sha256" || return 1
        rm -rf "$T"
        return
    fi
    test -s "$D/installed-home.sha256" && test -s "$T/old.sha256" && test -s "$T/new.sha256" || return 1
    recorded=$(cat "$D/installed-home.sha256") || return 1
    test "$recorded" = "$(cat "$T/old.sha256")" || test "$recorded" = "$(cat "$T/new.sha256")" || return 1
    if [ -e "$T/commit" ]; then
        sha256sum -c "$T/new.sha256" || return 1
        check_tree "$A/res/quiet-home" "$T/new.resources" || return 1
        cp "$T/new.sha256" "$T/committed.sha256" || return 1
        mv "$T/committed.sha256" "$D/installed-home.sha256" || return 1
        rm -f "$T/ready" || return 1
        rm -rf "$T"
        return
    fi
    test "$recorded" = "$(cat "$T/old.sha256")" || return 1
    old=$(cut -d ' ' -f 1 "$T/old.sha256")
    new=$(cut -d ' ' -f 1 "$T/new.sha256")
    test "$(digest "$T/old.dll")" = "$old" || return 1
    live=$(digest "$A/bin/$dll")
    test "$live" = "$old" || test "$live" = "$new" || return 1
    if [ -d "$T/old-resources" ]; then
        check_tree "$T/old-resources" "$T/old.resources" || return 1
        if [ -e "$A/res/quiet-home" ]; then
            check_tree "$A/res/quiet-home" "$T/new.resources" || return 1
        fi
    else
        check_tree "$A/res/quiet-home" "$T/old.resources" || return 1
    fi
    app_launcher -u 5001 -k "$container" || true
    cp -a "$T/old.dll" "$T/restoring.dll" || return 1
    chsmack -a "$label" "$T/restoring.dll" || return 1
    mv "$T/restoring.dll" "$A/bin/$dll" || return 1
    if [ -d "$T/old-resources" ]; then
        rm -rf "$A/res/quiet-home" || return 1
        mv "$T/old-resources" "$A/res/quiet-home" || return 1
    fi
    app_launcher -u 5001 -s "$container" || return 1
    rm -f "$T/ready" || return 1
    rm -rf "$T"
}

if [ -e "$T" ]; then recover; fi
if [ "$action" = recover ]; then echo 'No interrupted update remains'; exit 0; fi
sha256sum -c "$D/installed-home.sha256" || { echo 'Installed DLL changed outside Quiet Home; inspect it before updating' >&2; exit 1; }
test "$(stat -c %u:%g "$A/bin/$dll")" = 201:0
mode=$(cut -d ' ' -f 1 "$D/original-metadata.txt")
case "$mode" in [0-7][0-7][0-7]|[0-7][0-7][0-7][0-7]) ;; *) exit 1;; esac
test -s "$S/package.tar.gz"
mkdir "$T"
# A normal error restores the previous files. SIGKILL or a lost session is recovered
# by the next invocation, using the same fixed pending directory.
trap 'status=$?; trap - 0 1 2 15; if ! recover; then echo "Recovery stopped; inspect $T" >&2; fi; exit "$status"' 0
trap 'exit 1' 1 2 15
mkdir "$T/package"
tar -xzf "$S/package.tar.gz" -C "$T/package"
test -s "$T/package/$dll"
test -s "$T/package/quiet-home/apps.json"
chown 201:0 "$T/package/$dll"
chmod "$mode" "$T/package/$dll"
chsmack -a "$label" "$T/package/$dll"
find "$T/package/quiet-home" -type d -exec chmod 755 {} \;
find "$T/package/quiet-home" -type f -exec chmod 644 {} \;
find "$T/package/quiet-home" -exec chown 201:0 {} \;
find "$T/package/quiet-home" -exec chsmack -a "$label" {} \;
cp -a "$A/bin/$dll" "$T/old.dll"
cp "$D/installed-home.sha256" "$T/old.sha256"
printf '%s  %s\n' "$(digest "$T/package/$dll")" "$A/bin/$dll" > "$T/new.sha256"
tree "$A/res/quiet-home" > "$T/old.resources"
tree "$T/package/quiet-home" > "$T/new.resources"
sha256sum -c "$D/installed-home.sha256"
test "$(digest "$T/old.dll")" = "$(cut -d ' ' -f 1 "$T/old.sha256")"
touch "$T/ready"
app_launcher -u 5001 -k "$container" || true
mv "$A/res/quiet-home" "$T/old-resources"
mv "$T/package/quiet-home" "$A/res/quiet-home"
mv "$T/package/$dll" "$A/bin/$dll"
sha256sum -c "$T/new.sha256"
app_launcher -u 5001 -s "$container"
cp "$T/new.sha256" "$T/committed.sha256"
touch "$T/commit"
mv "$T/committed.sha256" "$D/installed-home.sha256"
rm -f "$T/ready"
rm -rf "$T"
rm -f "$S/package.tar.gz"
trap - 0 1 2 15
echo 'Quiet Home updated. Original Samsung recovery files are in the installation snapshot.'
