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
`Tizen.Applications.Common.dll`, `Tizen.System.dll` and `Tizen.dll`, plus
`Tizen.TV.NUI.dll` and `Tizen.TV.Broadcast.TVService.dll`, the TV service
behind the input preview ([HDMI preview](12-hdmi-preview.md)).
The shipped project files use `Private=false`, so those references are not
copied into the app package.

Find the paths on the target:

```sh
tv exec --command 'find /usr/share/dotnet.tizen -name Tizen.NUI.dll -o -name Tizen.Applications.Common.dll -o -name Tizen.System.dll -o -name Tizen.dll -o -name Tizen.TV.NUI.dll -o -name Tizen.TV.Broadcast.TVService.dll'
```

Inspect the results. When all six runtime assemblies are in one directory
(`/usr/share/dotnet.tizen/framework` by default), pull them in one root
session:

```sh
tv framework-pull --from /usr/share/dotnet.tizen/framework
```

It verifies each file's SHA-256 against the TV and writes nothing unless all
transfers verify. It pulls only the assemblies missing from
`.local/tv-framework/` and never replaces a copy already there, so a checkout
that has the first four from an earlier build gets the two TV service ones. If
the assemblies are spread over several
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

The focused input card widens to 16:9 and the other cards narrow to make room;
on the Home tab the card of the input on screen is wide and Down focuses it.
That card shows the input's live picture: the TV's main video plane is moved
into it through Samsung's private TV service. Only the input already on screen
is previewed, because previewing another one switched the TV's source or
pipeline and with it HDR and Game Mode on every focus move; Live TV keeps its
icon. While Quiet Home is in front it mutes the input's sound, as Samsung's
home does, and turns it back on when an input opens or Quiet Home goes to the
background. Before an input or app opens the TV's full-screen picture is put
back first. After an app the TV plays no picture until the input is opened
again: when Quiet Home opened the app the card shows its icon, after an app
opened with the remote's own buttons it stays black. If the TV refuses the preview (for example without the internal
platform privilege, or on a TV without those assemblies), the cards keep their
icons until Quiet Home restarts and the diagnostics file says
`preview=unsupported`. All of this was tested on the QE65S95F
(`T-RSMFDEUC-0090-1301.0`) only. See
[the HDMI preview findings](12-hdmi-preview.md).

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

