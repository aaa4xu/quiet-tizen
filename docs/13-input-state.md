# Is the device on? Input state from the TV

This note explains where a Samsung TV reports whether the device on an HDMI
input is on, and how Quiet Home shows it under the input cards. The findings
come from one TV, a QE65S95FAUXRU on `T-RSMFDEUC-0090-1301.0` (Tizen 9, 32-bit
ARM userland), with a PlayStation 5 on HDMI 2 (HDMI-CEC enabled) and a PC
without CEC on HDMI 3. No other model was tested. Every interface here is
private: check it on your own TV before relying on it.

All calls listed were read-only. Methods that change something (switching the
active source, sending keys, turning a receiver on or off, CEC line tests) were
not called.

## What Quiet Home shows

The line under an input's name gets the device's state:

| State | Shown | When |
|---|---|---|
| On | `HDMI 2 · On` | the source list's `powerState` is 1 |
| Standby | `HDMI 2 · Standby` | `powerState` is 2 and a CEC device answers on the port |
| No signal | `HDMI 3 · No signal` | `powerState` is 2 and nothing answers CEC |
| unknown | as before | Live TV, or any other value |

An input named after its port shows the state alone (`No signal`). The HDMI
port is shown only when the card's title is another name. Live TV keeps
"Broadcast channels".

The input on screen gets no preview while it is in standby or without a
signal: its card keeps the icon instead of a black rectangle.

Quiet Home rereads the input list every 1.5 seconds while Home is in front, and
the CEC devices on start, on resume and on the CEC service's notice. The
diagnostics file's `input_states=` line lists each card's state, for
example `input_states=0:Unknown,14:Standby,15:On`.

## Signal: the source list's `powerState`

`get_source_list()` in `libsource-api.so` (see
[the source-list findings](11-source-api.md)) already has a `powerState`
field for each input:

| Value | Meaning observed |
|---|---|
| 0 | Live TV |
| 1 | the device sends a signal |
| 2 | the device sends nothing: the PS5 in rest mode, the PC off |

It changed in the same second as the TV service's `CheckSourceStatus` (below)
and earlier than anything CEC reported. It works for a device without CEC.

Two caveats:

- **No notice.** The source-list change callback did not fire when a device
  turned on or off, only on plug, unplug and rename. The list has to be reread.
- **A console process saw stale values.** A .NET console probe run as root
  kept getting the old `powerState` for 20 seconds after the change, while a
  new process read the new one at once. Inside Quiet Home, which runs the
  app's main loop, the value was current at every read. A probe without a main
  loop is not a reliable witness for this field.

## Standby: the HDMI-CEC service

The TV's CEC service is `org.tizen.tv.hdmicec` on the system bus, object
`/org/tizen/tv/hdmicec`, interface `org.tizen.tv.HdmiCec`. Every method returns
its result code first. The read-only methods that answered:

| Method | Arguments | PS5 on HDMI 2 | Port without CEC |
|---|---|---|---|
| `IsCECDeviceConnected` | `int32 port` | 1, also in rest mode | 0 |
| `GetDevicePowerStatusByHdmiPort` | `int32 port` | 0 on, 1 in rest mode | -1 |
| `GetDeviceInfoByHdmiPort` | `string proc_name, int32 snid, int32 port` | name `PlayStation 5`, logical address 4, vendor 0x080046 (Sony) | empty name, address 15, vendor 0xFFFFFF |
| `GetActiveDeviceName` | none | `PlayStation 5` while it is the active source; empty in rest mode | |
| `GetDeviceCount` | `boolean is_receiver_included` | 1 | |
| `GetDeviceName`, `GetDevicePortNum`, `GetDeviceType` | `int32 index` | `PlayStation 5`, port 2, `PLAYBACK_DEVICE1` for index 0 | -1 past the last device |
| `IsEnable` | none | 1 | |

The power values follow the CEC standard's `<Report Power Status>`: 0 on,
1 standby; 2 and 3 (the transitions) were not seen. The port number is the
HDMI number; Quiet Home maps port *n* to source type *n* + 12 (HDMI 1 is 13,
as in [the preview findings](12-hdmi-preview.md)).

