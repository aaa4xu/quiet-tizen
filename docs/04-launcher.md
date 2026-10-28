# Build and install Quiet Home

Quiet Home is a native NUI/.NET application, with Home/startup and remote-key
routing adjusted to make it act as the launcher. It does not replace the
kernel or the whole Tizen shell. Samsung's settings, app framework and media
stack remain available underneath.

The design follows Free My TV: input cards, colorful tiles, rounded controls
and a translucent dock.
All UI text here is English. See [attribution](../NOTICE.md).

## Obtain matching private framework references

Build against the TV's own compatible `Tizen.NUI.dll`,
`Tizen.Applications.Common.dll`, `Tizen.System.dll` and `Tizen.dll`.
The shipped project files use `Private=false`, so those references are not
copied into the app package.

Find the paths on the target:

```sh
tv exec --command 'find /usr/share/dotnet.tizen -name Tizen.NUI.dll -o -name Tizen.Applications.Common.dll -o -name Tizen.System.dll -o -name Tizen.dll'
```

Inspect the results. When all four runtime assemblies are in one directory
(`/usr/share/dotnet.tizen/framework` by default), pull them in one root
session:

```sh
tv framework-pull --from /usr/share/dotnet.tizen/framework
```

It verifies each file's SHA-256 against the TV, writes nothing unless all
four transfers verify, and refuses to replace a copy already in
`.local/tv-framework/`. If the assemblies are spread over several
directories, pull them individually with
`tv pull REMOTE_PATH .local/tv-framework/NAME.dll`. Do not use a random
desktop/mobile SDK version just because it has a file with the same name.
Do not commit the resulting assemblies.

```sh
python3 tools/build.py launcher --framework .local/tv-framework
```

This builds `BixbyPromotion.dll`, `QuickLaunch.dll` and `Probe.dll`, then
assembles `.local/package/`. For another documented model, add its profile, for
example `--profile profiles/qe65s95fauxru-1301.0.json`; the profile's
`launcher` block names the host package and the entry assembly to build.
Public source cards are neutral text placeholders;
[optional publisher artwork](06-artwork.md) can be fetched before rebuilding.

## Adapt inputs and apps

The Home page builds one card per connected input from `get_source_list()` in
Samsung's `libsource-api.so`, and switches with the returned `sourceType`.
The name and icon come from the TV; a name set in Samsung's source list wins,
and an unnamed input shows the HDMI number from its icon. This is a private
platform interface: its availability, fields and memory ownership may differ
by model and firmware. If the call fails, the page says no inputs were found;
select an input from the native menu. The **Edit inputs** button at the top
right of the Home page opens Samsung Home's source list, where holding Select
on an input edits its name and device type. See [the source-list API findings](11-source-api.md).

On the QE65S95F (`T-RSMFDEUC-0090-1301.0`) the call returned a JSON array
allocated with `malloc`: Live TV (`sourceType` 0), a PlayStation 5 (14) and an
unnamed HDMI 3 (15), each with an icon from `com.samsung.tv.theme-resource`.
A `QN55S90FAFXZA` returned Live TV and one HDMI device (16).

Upload and run Probe to inspect the current main-operation handler:

```sh
tv push .local/build/Probe/Probe.dll Probe.dll
tv exec --command '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll'
```

The library is read from `apps.json`; verify all app IDs against the TV. See
[app configuration](06-artwork.md). Rebuild after source changes.

## Install without changing Home yet

On the U8000F the native-app container is **`com.samsung.tv.bixby-promotion`**.
Its `BixbyPromotion.dll` is replaced with our assembly, while the package's
existing identity, metadata and framework integration are retained. This
replaces Bixby Promotion's function. It is not a store-signed standalone TPK.

Inspect the installed package, its owner/labels, original entry assembly and
current registry flags first. This release's recipe requires the observed
owner `201:0`. If the layout differs, stop and adapt the profile.

```sh
tv exec --script recipes/snapshot.sh --arg home-01 \
  --arg com.samsung.tv.bixby-promotion --arg BixbyPromotion.dll
tar -czf .local/package.tar.gz -C .local/package .
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/install-launcher.sh --arg home-01
```

With the container and DLL, the snapshot also copies the original container,
records the entry DLL's digest and metadata (mode, owner, size) and packs the
copy into `bixby-promotion-original.tar.gz` in the snapshot folder.

Another profile can name a different host in its `launcher` block. The
QE65S95F profile uses `com.samsung.tv.adplayer` and `AdPlayer.dll`, because
Bixby Promotion is not installed there. Use both values in place of Bixby
Promotion's in the snapshot command, and append them to the install command,
so it becomes:

```sh
tv exec --script recipes/install-launcher.sh --arg home-01 \
  --arg com.samsung.tv.adplayer --arg AdPlayer.dll
```

Append them later to `restore-launcher.sh` as well, and use that app ID in the
Home and startup commands. The settings-bridge recipes still target Bixby
Promotion only.

The recipe refuses to run unless that snapshot's copy still matches both its
digest and the installed DLL. It then moves the cached native image out of the
execution path, replaces the assembly with the original's file mode, installs
resources, applies the package's observed SMACK label and requests a manual
launch. It does not change Home/startup.

Keep a second copy of the original container on the computer as well:

```sh
tv pull /opt/usr/share/quiet-tizen-backups/home-01/bixby-promotion-original.tar.gz \
  .local/home-01/bixby-promotion-original.tar.gz
```

`tv pull` stops on a file it cannot transfer in one session. In that case
pull at least the original entry DLL from the copy's `bin/` folder and the
`original-assembly.sha256` and `original-metadata.txt` records. On the
QE65S95F the AdPlayer archive is about 1.3 MB, above the transfer limit, so
only this fallback applies there:

