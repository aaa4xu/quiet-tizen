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

For local checks:

```sh
python3 -m unittest discover -s tests -v
python3 tools/build.py payloads
dotnet test tests/QuietHome.Core.Tests
```

NUI changes also need a build against matching private references and a
clearly stated hardware-test status. Keep attribution/license files with
derived assets and code.
