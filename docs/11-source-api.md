# Samsung's source-list API: findings

Quiet Home builds its input cards from Samsung's private `libsource-api.so`.
These findings come from a QE65S95FAUXRU on `T-RSMFDEUC-0090-1301.0`
(Tizen 9, 32-bit ARM userland), unless another model is named. The library is
undocumented: check the exports, fields and behavior on your own TV before
relying on anything here.

## Reading the list

`get_source_list()` takes no arguments and returns a JSON array as a `char*`.
The string is allocated with `malloc` (its usable size was its length plus
one); the caller frees it with `free()`. Fifty calls with `free()` under
`MALLOC_CHECK_=3` ran without errors.

Each entry has these fields:

| Field | Observed values |
|---|---|
| `sourceType` | 0 for Live TV; 14 and 15 for two HDMI ports here; 16 for an HDMI device on a QN55S90FAFXZA |
| `connectionState` | `true` for connected inputs; Quiet Home shows only these |
| `detectedName` | Name reported by the device, URL-encoded (`PlayStation%205`); empty for a device that sends none |
| `editName` | Name set in Samsung's source list, URL-encoded; empty when unset |
| `editState`, `settingMethod`, `powerState`, `mbrActivityIndex` | Integers; meaning not established |
| `iconPath` | SVG under `/usr/apps/com.samsung.tv.theme-resource/shared/res/VD_ICONS/`, e.g. `i_icon_device_m_gamepad.svg`, `i_icon_device_m_hdmi3.svg` |
| `launcherFocusedIconPath`, `launcherUnfocusedIconPath`, `uniqueKey` | Empty here |
| `flagLegacySource` | `true` for the HDMI inputs |

The icons are colored SVGs with gradients and render on both themes. An
unnamed input's icon file carries its HDMI number, which Quiet Home uses as
the card's name. `connect_source(int sourceType)` switches to an input.

## Stored settings

The service behind the library keeps its data in the vconf key
`db/source-list/source-name`, a JSON array with `DetectedName`, `EditName`,
`EditState`, `DeviceType`, `OrginalDeviceType` (sic), `SettingMethod`,
`SourceType` and `MbrActivityIndex`. The PlayStation 5 had `DeviceType` 7, a
game console. Other type codes were not mapped.

## Write functions (not used)

The library also exports these; the signatures come from the C++ methods they
wrap. Neither was called:

| Export | Wrapped method |
|---|---|
| `set_edit_name(int sourceType, const char* name, int)` | `CSourceAPI::SetSourceEditName(int, const std::string&, int)` |
| `set_device_type(int sourceType, unsigned deviceType, int)` | `CSourceAPI::SetSourceDeviceType(int, unsigned, int)` |
| `set_source_detetedname_and_devicetype` (sic) | `SetSourceDetectedNameAndDeviceType(int, const std::string&, unsigned, int)` |
| `get_edit_name`, `get_device_type` | `GetSourceEditName`, `GetSourceDeviceType(int, unsigned*, int)` |

The last `int` is unidentified; the cache holds several entries per source
type, so it may select one of them. `get_device_type(sourceType, 0)` returned
0 for every input, so that guess at its signature is wrong. Samsung's own
editor (`EditSourcePopupView` in `ImplViewDB.dll` of
`org.tizen.ConnectedDevicesNUIGadget`) calls `set_edit_name`.

## Editing names and types

Quiet Home does not write these settings. Its **Edit inputs** button opens
Samsung Home on its source list instead, by launching `com.samsung.tv.csfs`
with the extra `entry_point=source_key`, as the remote's Source key does.
Focus lands on the current input; hold Select on an input and choose Edit to
change its name or device type. Press Home to return; Quiet Home rereads the
list then.

QuickLaunch also accepts `launch_focus_app` with one of `None`, `VisionAI`,
`Color`, `ControlUI`, `ConnectedDevice`, `QuickSettings` or `SmartThings`. On
this S95F its panel has no Connected Devices button
(`quicklaunch.connecteddevice_support` is off), so `ConnectedDevice` fell
back to the Color panel. A model with that button may open the editor there.
On a TV where the settings bridge replaces `QuickLaunch.dll`, that path leads
to Quiet Home's settings instead.