```sh
tv pull /opt/usr/share/quiet-tizen-backups/home-01/adplayer-original/bin/AdPlayer.dll \
  .local/home-01/AdPlayer.dll
tv pull /opt/usr/share/quiet-tizen-backups/home-01/original-assembly.sha256 \
  .local/home-01/original-assembly.sha256
tv pull /opt/usr/share/quiet-tizen-backups/home-01/original-metadata.txt \
  .local/home-01/original-metadata.txt
```

The new release recipes are a generalized form of the operations used on the
original TV. Filesystem steps are not a single atomic transaction. If a step
fails, inspect the saved state and use recovery; do not rerun by deleting
the original backup. The snapshot and backup folder is
`/opt/usr/share/quiet-tizen-backups/home-01`.

Verify visibly:

- All pages render; the D-pad, Select and Back behave correctly.
- Input cards show the connected devices and switch to them, with picture and audio.
- Volume/mute and applicable quick settings work.
- Native Advanced and Network settings still open.
- Chosen app shortcuts launch the correct installed apps.
- The Controllers shortcut reaches the Bluetooth device list.

The `build_ms` value in `/tmp/quiet-home-state.txt` measures scene construction,
not input-to-photon latency or complete rendering time.

## Make it open on Home and startup

Save the **three original values** privately first: the main-operation Home
handler printed by Probe, `db/appfw/home_appid`, and
`db/appfw/startup_appid`. They all happened to be `com.samsung.tv.csfs` on the
case-study device; restoration must use the actual saved values.

Only after the visible verification above:

```sh
tv exec --command '/usr/bin/dotnet /opt/usr/share/quiet-tizen-backups/home-01/Probe.dll set-home com.samsung.tv.bixby-promotion'
tv exec --command 'vconftool set -f -t string db/appfw/home_appid com.samsung.tv.bixby-promotion; vconftool set -f -t string db/appfw/startup_appid com.samsung.tv.bixby-promotion'
```

Read all three selectors back. Let temporary root cleanup finish, then
restart with the remote. Check full boot, the Home button, standby/wake
and return from a streaming app separately. Automatic startup was verified
on the original TV; an OS update can change routing.

On a QN75QN85DDFXZA with all three selectors assigned, the first full
restart left the screen black for over 30 seconds until Home was pressed,
although the launcher process had started during boot. With **Auto Open
Last App** turned on and **Autorun Samsung TV Plus** turned off in Start
Screen Options (**Start with Smart Hub Home** stayed on), the next restart
and a standby/wake opened Quiet Home without pressing Home. Both settings
were changed together, so which one mattered is unknown.

## Replace the slow settings/sidebar path

The quick settings page handles volume, brightness, Game Mode and sleep
timer with native APIs. Availability is checked for the current input. Its
Theme card switches between the default dark theme and the light one; the
choice is saved in the container's data directory. Mute stays on the remote.
Advanced settings, network setup and Bluetooth pairing still use Samsung's
native menu on demand.

The small QuickLaunch bridge exits on vendor `launch_type=preload` requests.
For interactive requests it opens Quiet Home's settings page and exits.
It does not keep a second heavy settings process running.

After quick settings work, take a new snapshot and install the bridge:

```sh
tv exec --script recipes/snapshot.sh --arg shell-01
tv exec --script recipes/install-settings-bridge.sh --arg shell-01
```

This replaces `/opt/usr/apps/com.samsung.tv.quicklaunch/bin/QuickLaunch.dll`
and updates two `key_filter` rules:

| Rule owner / key | New destination |
|---|---|
| `com.samsung.tv.quicklaunch` / `XF86More` | Quiet Home, `quiet_page:settings` |
| `org.tizen.menu` / `XF86SysMenu` | Quiet Home, `quiet_page:settings` |

The recipe selects semantic keys and checks the saved values. It does not
copy another TV's row numbers. It notifies
`memory/appfw/app_context_db_change` after the transaction.

The recipe expects exactly one row for each rule. A QN75QN85DDFXZA on
`T-PTMDAKUC-0090-2130.0` has **two** `com.samsung.tv.quicklaunch` /
`XF86More` rows, so the recipe stops there before changing anything. Count
the rows on your TV first; this release has no recipe for a duplicated rule.

Test both settings buttons, then generate/apply the **`light-shell`** cleanup
group from the `shell-01` registry snapshot. That disables the native sidebar
and browser. Keep the QuickLaunch bridge and `org.tizen.menu` enabled.

Retain rollback files and remove only the known staged files after verification:

```sh
tv exec --command 'rm -f /home/owner/share/tmp/sdk_tools/quiet-tizen/package.tar.gz /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll; rm -rf /home/owner/share/tmp/sdk_tools/quiet-tizen/unpacked'
```

## Performance choices in the launcher

App art is local. Real `ImageView` objects are created ahead of use and moved
between the hidden cache parent and visible cards. `ReleasePolicy=Destroyed`
keeps decoded textures alive while those views live. The cache is bounded
by catalog size and the grid/dock variants, not an ever-growing screenshot
cache. Image loading is asynchronous, so a first launch still needs loading
time; subsequent page visits avoid recreating image views.

The clock updates every 30 seconds and stops while the launcher is paused.
There is no always-running controller-poll loop. The 180 ms repeat filter
applies inside Quiet Home only; changing it cannot fix input lag inside Netflix.
Local diagnostic files retain only current launcher state and are overwritten;
the launcher has no telemetry/network client.
