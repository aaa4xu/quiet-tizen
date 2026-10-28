# Identify the TV before changing it

The supported case study is the **75-inch Samsung U8000F,
UN75U8000FFXZA**, firmware **T-KSU2EFAKUC-0090-1300.9**. The model code came
from the television's About screen, not an Amazon listing or an inferred
marketing name. That is the authoritative starting point for another TV too.

Open Settings → Support → About This TV. Record model and software version
privately. Do not publish the serial number, MAC address, device ID or QR code
from that screen. Write down the TV's local IP from its network settings and
the computer's current LAN IPv4 address.

The observed platform was Tizen 9.0, Linux 5.4.261, ARMv7 softfloat and .NET
6.0.9. Similar year/model names can have different chipsets, framework
assemblies, APIs and app containers.

Ask which features matter if the owner has not already said. Our setup
prioritized HDMI, streaming shortcuts and native Bluetooth. Voice assistants,
context suggestions and several background smart features were unwanted.
Another owner may need them.

After [access setup](02-access.md), collect a private inventory:

```sh
tv exec --command 'uname -srmo; readlink -f /usr/share/dotnet.tizen/netcoreapp'
tv exec --command 'sqlite3 -readonly /opt/dbspace/.pkgmgr_parser.db ".schema package_app_info"'
tv exec --command 'sqlite3 -readonly -header -csv /opt/dbspace/.pkgmgr_parser.db "SELECT app_id,app_disable,app_onboot,app_autorestart FROM package_app_info ORDER BY app_id;"'
tv exec --command 'sqlite3 -readonly /opt/dbspace/.app-context.db ".schema key_filter"'
tv exec --script recipes/status.sh
```

`tv` is the shell helper defined in the access guide. Raw command results
are automatically stored in ignored `.local/sessions/`.

Record current Home/startup preferences, then use the compiled Probe described
in the launcher guide to read the main-operation Home handler and actual input
source list. On our TV, HDMI 1/2/3 mapped to 13/14/15. **Do not infer that mapping
for another model.** Quiet Home builds its input cards from the TV's own
source list; check that list on the owner's TV.

Inventory service unit definitions before creating stop markers:

```sh
tv exec --command 'systemctl cat pisa-service.service canalysis-service.service'
```

A stop marker works only if the exact vendor unit contains the corresponding
negated `ConditionPathExists`. Guessing a path based on a process name is not
enough. The [profile](../profiles/un75u8000ffxza-1300.9.json) describes observed
targets, not authorization to change every entry.

Samsung's [product page](https://www.samsung.com/us/tvs/uhd-4k-tv/u8000f-75-inch-crystal-uhd-4k-smart-tv-un75u8000ffxza/)
provides the model's consumer specifications. Inspect the real device for
firmware-specific implementation details.
