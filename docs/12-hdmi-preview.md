# HDMI input preview on the home screen

This note explains how a Samsung TV puts a live HDMI picture on its own home
screen, and how Quiet Home shows the input on screen inside its card. The
findings come from one TV, a QE65S95FAUXRU on `T-RSMFDEUC-0090-1301.0` (Tizen 9,
32-bit ARM userland), unless another model is named; no other model was tested.
The API is private: check the exports and behaviour on your own TV before
relying on them.

## What the TV actually draws

The HDMI picture is **not** part of the app's UI surface. The TV keeps the
input in a separate video plane, and the home screen is an overlay above or
below that plane. Two observations from this TV show the split:

- `efl_util_screenshot` (and the screen mirror built on it) captures only the
  UI compositor. An HDMI or Live TV picture comes out black, and so does the
  whole frame while the screen is off. This is why Quiet Home's screenshots
  never show the input picture.
- `secvideo_api_capture_screen_video_only` in `libvideo-capture.so` captures
  the video plane itself. On the same TV it returned the HDMI2 frame while the
  UI capture was black.

So a home-screen preview cannot be made by drawing a captured frame into an
`ImageView`. The TV has to place the input's video plane into the app's window
or into a separate TV window at a chosen rectangle. Samsung's own home does
that through the TV service, not through a screenshot.

## The framework API behind it

The TV framework assemblies are in `/usr/share/dotnet.tizen/framework/`. The
relevant pieces, decompiled from this TV, are:

| API | What it does |
|---|---|
| `Tizen.TV.NUI.TVWindow.GetResourceID(Window)` | Returns the X/window resource id of the NUI window. It checks `http://tizen.org/privilege/internal/default/platform`. |
| `Tizen.TV.Broadcast.TVService.TVServiceAPI.Instance().CreateVideoControl(Profile, ushort)` | Returns an `IVideoControl` for the TV video pipeline. |
| `IVideoControl.SetXWindow(uint windowId)` | Connects the video plane to that window. `0` disconnects it. |
| `IVideoControl.SetSize(int x, int y, int h, int w)` | Places the video plane at a rectangle. **The last two arguments are height, then width.** |
| `IVideoControl.SetXWindowSize(uint windowId, int x, int y, int h, int w)` | Same, but also sets the window rectangle. |
| `TVServiceAPI.Instance().CreateSourceControl(Profile, ushort)` | Returns an `ISourceControl`. |
| `ISourceControl.SetPreviewSource(Source sourceType, string appID)` | Shows one input as a preview; returns `1` on success. |
| `ISourceControl.StopPreviewSource()` | Ends the preview; returns `1` on success. |
| `ISourceControl.SetSource(Source sourceType, string appID)` / `DeactivateSource()` | Switches the main source instead of previewing it. |
| `TVServiceAPI.Instance().CreateAVControl(Profile, ushort)` | Returns an `IAVControl` with `MuteAudio(int)` and `MuteVideo(int)`. |

`Profile` and `Source` are in `Tizen.TV.Broadcast.TVService.Types`. The values
match the source-list API from [the source-list findings](11-source-api.md):
`Source.TV` is 0, `Source.HDMI1` is 13, `Source.HDMI2` is 14, `Source.HDMI3` is
15. A source-list `sourceType` can therefore be cast to `Source` directly.
`Profile.Main` is 1; `Profile.Thumbnail` is 801, but the preview path here uses
`Profile.Main` with sub-id 0, as Samsung's own code does.

Samsung's managed code wraps the same calls in
`Tizen.TV.Service.ViewPlugin.Common.TVServiceController`: `ConnectToVideo`
computes `TVWindow.GetResourceID(Window.Instance)` and calls
`CreateVideoControl(Profile.Main, 0).SetXWindow(...)`; `StopPreviewSource`
calls `CreateSourceControl(Profile.Main, 0).StopPreviewSource()`.
`com.samsung.tv.csfs` (the home app) uses `ConnectToVideo(true)`; the same
calls are available to any .NET app in a TV container.

The higher-level TV-window controls are built on the same operations:

- `Tizen.TV.Broadcast.TVWindowControl.TVWindowControlAPI` implements
  `IVideoWindowControl` and `ITVWindowControl`. `SetTVAttribute` takes a
  `TVAttribute` (window resource id, group id, profile, base resolution,
  optional video-shell surface) and `SetDisplayArea` takes a `VideoAttribute`
  and a `WindowAttribute`. It also checks the internal platform privilege.
