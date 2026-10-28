#!/bin/sh
# Install only. Home/startup routing is changed AFTER manual launch is verified.
# SNAPSHOT_NAME: a snapshot.sh run that kept the original container; stage
# .local/package.tar.gz as quiet-tizen/package.tar.gz first.
set -eu
test "$(id -u)" = 0
name=${1:?Snapshot name required}
case "$name" in ''|*[!A-Za-z0-9_-]*) exit 1;; esac
D=/opt/usr/share/quiet-tizen-backups/$name
test -s "$D/launcher-container.txt" || { echo 'No container backup in this snapshot; take a new one before installing' >&2; exit 1; }
read -r container dll < "$D/launcher-container.txt"
for value in "$container" "$dll"; do
    case "$value" in ''|.*|*[!A-Za-z0-9_.-]*) exit 1;; esac
done
# tv.py passes the current profile's launcher; the snapshot must be of that same container.
if [ -n "${QUIET_LAUNCHER_APP:-}" ]; then
    test "$container $dll" = "$QUIET_LAUNCHER_APP ${QUIET_LAUNCHER_DLL:-}" || { echo "The snapshot backed up $container, not the profile's launcher" >&2; exit 1; }
fi
A=/opt/usr/apps/$container
original=$D/${container##*.}-original
label=User::Pkg::$container::RO
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$D/.pkgmgr_parser.db"
test -s "$S/package.tar.gz"
test -s "$A/bin/$dll"
test "$(stat -c %u:%g "$A/bin/$dll")" = 201:0
# snapshot.sh saved the original container; it must still match the installed DLL.
test -s "$original/bin/$dll"
test -s "$original.tar.gz"
sha256sum -c "$D/original-assembly.sha256"
test "$(sha256sum "$A/bin/$dll" | cut -d ' ' -f 1)" = "$(cut -d ' ' -f 1 "$D/original-assembly.sha256")" || { echo "$dll is not the original the snapshot saved" >&2; exit 1; }
mode=$(cut -d ' ' -f 1 "$D/original-metadata.txt")
case "$mode" in [0-7][0-7][0-7]|[0-7][0-7][0-7][0-7]) ;; *) exit 1;; esac
# First install only; do not silently overwrite an existing custom launcher.
test ! -e "$A/res/quiet-home" || { echo 'Quiet Home is already installed in this container' >&2; exit 1; }
# One install per snapshot: a second one would overwrite its records and nest the native image.
test ! -e "$D/installed-home.sha256" || { echo 'This snapshot was already used for an install; take a new one' >&2; exit 1; }
test ! -e "$S/unpacked" || { echo "Remove $S/unpacked left by an earlier install first" >&2; exit 1; }
# One registry row for the container, live and in the snapshot, before touching the package.
for db in /opt/dbspace/.pkgmgr_parser.db "$D/.pkgmgr_parser.db"; do
    test "$(sqlite3 -readonly "$db" "SELECT count(*) FROM package_app_info WHERE app_id='$container';")" = 1
done
# The archive is generated from our local build, not downloaded from an unknown party.
mkdir "$S/unpacked"
tar -xzf "$S/package.tar.gz" -C "$S/unpacked"
test -s "$S/unpacked/$dll"
test -s "$S/unpacked/Probe.dll"
test -s "$S/unpacked/quiet-home/apps.json"
cp "$S/unpacked/Probe.dll" "$D/Probe.dll"
/usr/bin/dotnet "$D/Probe.dll" > "$D/operation-and-sources-before.txt"
app_launcher -u 5001 -k "$container" || true
if [ -d "$A/bin/.native_image" ]; then mv "$A/bin/.native_image" "$D/home-native-image"; fi
cp "$S/unpacked/$dll" "$A/bin/$dll"
cp -R "$S/unpacked/quiet-home" "$A/res/"
chown 201:0 "$A/bin/$dll"
chmod "$mode" "$A/bin/$dll"
chsmack -a "$label" "$A/bin/$dll"
sha256sum "$A/bin/$dll" > "$D/installed-home.sha256"
find "$A/res/quiet-home" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home" -exec chown 201:0 {} \;
find "$A/res/quiet-home" -exec chsmack -a "$label" {} \;
sqlite3 -bail /opt/dbspace/.pkgmgr_parser.db <<SQL
BEGIN IMMEDIATE;
CREATE TEMP TABLE guard(ok INTEGER CHECK(ok=1));
INSERT INTO guard SELECT count(*)=1 FROM package_app_info WHERE app_id='$container';
UPDATE package_app_info SET app_disable='false',app_onboot='false',app_autorestart='false' WHERE app_id='$container';
DROP TABLE guard;
COMMIT;
SQL
app_launcher -u 5001 -s "$container"
echo 'Installed and launch requested. Verify the visible UI, remote, sound, apps and HDMI before changing Home routing.'
echo "Original package and operation/source record: $D"
