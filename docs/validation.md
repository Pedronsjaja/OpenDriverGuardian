# Validation evidence — initial MVP

2026-11-06. Local host: Windows x64; isolated official .NET SDK 10.0.403 (SDK ZIP SHA-512 checked against Microsoft release metadata); Avalonia 12.1.5. Runtime source uses C# and native OS observations; no Python dependency. Python was only used by the development agent to prepare files.

## Executed locally
- dotnet build OpenDriverGuardian.sln -c Release --no-restore: passed, zero warnings/errors; SDK analyzers and warnings-as-errors enabled.
- dotnet test OpenDriverGuardian.sln -c Release --no-build: 18 passed, 0 failed, 0 skipped. Includes code28/22 classification, unbound-not-missing, null/JSON roundtrip, HTML escaping, audio classes, cancellation, Linux synthetic PCI/USB and missing sources, atomic export preserving old output and anonymized real Windows schema.
- CLI scan with explicit JSON/HTML filenames outside the repository: 213 devices, 9 Windows MEDIA/AudioEndpoint records, 0 collection warnings, 0 reported nonzero PnP problem codes. This does not prove that every device is healthy or an update is compatible. Full private reports were not committed.
- A single PCI display observation was anonymized into tests/OpenDriverGuardian.Tests/Fixtures/windows-device.json. Host CPU/board/BIOS/memory removed and instance-derived key replaced; no USB instance/serial retained. Other fixtures are explicitly synthetic and do not prove Linux execution.
- Desktop process launched and accessibility tree exposed all six tabs and scan button. Native automation could not activate/click/render the window (unavailable coordinate geometry; capture returned desktop wallpaper). Therefore interactive scan, file picker and visual layout are NOT VERIFIED on this host. CLI successfully exercises the shared scanner. No misleading GUI screenshot is provided as proof.

## Regression findings addressed
- Linux audio filtering now recognizes PCI/USB audio class codes.
- Atomic local exports preserve existing reports on failure; tests cover overwrite refusal/cancellation cleanup.
- Windows collector requests an explicit property allowlist, not whole CIM class properties.
- Output overflow terminates the fixed collector child rather than leaving a full pipe until timeout.
- Main UI has an outer scroll container for minimum window sizes.

CI performs locked restore, build, tests and a real native scan on Windows and Ubuntu. Private raw runner inventories are not uploaded; only counts, build artifacts and test results are retained. Check GitHub Actions for current executed results; merely defining a workflow is not a passing Linux test. ARM64 and graphical Linux execution are NOT TESTED. Windows/GPU/OEM/distro coverage is not universal.

## Reproduce
Use README commands with .NET10 SDK. Scan requires no elevation. Choose fresh report filenames. Inspect a returned problem code without changing device state. For real Linux, require at least one observed PCI/USB device and identify collection warnings. VM/container visibility can differ from the physical host. The initial Linux scanner intentionally reports its scope warning and can return exit1; this is not an installation failure.
