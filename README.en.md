# OpenDriver Guardian

A local-first, read-only hardware inventory MVP for Windows and Linux, built with .NET 10 and Avalonia. [Português](README.md).

Windows uses structured CIM JSON for OS/build, CPU, motherboard, BIOS, memory, PnP hardware IDs and associated signed-driver metadata. Linux reads documented proc/sysfs/os-release attributes for PCI/USB IDs, subsystem IDs, modalias and driver bindings. Missing/denied sources produce explicit warnings. The scanner is vendor-neutral; this does not establish validation on every Intel, AMD or NVIDIA device or distribution.

The CLI implements `scan`, JSON and HTML export. The desktop UI exposes the same real inventory, explainable observations, an audio-device subset, explicit exports and Portuguese/English labels. Audio routing, default endpoints, services, playback/recording tests, PDF, SQLite history, update catalogs, compatible-candidate matching, downloads, installation and rollback remain planned. No microphone access, driver changes, background telemetry or automatic inventory upload.

```bash
dotnet restore OpenDriverGuardian.sln
dotnet build OpenDriverGuardian.sln -c Release --no-restore
dotnet test OpenDriverGuardian.sln -c Release --no-build
dotnet run --project src/OpenDriverGuardian.Cli -- scan --output report.json --html report.html
dotnet run --project src/OpenDriverGuardian.App
```

Use .NET SDK 10.0.403 or an allowed patch from global.json. CLI exports refuse existing filenames. Exit codes: 0 collection without warnings; 1 collected with partial/scope warnings; 2 failure; 3 cancelled/timed out. A warning does not establish hardware failure. Linux UI needs a graphical session and [Avalonia native dependencies](https://docs.avaloniaui.net/docs/supported-platforms). Offline inventory requires the application/runtime to be installed first.

Windows code 28 means the OS reports a missing driver; code 22 means disabled, not necessarily missing. Other codes are reported without inventing a root cause. A Linux device without a bound module does not prove a required driver is missing. A higher version alone is not evidence of compatibility. `IsSigned` is OS metadata, not an independent Authenticode chain verification. No health percentage is fabricated.

Do not share real reports without review: even without requested serial numbers/accounts/MACs, hardware IDs and labels can reveal configuration. No external source code or driver pack was copied. [Architecture](docs/architecture/overview.md), [research](docs/research/references.md), [validation](docs/validation.md), [roadmap](docs/roadmap/backlog.md), [security](SECURITY.md), [MIT](LICENSE).
