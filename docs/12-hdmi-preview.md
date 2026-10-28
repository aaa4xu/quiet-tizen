# HDMI input preview on the home screen

This note explains how a Samsung TV puts a live HDMI picture on its own home
screen, and how to add the same thing to Quiet Home. The findings come from a
QE65S95FAUXRU on `T-RSMFDEUC-0090-1301.0` (Tizen 9, 32-bit ARM userland) unless
another model is named. The API is private: check the exports and behaviour on
your own TV before relying on them.

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

This is the feature Quiet Home's Home page is shaped for: the focused HDMI
card shows the input, while the rest of the page keeps its normal background.

1. Connect the video plane to the launcher window once with
   `SetXWindow((uint)windowId)`.
2. On a focus move, call
   `SetPreviewSource((Source)input.SourceType, appId)` for the focused input,
   then `SetSize(cardX, cardY, cardH, cardW)` to place the plane at that card.
3. Draw the card as a frame: keep its border, focus ring and the name/text
   below it, but leave the card's interior unpainted and do not put the icon
   inside it. The video plane fills that rectangle.
4. When the focus leaves the inputs, the page changes, the app is paused or an
   app is launched, call `StopPreviewSource()` and `SetXWindow(0)`.

Whether the video plane is composited above or below the NUI layer decides how
much of the card you may paint. If it is above the UI, any text or icon inside
the rectangle is hidden; if it is below, an opaque card background hides the
video. Leaving the rectangle unpainted and keeping the labels outside it works
in both cases. Verify the stacking on the TV with a bright test frame before
committing to a visual design.

Only one preview source can be shown at a time, so preview the focused card
only. `SetPreviewSource` is a preview, not a source switch: the main picture
and audio of the TV are not changed. Check whether the preview carries audio on
your model; if it does, mute it with `IAVControl.MuteAudio(1)` while the
preview is visible and restore `0` on hide.

## Adding it to Quiet Home