Quiet Home writes what it is doing to `/tmp/quiet-home-state.txt`; see
[the diagnostics file](#the-diagnostics-file) for what its values mean.

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
`quiet_page=home|apps|settings` a page, and `quiet_focus=toolbar` puts the focus
on the page's first toolbar button. Any launch without `quiet_demo`
returns to the normal state. Demo mode never shows an input preview and leaves the
sound alone. With the launcher installed, refresh all six
images with:

```sh
python3 tools/screens.py --host "$TV_IP" --sdb "$SDB" --profile profiles/qe65s95fauxru-1301.0.json
```

The same run also takes Home with its first toolbar button focused in both
themes (`toolbar-dark` and `toolbar-light`). These frames are only for
comparing builds and do not go into the README.

Before each frame the recipe closes Samsung's screen saver
(`com.samsung.tv.glscreensaver-launcher`) if it is running; the Screen Saver
setting itself is left on. The script still refuses to write `docs/screens/`
if a frame comes out black, for example when the screen turned off.

## Install a newer build

A later build replaces the installed one without restoring Samsung's
launcher first. Name the snapshot Quiet Home was installed from and give each
update its own name:

```sh
COPYFILE_DISABLE=1 tar -czf .local/package.tar.gz -C .local/package .
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
same snapshot after any number of updates. `COPYFILE_DISABLE=1` keeps macOS
tar from adding `._` metadata files to the package.

Every update keeps a full copy of the build it replaced. To free that space,
remove old updates right after a successful `apply`:

```sh
tv exec --script recipes/update-launcher.sh --arg prune --arg home-01 --arg 3
```

`prune` keeps the installed update and the two below it, and removes every
other update of the snapshot, rolled-back ones included. It never touches
the installed build or Samsung's container in the snapshot. Rollback then
goes back as far as the oldest kept update; `restore-launcher.sh` works as
before.

For a development loop, `tools/update.py` does all of this in one run: it
builds the launcher for the profile, packs and uploads it, applies it as
`dev-YYYYMMDD-HHMMSS`, optionally prunes (`--keep N`) and takes the demo
frames with `tools/screens.py` into `.local/baseline/<name>`, comparing them
with `--compare DIR` when given. The name, snapshot and DLL digest are
appended to `.local/updates.log` for a later rollback. Like `screens.py`, it
restarts Quiet Home and shows demo pages over the picture:

```sh
python3 tools/update.py --host "$TV_IP" --sdb "$SDB" \
  --profile profiles/qe65s95fauxru-1301.0.json --snapshot home-01 \
  --keep 3 --compare .local/baseline/BUILD
```

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

## How the code is organized

Quiet Home's source has two layers, and `launcher/tizen/Home.csproj`
compiles both into the one entry assembly the profile names. The package
layout does not change: one DLL and `res/quiet-home`.

- **`launcher/core`** (`QuietHome.Core`) holds the behaviour and has no
  Tizen references:
  - `HomeController` keeps the state (page, focus, inputs, catalog,
    settings, theme, demo mode). It handles the app's life cycle and the
    remote, acts through the ports and shows the result through the view
    port. Errors become a notice on screen.
  - `Navigation/` moves the focus. `Navigator` turns a key into a
    `KeyOutcome`, and `KeyRepeatFilter` drops held-key repeats that come
    within 180 ms.
  - `Screens/` describes what is on screen. `ScreenBuilder` turns the state
    into a `HomeScreen`: a `ChromeModel` (title, tabs, toolbar, bottom lines)
    and one `PageModel` per page. `IHomeView` is the port the view
    implements.
  - `Platform/` holds the ports to the TV: `IInputSources`,
    `IQuickSettings`, `IThemeStore`, `IClock`, `ISystemLauncher`,
    `IDiagnostics` and `IInputPreview`, passed together as `HomePorts`. `Platform/Demo/` holds
    the in-memory versions that demo mode puts in front of them.
  - `Actions/HomeAction` lists what Select on a card can do.
    `Settings/SettingsTile` is the table of Settings tiles, and
    `QuickSettingsState` holds the values read from the TV. `Catalog/`,
    `Inputs/`, `Theme/` and `Diagnostics/` cover the app catalog, input
    cards, colors and the diagnostics file.
- **`launcher/tizen`** is the TV host:
  - `QuietHomeApplication` creates the ports, the view and the controller,
    and forwards Tizen's events to the controller. `KeyNames` maps the TV's
    key names to remote keys.
  - `Platform/` implements the ports with the P/Invoke declarations in
    `Interop/`. `TizenInputPreview` and `TvVideoPlane` implement the input
    preview and the mute with the TV service assemblies; `PreviewHole` in
    `Views/` cuts the see-through hole the picture shows in.
  - `Views/` draws a `HomeScreen` with NUI. `HomeView` draws the chrome,
    focus and notice, and one view per page draws that page's content.
    `Layout` holds the coordinates and sizes, and `ArtworkCache` keeps app
    art loaded.

`QuickLaunch.cs` (the settings bridge) and `launcher/probe` are separate
assemblies outside these layers. Keep them byte-identical: the bridge has no
update path.

A key press goes through these steps:

1. The host passes the key to `HomeController.Key`.
2. The repeat filter and `Navigator.HandleKey` decide the outcome: move the
   focus, change the page, adjust a value or activate the focused card.
3. To activate the card, the controller performs that card's `HomeAction`
   through a port.
4. The controller rebuilds the `HomeScreen` and passes it to
   `IHomeView.Render`. The mode is `Rebuild` for a new page, `Refresh` for
   new values and `AnimateFocus` for a focus move.
5. The controller rewrites the diagnostics file.

### Where to add things

- **A Settings tile.** The Settings page is a 4 × 2 grid with every place
  taken, so a new tile replaces an existing one. A bigger grid also needs
  `Layout`, `SettingsPageView` and the Settings column handling in
  `Navigator` changed. To add the tile:
  - add a line to `SettingsTile.All` and its big line to
    `SettingsTile.ValueText`;
  - add the icon `launcher/assets/quick-ICON.svg`;
  - if the tile shows a TV setting, add the setting's ID to
    `QuickSettingsState.TvSettingIds` and a sample value to
    `DemoQuickSettings`, which supplies demo mode's values.
- **An action.** Add a record to `HomeAction` and a case to
  `HomeController.Activate`. To open another Samsung screen or app, a
  `LaunchRequest` is usually enough. Anything else that changes the TV
  needs:
  - a method on a port;
  - its Tizen implementation in `launcher/tizen/Platform`;
  - a version in `Platform/Demo` if demo mode must leave the TV unchanged;
  - the same method on the test stand-in `FakeTv`.
- **A page.** Changes are needed in several places:
  - the core: the `Page` enum (its number is `page=` in the diagnostics
    file), `ChromeModel.TabNames`, a `PageModel` record, `ScreenBuilder`
    (title, content, toolbar button) and `quiet_page` in `LaunchOptions`;
  - `Navigator.MoveSideways`, which assumes three tabs;
  - the host: the tab positions in `Layout`, a page view in
    `launcher/tizen/Views` and a case in `HomeView`;
  - the page lists in `recipes/readme-screens.sh` and `tools/screens.py`.

Add a scenario test for every new behaviour (see below). Keep
the comment rule in [AGENTS.md](../AGENTS.md#c-code): every type says where
it sits in this structure, and every method says what it does.

### Tests without the TV

```sh
dotnet test tests/QuietHome.Core.Tests
dotnet format launcher/core/QuietHome.Core.csproj --verify-no-changes
dotnet format tests/QuietHome.Core.Tests --verify-no-changes
```

The test project builds the core without Samsung references, so it runs on
any computer and in CI. Unit tests sit in a folder named after the core
folder they test. `Scenarios/` holds tests written as remote-control use.
Each one runs on `Fakes/HomeHarness`, a real `HomeController` over two
stand-ins:

- `FakeTv` implements every port in memory and records what was switched,
  written, launched and previewed;
- `FakeView` records each render, notice and clock text.

```csharp
home.Tv.Locked.Add("brightness");
home.Open();
home.Launch(("quiet_page", "settings"));
home.Press(RemoteKey.Right, RemoteKey.Select);
Assert.Equal("Brightness is unavailable for the current input.", home.View.Notice);
```

These tests cannot see what NUI draws. Layout, colors, fonts and artwork are
checked only on the TV: by the demo frames and with the remote.

### The diagnostics file

Quiet Home rewrites `/tmp/quiet-home-state.txt` after every event. The file
holds the page and focus, the last activity and key, the inputs, the catalog
and artwork counters, the quick-setting values and, last, `preview=` (`on`,
`off` or `unsupported`). Its keys and their order are a contract for the
recipes and tools that read it; new keys go at the end.

`build_ms` measures how long the view takes to build a page's NUI objects. It
does not measure input-to-photon latency or the complete rendering time. It
also leaves out reading the TV's settings and input list. The settings are
read before the page is built. The input list is read once per event and
kept: start, resume, window focus, the TV's change callback, a Home request
and arriving at the Home page. The Home page is built from that kept list.
Builds from before the input list moved behind `IInputSources` also counted
`get_source_list()`, so their values are not comparable.

### Develop on the TV

Build against the TV's framework once, then run one command per change in a
window agreed with the owner:

```sh
python3 tools/update.py --host "$TV_IP" --sdb "$SDB" \
  --profile profiles/qe65s95fauxru-1301.0.json --snapshot home-01 \
  --keep 3 --compare .local/baseline/PREVIOUS
```

The demo frames (the three pages and a focused toolbar button, in both
themes) are reproducible byte for byte. A change that should not
alter what is drawn must therefore match the previous run's frames exactly. A
visual change makes its own run the new baseline. Then check by hand with the
remote what the frames do not show:

- every page;
- switching inputs, with picture and sound;
- the input preview: the card of the input on screen shows it without
  sound, other cards change no picture mode, and opening the input or leaving
  Quiet Home gives a full-screen picture with sound (the frames cannot show
  it, since a screenshot sees only the window);
- volume, brightness, Game Mode and the sleep timer;
- the theme;
- the Samsung settings tiles;
- the app shortcuts;
- the Home and Settings keys.

To undo the change, roll it back with the command the run prints (see
[Install a newer build](#install-a-newer-build)).
