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
Public app cards are plain placeholders showing the app's name;
[optional publisher artwork](06-artwork.md) can be fetched before rebuilding.

## Adapt inputs and apps

The Home page builds one card per connected input from `get_source_list()` in
Samsung's `libsource-api.so`, and switches with the returned `sourceType`.
The cards follow devices being plugged in or removed. The name and icon come
from the TV; a name set in Samsung's source list wins, and an unnamed input
shows the HDMI number from its icon. This is a private
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
tv exec --script recipes/snapshot.sh --arg home-01
tar -czf .local/package.tar.gz -C .local/package .
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/install-launcher.sh --arg home-01
```

`tv exec --script` passes the matched profile's `launcher` block to every
recipe as `QUIET_LAUNCHER_APP` and `QUIET_LAUNCHER_DLL`. Until Quiet Home is
installed in that container, the snapshot also copies the original container,
records the entry DLL's digest and metadata (mode, owner, size), packs the
copy into `bixby-promotion-original.tar.gz` in the snapshot folder and writes
the container and DLL to `launcher-container.txt`. The install and restore
recipes take them from there, so they always work on the container that was
backed up. Snapshots taken after the install keep no container copy.

Another profile can name a different host. The QE65S95F profile uses
`com.samsung.tv.adplayer` and `AdPlayer.dll`, because Bixby Promotion is not
installed there; the archive is then `adplayer-original.tar.gz`. Use that app
ID in the Home and startup commands.

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
not input-to-photon latency or complete rendering time. It does not include
reading the TV's input list: the list is read once per event (start, resume,
window focus, the TV's change callback, a Home request and arriving at the
Home page) and kept, and the Home page is built from the kept list. Values
from builds before this change also counted `get_source_list()` and are not
comparable.

To see what the TV draws without a camera, Probe can capture the UI layer
with `efl_util_screenshot` and write it as a PNG small enough for `tv pull`:

```sh
tv push .local/build/Probe/Probe.dll Probe.dll
tv exec --command '/usr/bin/dotnet /home/owner/share/tmp/sdk_tools/quiet-tizen/Probe.dll screenshot /tmp/quiet-home.png'
tv pull /tmp/quiet-home.png .local/screens/home.png
```

An optional width and height follow the path (default 1920 1080). Only the
UI is captured: an HDMI or TV picture comes out black, and so does the whole
frame while the screen is off. Keep the images in `.local/`; they can show
account names, app lists and other personal details.

The README screenshots come from Quiet Home's demo mode. A launch with the
extra `quiet_demo=1` shows the public catalog (packaged as
`apps.public.json`), three sample inputs, fixed settings values and a 9:41
clock. Settings and the theme changed during a demo stay in memory and change
nothing on the TV; each demo starts from the same values. `quiet_theme=light|dark` picks a theme without saving it, and
`quiet_page=home|apps|settings` a page. Any launch without `quiet_demo`
returns to the normal state. With the launcher installed, refresh all six
images with:

```sh
python3 tools/screens.py --host "$TV_IP" --sdb "$SDB" --profile profiles/qe65s95fauxru-1301.0.json
```

Before each frame the recipe closes Samsung's screen saver
(`com.samsung.tv.glscreensaver-launcher`) if it is running; the Screen Saver
setting itself is left on. The script still refuses to write `docs/screens/`
if a frame comes out black, for example when the screen turned off.

## Install a newer build

A later build replaces the installed one without restoring Samsung's
launcher first. Name the snapshot Quiet Home was installed from and give each
update its own name:

```sh
tar -czf .local/package.tar.gz -C .local/package .
tv push .local/package.tar.gz package.tar.gz
tv exec --script recipes/update-launcher.sh --arg apply --arg home-01 --arg visual-01
tv exec --script recipes/update-launcher.sh --arg rollback --arg home-01 --arg visual-01
```

`apply` refuses to run when the installed DLL no longer matches the
snapshot's `installed-home.sha256`, for example after a vendor app update, or
when a cached native image has returned. It keeps the previous DLL and
resources in `updates/visual-01/` of the snapshot folder, installs the new
build with the original's file mode and SMACK label, records the new digest
and relaunches the container. Home and startup selectors are unchanged.

`rollback` returns the build the update replaced, newest update first.
`restore-launcher.sh` still restores Samsung's original container from the
same snapshot after any number of updates.

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
timer with native APIs. Availability is checked for the current input.
Where the TV's menu offers Game Mode Auto (the S95F does), the Game Mode card
steps Auto → On → Off and shows the TV's own name for the value; on TVs with
only the older on/off item it toggles On and Off. Its
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
and points two `key_filter` rules, `com.samsung.tv.quicklaunch` /
`XF86More` and `org.tizen.menu` / `XF86SysMenu`, at Quiet Home's settings
page (`quiet_page:settings`). The profile's `launcher.settings_route`
chooses how, and `tv exec` passes it to the recipe:

| `settings_route` | Keys launch | Used by |
|---|---|---|
| `launcher` (default) | the container that holds `res/quiet-home` | U8000F |
| `quicklaunch` | the replaced QuickLaunch, which opens Quiet Home | QE65S95F |

On the QE65S95F, a key launch of the AdPlayer container itself opened
nothing, while the same key sent to QuickLaunch opened Quiet Home's settings,
also after standby and a restart.
The Smart Control's settings button sends `XF86More` there. Either way the
recipe stops unless exactly one container holds `res/quiet-home`, and it
records the target in `bridge-target.txt` for the restore.

The recipe selects semantic keys and checks the saved values. It does not
copy another TV's row numbers. It notifies
`memory/appfw/app_context_db_change` after the transaction.

Only each key's unconditional row (empty `on_selector`) is redirected, and
the recipe stops unless there is exactly one. Conditional rows stay as they
are. A QN75QN85DDFXZA on `T-PTMDAKUC-0090-2130.0` has **two**
`com.samsung.tv.quicklaunch` / `XF86More` rows; check that the second one is
conditional before you run the recipe there.

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