- `Tizen.TV.NUI.VideoCanvasView` is a NUI view that forwards its rectangle to
  an `IVideoWindowControl`. Its four-argument `SetDisplayArea` path is meant
  for player-backed controls (`Tizen.TV.Multimedia.MediaPlayer` and similar);
  `TVWindowControlAPI` throws `NotImplementedException` there, so for a source
  preview use `IVideoControl` directly rather than `VideoCanvasView` with
  `TVWindowControlAPI`.
- `Tizen.TV.Application.Window.VirtualScreenLinker` is the FLUX/PIG wrapper used
  by Samsung's own home view plugins: it assigns app windows to a virtual
  screen and sets its geometry (`VirtualScreenSetGeometry` in
  `libvirtualscreen-linker.so`). It is another layer over the same video plane;
  a NUI app does not need it for a single preview rectangle.

The documented Web equivalent is the **TVWindow API** (`tizen.tvwindow`):
`show(rect, type)`, `setSource(videoSource)`, `hide()`, with the public
privilege `http://tizen.org/privilege/tv.window`. That API is the same idea: a
native TV window that shows a TV or HDMI source at a rectangle, separate from
the app's HTML. The .NET TV APIs above are the lower-level equivalent.

## Privileges and availability

`TVWindow.GetResourceID` and `TVWindowControlAPI` call Cynara with
`http://tizen.org/privilege/internal/default/platform`. An in-house package
gets this privilege automatically even when its manifest does not list it. On
this TV, `com.samsung.tv.adplayer` (the QE65S95F profile's launcher container)
is granted it:

```sh
tv exec --command 'grep "User::Pkg::com.samsung.tv.adplayer;" /usr/share/cynara/db/_MANIFESTS_GLOBAL | sed "s/.*;\(http[^;]*\);.*/\1/" | sort -u'
```

The output includes `http://tizen.org/privilege/internal/default/platform`.
Check the same line for the container Quiet Home replaces. If the privilege is
missing, `GetResourceID` throws `MethodAccessException`; keep the icon cards and
turn the preview off for that session instead of failing the page.

The TV also has to keep the input's video plane alive while the launcher is in
front. Samsung's home is a system home and does that; a third-party launcher
may be treated differently. Test the sequence on the target TV before
designing around it.

## Two ways to show it

### A. A live backdrop for the whole home screen

This is closest to Samsung's own home: the current input stays connected to
the launcher's window, the launcher draws a dark translucent scrim over it, and
the UI sits on top. It needs one window connection and no per-card rectangle.

1. On Home/resume, get the window id and call
   `CreateVideoControl(Profile.Main, 0).SetXWindow((uint)windowId)`.
2. Make the NUI window and its background translucent:
   `Window.Default.SetTransparency(true)` and a background colour with alpha
   below 1. Replace the opaque `free-background.svg` layer with a scrim
   (or draw it with reduced opacity).
3. On pause, leaving Home or launching an app, call `SetXWindow(0)` and
   `DeactivateSource()`.

The picture covers the whole screen, so this is a background effect, not a
preview card. It is the least code and the easiest to validate.

### B. A live preview inside an input card

This is what Quiet Home does. It previews only the input that is already on
screen: the TV's main video plane already plays that input, so the launcher
only moves the plane into the card. Previewing any other input switches the
TV's source or pipeline and, with it, the picture mode of the whole screen
(see the findings below), so Quiet Home does not do it.

1. Make the window transparent once, when the app starts
   (`Window.SetTransparency(true)`). The pages cover the whole window, so
   nothing else changes, and switching it later resets the plane.
2. When the card of the input on screen is shown wide, tie the main plane to
   the window and place it with `SetXWindowSize(windowId, x, y, h, w)`, then
   `SetSize(x, y, h, w)`.
3. Cut a see-through hole the shape of the card into the window (below) once
   the size holds.
4. When the focus leaves the card or the page changes, close the hole first,
   then one frame later call `SetSize(0, 0, 1080, 1920)` and `SetXWindow(0)`.
5. Before opening an input or app, do step 4 at once and wait 0.5 s after
   `SetXWindow(0)` before the input switch. Mute the input's sound with
   `IAVControl.MuteAudio(1)` while the launcher is in front and turn it back on
   with `MuteAudio(0)` when an input opens or the launcher goes to the
   background.

## Findings on the QE65S95F

**All of the following was observed on one TV only: a QE65S95FAUXRU on
firmware `T-RSMFDEUC-0090-1301.0`, on 28–29 October 2026,** with a PlayStation 5
on HDMI2 (`sourceType` 14, HDR, Game Mode), a PC on HDMI3 (15, SDR), no antenna
on Live TV (0), from Quiet Home in the `com.samsung.tv.adplayer` container.
The owner watched the screen; screenshots cannot show the video plane. Treat
every point as a hypothesis on another model or firmware.

The calls below all returned `1` (the TV service's success code) unless a
point says otherwise.

### The plane and the window

- **The plane is below the UI.** With the normal opaque window nothing of the
  preview is visible. With the window transparent and the views hidden, the
  input shows. A half-transparent view over the picture blends with it, so a
  focus ring or a scrim may overlap the picture.
- **A hole works.** A view with a custom `Renderer` whose blend mode is off and
  whose fragment shader writes `vec4(0.0)` inside a rounded rectangle (and
  discards outside its corners) replaces the window's pixels with transparent
  ones, the way the underlay of DALi's `VideoView` does. The picture shows
  through it with rounded corners; everything else stays. Quiet Home's version
  is `launcher/tizen/Views/PreviewHole.cs`.
- **Coordinates are the window's 1920 × 1080**, in the order
  `(x, y, height, width)`. A 16:9 rectangle keeps the picture's shape; any
  other shape stretches it. Quiet Home therefore widens the card to 16:9.
- **Switching the window's transparency resets the plane** to full screen about
  0.1 s later, even with `SetXWindowSize`. Making the window transparent once
  at start removed every reset; the demo frames stayed byte for byte the same.
- **`SignalUpdateSizeInfo`** (subscribe through
  `TVServiceAPI.CreateSignalSubscriber`) reports the plane's actual rectangle:
  its `SignalData.ll` holds x, y, width and height as four 16-bit numbers,
  lowest first. `GetSize` does not: it kept reporting the rectangle Quiet Home
  had set while the picture was full screen.
- **The plane keeps its size and window tie.** It stayed card-sized after Quiet
  Home was restarted, and an input switch through the TV viewer showed the
  input in the card's rectangle. `SetSize(0, 0, 1080, 1920)` and
  `SetXWindow(0)` before anything else changes the picture prevent that.
- **Opening an input right after `SetXWindow(0)` gave a black picture with
  sound.** So did leaving the plane tied to the launcher's window (as
  Samsung's home does before the TV viewer). Waiting 0.5 s after
  `SetXWindow(0)` before the switch fixed it. The TV sends no
  `SignalXWindowChanged` for the untie, only when the viewer ties the plane
  about 1 s later, so Quiet Home waits for the notice and times out.
- **Timing.** `SetSize` takes about 20–30 ms on the UI thread. Moving the plane
  every frame would stall the UI; Quiet Home places it once, after the card's
  focus animation. With these steps the picture appeared in its card about
  0.6 s after the focus stopped, without a black or full-screen frame.

### Previewing other inputs (not used)

- **`SetPreviewSource` on the main profile** previews any input, including the
  one on screen, in about 140–200 ms. It does switch the TV's main source
  (`GetCurrentSourceInfo` reports the previewed input), with its sound, so
  `MuteAudio(1)` was needed. It reset the plane's size and the mute after it
  returned, and moving between the HDR PlayStation and the SDR PC made the
  whole screen flicker as the TV changed its picture mode.
- **After `StopPreviewSource` the input on screen was inactive**
  (`IsActive` false) until it was selected again.
- **The PIP profile** (`CreateSourceControl(Profile.PIP, 0)` with
  `SetPreviewSource`, and a PIP video control tied to the same window) shows
  another input without touching the main source, without sound and without
  the screen flicker. But it takes the input away from the main pipeline (the
  card of the input on screen went black, `IsActive` false until
  `SetSource` on the main profile), and starting it switched the TV's Game
  Mode off, with a second-long stutter, and on again when the PlayStation was
  opened.
- With either approach, moving the focus across an HDMI card on the way to
  another started a preview and a mode switch. That is why Quiet Home
  previews only the input on screen.
- **Live TV without an antenna** showed a black rectangle and Samsung's
  full-screen "antenna disconnected" background behind the window.

### Sound

- The main pipeline's sound plays under the launcher unless something mutes
  it. Samsung's home mutes audio and video on resume (decompiled
  `TVServiceController`). Quiet Home mutes with `MuteAudio(1)`.
- The TV viewer did **not** turn that sound back on: the PlayStation opened
  silent. Quiet Home calls `MuteAudio(0)` before opening an input and when it
  goes to the background, not before starting an app (the input's sound
  played for a couple of seconds while the app started).
- `MuteAudio(1)` answers `0` when the sound is already muted.

### Not solved

- **After another app the card's preview is black.** The TV still reports the
  input as active (`IsActive` true) but plays no picture until the input is
  opened again.
- Not checked: an HDMI input without a signal, an input unplugged while
  previewed, a full restart and standby/wake with a preview on, and whether
  the owner's own mute survives Quiet Home (`MuteAudio(0)` probably undoes it).

## How Quiet Home implements it

The decision is in the core and the TV calls are in the host, as
[the launcher documentation](04-launcher.md#how-the-code-is-organized) asks.

- **Assemblies.** `tools/tv.py framework-pull` also pulls
  `Tizen.TV.NUI.dll` and `Tizen.TV.Broadcast.TVService.dll` into
  `.local/tv-framework/`, `tools/build.py` checks for them, and
  `launcher/tizen/Home.csproj` references them with `Private=false`. They are
  not committed or packaged; the TV provides them at run time. The
  container's own manifest and Cynara privileges apply, so no new TPK is
  needed.
- **Core.** `Platform/IInputPreview` has `Show(sourceType, slot)`, `Hide()`,
  `Release(sound)`, `Silence()` and a `Failed` event. `HomeController` asks for
  a preview after every render while the card of the input on screen (other
  than Live TV) is shown wide on the Home page, outside demo mode: focused, or
  the card the Home tab leads to. It hides the preview on every other focus and
  page change, releases the screen before an input or app opens and on `Pause`
  and `Terminate`, and mutes the input when it comes to the front. After one
  refused call it stops asking until the app restarts. The diagnostics file
  ends with `preview=on|off|unsupported`. `Scenarios/PreviewScenarios` tests
  this with the remote.
- **Host.** `Platform/TizenInputPreview` waits until the card has settled,
  then calls `TvVideoPlane` (the only class that touches the TV service types;
  it is reached only inside `try` blocks, so a TV without those assemblies
  just turns the preview off), undoes the TV's size resets as
  `SignalUpdateSizeInfo` reports them and asks `HomeView` to open the hole
  once the size holds. `Layout.InputBox` widens the card to 786 × 442 and
  narrows the others; `HomePageView.Place` animates them, and the picture's
  rectangle is the card's, a few pixels larger, so its square corners stay
  under the window. Every TV service call and size report is appended to
  `/tmp/quiet-preview.txt` (it starts over past 64 KiB).
- **Demo mode** never previews or mutes, so the demo frames show the cards as
  drawn.

## Verify on the TV

Build against the TV's own assemblies and install the build as usual
(`docs/04-launcher.md`). Then check with the remote, and read
`/tmp/quiet-preview.txt` for the calls and the TV's answers:

- Home with an HDMI input on screen: its card is wide and shows the input
  without sound; moving to the other cards changes no picture mode.
- Select the card, and Back from Home: the input opens full screen with
  picture and sound.
- From Settings or Apps to the Home tab: the card of the input on screen is
  wide with its preview; Down focuses it.
- Open an app from the dock: no input sound while it starts.
- Unplug the HDMI device, restart fully, standby and wake: no stale plane.
- The preview cannot be seen in `efl_util_screenshot`; watch the screen, or
  capture the video plane with `secvideo_api_capture_screen_video_only` from a
  root session.

## Limits

- The API is private and model/firmware-specific; the findings above come
  from one TV. Recheck them on a different TV before reusing the code.
- The internal platform privilege is granted to in-house containers. A
  third-party container may not have it; the preview then stays off.
- Only the input on screen is previewed; other cards keep their icons.
- After another app the card stays black until the input is opened again.
- The TV keeps the plane's size after the app; a crash between showing and
  hiding a preview can leave the picture small until Quiet Home shows and
  hides a preview again.
- Do not build the preview from captured frames in the launcher:
  `libvideo-capture.so` works from a root session, is a snapshot API at about
  15–20 fps and is not part of the normal app path.