The service emits these signals, among others: `UpdateCecDeviceList`,
`DeviceSearchComplete`, `ClearScreen`, `ReceiverInfoChanged`,
`SecondDepthConnectionStatusChanged`. The ones that came during the tests are
in the timeline below.

Quiet Home calls it through GIO (`libgio-2.0.so.0`, `g_dbus_connection_call_sync`
and `g_dbus_connection_signal_subscribe`). From the app's own process (the app
user, not root) both worked, and the signal arrived on the UI thread.

## The TV service

The same `Tizen.TV.Broadcast.TVService` assembly the preview uses has more:

- `ISourceControl.CheckSourceStatus(List<Source>, ref SortedDictionary<Source, SourceStatus>)`
  gives `Available` or `NotAvailable` for several inputs at once. The
  dictionary must be passed in already created; `null` throws inside the
  library. The answers matched `powerState` (1 is `Available`) at every read.
  Quiet Home does not need it, since `powerState` comes with the list it
  already reads.
- `ISourceControl.GetSourceState(ref SourceState)` covers only the input on
  screen: `SignalOk` for the PS5. Right after the TV switched to the PC it read
  `NoSignal` and then `Setting` for about 15 seconds while the picture was
  already up, so it is not a good "is it on" signal.
- `IAVControl.GetResolution(ref Resolution, ResolutionInformation)` describes the
  input on screen: `Resolution2160P60HZ`, 3840 × 2160, `frequency` 6000 (60 Hz)
  and `hdcpType` 2 for the PS5; `Resolution3840X2160PVRR` and HDCP 0 for the PC.
  It returned 0 rather than 1 while filling in the values.
- The signals `SignalExtSourceSignalOk`, `SignalExtSourceNoSignal`,
  `SignalExtSourceUnplug`, `SignalSourceStateChanged` and their relatives
  subscribed successfully but never arrived when the PS5 turned on or off, in a
  console probe or in the app.

## vconf

`db/source-service/detection_state_HDMIn` reads `detected,autodetection`,
`detected,mbr` or `not_detected,mbr`. It says whether a device was ever
detected on the port, not whether it is on: it did not change with power.

## Timeline

Times are relative to the first change; when the button was pressed was not
recorded. From the in-app check, reading every second:

| | PS5 turns on | PS5 goes to rest mode |
|---|---|---|
| `powerState` and `CheckSourceStatus` change | 0 s | 0 s |
| `UpdateCecDeviceList` (with `DeviceSearchComplete` on waking) | +1.2 s | +3.3 s |
| `GetDevicePowerStatusByHdmiPort` changes | about +6 s | +3.4 to +4 s |
| `ClearScreen` (twice) | about +5 s | none |

In one waking run the TV switched to HDMI 2 by itself: the PS5 asked for it
over CEC. The CEC status lags the picture by several seconds on waking.
This is why Quiet Home lets the signal decide and uses CEC only to tell
standby from off.

## Checking a TV

With temporary root (see [access](02-access.md)), these read-only calls show the
state of port 2:

```sh
dbus-send --system --print-reply --dest=org.tizen.tv.hdmicec /org/tizen/tv/hdmicec \
  org.tizen.tv.HdmiCec.IsCECDeviceConnected int32:2
dbus-send --system --print-reply --dest=org.tizen.tv.hdmicec /org/tizen/tv/hdmicec \
  org.tizen.tv.HdmiCec.GetDevicePowerStatusByHdmiPort int32:2
```

On the TV itself, `input_states=` in `/tmp/quiet-home-state.txt` shows what
Quiet Home decided for each card.

## Not established

- Any other model or firmware, any port above HDMI 4, and receivers on eARC.
- Whether `IsCECDeviceConnected` stays 1 after a device loses mains power.
- What `powerState` reports for a device that sends a signal the TV cannot
  show, and what the other source-list fields (`editState`, `settingMethod`,
  `mbrActivityIndex`) mean.
