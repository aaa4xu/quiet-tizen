# Contributing

Useful contributions are new compatibility reports, corrections to observed
behavior, small reviewed recipes and improvements to the native launcher.

For a new model, report its exact model/firmware, architecture/runtime, which
steps were exercised, what was checked after full boot and standby/wake, and
how restoration was verified. A successful preflight is not a successful
root session; a root session is not a validated launcher install.

Use a separate profile rather than widening the existing profile's model
gate. Preserve the owner-selected features and take fresh rollback state.
Include sanitized observations and the corresponding tests.

Do not attach serial numbers, MAC/IP addresses, device/account IDs, original
app assemblies, SDK binaries, database snapshots or raw session logs.
Review screenshots for viewing history, profile names and other personal
information before sharing them.

For local checks (inside `nix develop` when you use Nix):

```sh
python3 -m unittest discover -s tests -v
python3 tools/build.py payloads
dotnet test tests/QuietHome.Core.Tests
dotnet format launcher/core/QuietHome.Core.csproj --verify-no-changes
dotnet format tests/QuietHome.Core.Tests --verify-no-changes
```

CI runs the same checks. To fix the formatting, run `dotnet format` on
the project without `--verify-no-changes`.

For Quiet Home's code:

- Read "How the code is organized" in
  [docs/04-launcher.md](docs/04-launcher.md#how-the-code-is-organized). It
  shows the layers and where a new Settings tile, action or page goes.
- Put behaviour in `launcher/core`, behind the ports, not in the NUI views.
- Add a scenario to `tests/QuietHome.Core.Tests/Scenarios` for each change in
  what the remote does.
- New or rewritten C# needs a comment on every class and interface
  explaining its place in the architecture, and on every method saying what
  it does; see "C# code" in AGENTS.md.

NUI changes also need a build against matching private references and a
clearly stated hardware-test status. Keep attribution/license files with
derived assets and code.
