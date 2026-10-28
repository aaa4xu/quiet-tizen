#!/bin/sh
# Replace an installed Quiet Home with a new local build, or roll one update back.
# apply|rollback SNAPSHOT_NAME UPDATE_NAME
# SNAPSHOT_NAME: the snapshot install-launcher.sh used. For apply, stage
# .local/package.tar.gz as quiet-tizen/package.tar.gz first.
# Samsung's original container in the snapshot is never touched here.
set -eu
test "$(id -u)" = 0
umask 077
action=${1:?apply or rollback}; name=${2:?Snapshot name required}; update=${3:?Update name required}
for value in "$name" "$update"; do
    case "$value" in ''|*[!A-Za-z0-9_-]*) exit 1;; esac
done
D=/opt/usr/share/quiet-tizen-backups/$name
U=$D/updates/$update
test -s "$D/launcher-container.txt" || { echo 'This snapshot has no container record; use the one Quiet Home was installed from' >&2; exit 1; }
read -r container dll < "$D/launcher-container.txt"
for value in "$container" "$dll"; do
    case "$value" in ''|.*|*[!A-Za-z0-9_.-]*) exit 1;; esac
done
if [ -n "${QUIET_LAUNCHER_APP:-}" ]; then
    test "$container $dll" = "$QUIET_LAUNCHER_APP ${QUIET_LAUNCHER_DLL:-}" || { echo "The snapshot backed up $container, not the profile's launcher" >&2; exit 1; }
fi
A=/opt/usr/apps/$container
label=User::Pkg::$container::RO
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$D/installed-home.sha256" || { echo 'Quiet Home was not installed from this snapshot' >&2; exit 1; }
test -d "$A/res/quiet-home" || { echo "Quiet Home is not installed in $container" >&2; exit 1; }
# Stop if anything else, for example a vendor app update, replaced the installed launcher.
sha256sum -c "$D/installed-home.sha256" || { echo "$dll is not the Quiet Home build this snapshot installed" >&2; exit 1; }
# install-launcher.sh moved the cached native image away; a new one would run instead of the DLL.
test ! -e "$A/bin/.native_image" || { echo "$A/bin/.native_image would shadow the new DLL" >&2; exit 1; }
mode=$(cut -d ' ' -f 1 "$D/original-metadata.txt")
case "$mode" in [0-7][0-7][0-7]|[0-7][0-7][0-7][0-7]) ;; *) exit 1;; esac
label_tree() {
    chown 201:0 "$A/bin/$dll"
    chmod "$mode" "$A/bin/$dll"
    chsmack -a "$label" "$A/bin/$dll"
    find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
    find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
    find "$A/res/quiet-home" -exec chown 201:0 {} \;
    find "$A/res/quiet-home" -exec chsmack -a "$label" {} \;
}
case "$action" in
apply)
    test ! -e "$U" || { echo "Update $update already exists; choose a new name" >&2; exit 1; }
    test -s "$S/package.tar.gz"
    test ! -e "$S/unpacked" || { echo "Remove $S/unpacked left by an earlier install first" >&2; exit 1; }
    mkdir "$S/unpacked"
    tar -xzf "$S/package.tar.gz" -C "$S/unpacked"
    test -s "$S/unpacked/$dll" || { echo "The package has no $dll; build it with this TV's profile" >&2; exit 1; }
    test -s "$S/unpacked/quiet-home/apps.json"
    mkdir -p "$U"
    cp -a "$A/bin/$dll" "$U/previous.dll"
    cp "$D/installed-home.sha256" "$U/previous-installed-home.sha256"
    app_launcher -u 5001 -k "$container" || true
    mv "$A/res/quiet-home" "$U/previous-quiet-home"
    cp -R "$S/unpacked/quiet-home" "$A/res/"
    cp "$S/unpacked/$dll" "$A/bin/$dll"
    label_tree
    sha256sum "$A/bin/$dll" > "$D/installed-home.sha256"
    cp "$D/installed-home.sha256" "$U/applied-installed-home.sha256"
    rm -rf "$S/unpacked"
    ;;
rollback)
    test -s "$U/previous.dll" || { echo "No update $update to roll back" >&2; exit 1; }
    test -d "$U/previous-quiet-home"
    test -s "$U/previous-installed-home.sha256"
    test ! -e "$U/rolled-back-quiet-home" || { echo "Update $update was already rolled back" >&2; exit 1; }
    # Only the latest update: rolling back an older one would drop the builds applied after it.
    test "$(cat "$U/applied-installed-home.sha256")" = "$(cat "$D/installed-home.sha256")" || { echo "Update $update is not the installed one; roll back the later updates first" >&2; exit 1; }
    app_launcher -u 5001 -k "$container" || true
    mv "$A/res/quiet-home" "$U/rolled-back-quiet-home"
    mv "$U/previous-quiet-home" "$A/res/quiet-home"
    cp "$U/previous.dll" "$A/bin/$dll"
    label_tree
    cp "$U/previous-installed-home.sha256" "$D/installed-home.sha256"
    sha256sum -c "$D/installed-home.sha256"
    ;;
*) echo "Unknown action $action" >&2; exit 1;;
esac
app_launcher -u 5001 -s "$container"
echo "Quiet Home $action complete ($update). The previous version is kept in $U"