The launcher separates the core (no Tizen references) from the TV host. Keep
the decision in the core and the TV calls in the host, as
[the launcher documentation](04-launcher.md#how-the-code-is-organized) asks.

### 1. Get the framework assemblies

Pull the two assemblies the preview needs from the TV into the private
`.local/tv-framework/` directory; do not commit them and do not copy them into
the package:

```sh
tv pull /usr/share/dotnet.tizen/framework/Tizen.TV.NUI.dll \
  .local/tv-framework/Tizen.TV.NUI.dll
tv pull /usr/share/dotnet.tizen/framework/Tizen.TV.Broadcast.TVService.dll \
  .local/tv-framework/Tizen.TV.Broadcast.TVService.dll
```

The higher-level TV-window controls also need
`Tizen.TV.NUI.VideoWindowControl.dll`, `Tizen.TV.Broadcast.TVWindowControl.dll`
and `Tizen.TV.UI.SharedAPI.dll`; add them only if you use those classes. At
run time the TV provides all of them from its framework directory.

`tools/tv.py framework-pull` knows only the four assemblies Quiet Home
already uses, and `tools/build.py` checks for the same four names. Extend both
so a fresh checkout pulls and validates the new files too, or pull them by hand
as above.

### 2. Reference them without packaging them

Add the references to `launcher/tizen/Home.csproj` with `Private=false`, next
to the existing four:

```xml
<Reference Include="Tizen.TV.NUI">
  <HintPath>$(TV_FRAMEWORK_DIR)/Tizen.TV.NUI.dll</HintPath><Private>false</Private>
</Reference>
<Reference Include="Tizen.TV.Broadcast.TVService">
  <HintPath>$(TV_FRAMEWORK_DIR)/Tizen.TV.Broadcast.TVService.dll</HintPath><Private>false</Private>
</Reference>
```

The container's package already exists; its manifest and Cynara privileges are
what the app runs with, so do not add a new TPK.

### 3. Add a port in the core

Add `launcher/core/Platform/IInputPreview.cs`:

```csharp
/// <summary>
/// Port for the TV's source-preview plane: shows one connected input in the
/// rectangle of a Home card, or hides it. The host implements it with the TV
/// framework; demo mode uses a no-op.
/// </summary>
public interface IInputPreview
{
    /// <summary>Shows <paramref name="input"/> in the rectangle of card
    /// <paramref name="slot"/>; returns false when the TV refuses.</summary>
    bool Show(InputSource input, int slot);

    /// <summary>Stops the preview and disconnects the video plane.</summary>
    void Hide();
}
```

Pass it through `HomePorts`. Add a no-op `DemoInputPreview` under
`Platform/Demo/` that returns `true`, and the same methods to the test stand-in
`FakeTv`, recording `PreviewShown` and `PreviewHidden`.

In `HomeController`, call `Show` only while the Home page is current and the
focused card is a connected input whose `sourceType` maps to a `Source` value.
Call `Hide` on every other focus, page change, app launch, `Pause` and
`Terminate`. Remember a failed `Show` and stop retrying for the session, so a
TV without the privilege does not log an exception on every key press.

Add a scenario test under `tests/QuietHome.Core.Tests/Scenarios/` in the style
of the existing remote-control tests:

```csharp
home.Open();
home.Press(RemoteKey.Right);            // focus the game console (sourceType 14)
Assert.Equal(14, home.Tv.PreviewShown);
home.Press(RemoteKey.Right);            // next input: HDMI3 (sourceType 15)
Assert.Equal(15, home.Tv.PreviewShown);
home.Press(RemoteKey.Down);             // leave the inputs
Assert.True(home.Tv.PreviewHidden);
```

The core decides, the host draws: these tests cannot see the video plane.

### 4. Implement the host port

Add `launcher/tizen/Platform/TizenInputPreview.cs` (the `Layout.InputRect`
helper is described in the next step):

```csharp
using System;
using Tizen.Applications;
using Tizen.NUI;
using Tizen.TV.Broadcast.TVService;
using Tizen.TV.Broadcast.TVService.API.Interface;
using Tizen.TV.Broadcast.TVService.Types;
using Tizen.TV.NUI;

/// <summary>
/// The TV implementation of <see cref="IInputPreview"/>: connects the TV video
/// plane to Quiet Home's window and asks TVService to preview one input in the
/// focused card's rectangle. A missing privilege or a refused call turns the
/// preview off for the session.
/// </summary>
sealed class TizenInputPreview : IInputPreview
{
    IVideoControl? video;
    ISourceControl? source;
    bool connected;

    public bool Show(InputSource input, int slot)
    {
        try
        {
            video ??= TVServiceAPI.Instance().CreateVideoControl(Profile.Main, 0);
            source ??= TVServiceAPI.Instance().CreateSourceControl(Profile.Main, 0);
            if (!connected)
            {
                int id = Window.Default.GetResourceID();
                if (video.SetXWindow((uint)id) != 1)
                    return false;
                connected = true;
            }
            var r = Layout.InputRect(slot);
            if (video.SetSize(r.X, r.Y, r.H, r.W) != 1)
                return false;
            string appId = Application.Current.ApplicationInfo.ApplicationId;
            return source.SetPreviewSource((Source)input.SourceType, appId) == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Hide()
    {
        try { source?.StopPreviewSource(); } catch (Exception) { }
        try { if (connected) video?.SetXWindow(0); } catch (Exception) { }
        connected = false;
    }
}
```

Notes on the calls:

- `SetSize` takes `(x, y, height, width)`; passing the NUI width/height in the
  usual order silently produces a transposed rectangle.
- Keep `IVideoControl` and `ISourceControl` alive while the preview is used;
  both are `IDisposable`. Dispose them in `OnTerminate` after `Hide`.
- `Window.Default.GetResourceID()` is an extension method from
  `Tizen.TV.NUI`; it throws when the internal platform privilege is missing.
- `SetPreviewSource` returns `1` on success. A `0` or `-1` means the TV did not
  take the request; fall back to the icon card.

### 5. Draw the card as a frame

`HomePageView.BuildInputs` currently draws a filled card and a centred icon.
For the preview slot:

- add `bool Preview` to `InputCardModel` and set it in `ScreenBuilder` for the
  focused input card on the Home page;
- in `HomePageView`, draw the card with a transparent background and skip the
  centred icon when `Preview` is true; keep the shadow, border, focus ring and
  the name/text lines below the card;
- add `Layout.InputRect(int slot)` that returns the same `Box` the loop in
  `HomePageView.BuildInputs` computes, so the host port and the view cannot
  drift apart;
- if the video plane turns out to be **below** the UI on the TV, the opaque
  `free-background.svg` layer covers it. Draw the background as panels around
  the preview rectangle (or make that layer transparent and paint the
  surrounding area) so the rectangle stays unpainted.

The demo build must not change: `DemoInputPreview` returns `true` without
touching the TV, and the demo frames stay byte-for-byte identical.

### 6. Report and fall back

Add a `preview:` key at the end of `/tmp/quiet-home-state.txt` (for example
`on`, `off` or `unsupported`) so the recipes and tools can tell whether the
plane is active. Appending a key keeps the existing contract; do not reorder
the old ones. When `Show` fails once, record `unsupported` and leave the icon
cards exactly as they are today.

## Verify on the TV

Build against the TV's own assemblies and install the build as usual
(`docs/04-launcher.md`). Then check, with the remote and with `dlog`:

- Focus an HDMI card: the card shows the input. Move the focus: the preview
  follows or hides.
- Leave the inputs for the dock, open Apps or Settings, launch an app, press
  Home again, pause and resume: the preview appears and disappears with the
  page.
- Unplug the HDMI device: the input disappears from the source list; the card
  falls back to its icon and the preview stops.
- Full restart and standby/wake: no stale video plane, no black card.
- `dlog -t TVService` and `dlog -t TVWindowControl` for refused calls. A
  `MethodAccessException` means the container lacks
  `internal/default/platform`.
- The preview cannot be seen in `efl_util_screenshot`; use a camera, or capture
  the video plane with `secvideo_api_capture_screen_video_only` from a root
  session (see `.local/ambilight-findings.md`).

## Limits

- The API is private and model/firmware-specific. Recheck the exports on a
  different TV before reusing the code.
- The internal platform privilege is granted to in-house containers. A
  third-party container may not have it; the preview then stays off.
- The TV may show the preview plane above the UI; do not put text or icons
  inside the rectangle.
- Only one input can be previewed at a time.
- Do not build the preview from captured frames in the launcher:
  `libvideo-capture.so` works from a root session, is a snapshot API at about
  15–20 fps and is not part of the normal app path.
