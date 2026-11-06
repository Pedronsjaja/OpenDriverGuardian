# Phases and acceptance gates

Requested Phase 3 increment: WUA adapter, exact matching, bounded official HTTPS transfers, Authenticode/journal and bilingual GUI are implemented. Ubuntu provides local guidance only. Live WUA discovery/download, graphical interaction, OEM INF/catalog verification and distro package managers remain gates. See [scope](../architecture/driver-discovery.md). Original research tracks below retain their order; installation/rollback and firmware remain deferred.

1. Research/architecture: original DTOs, license audit, ADRs and compilable projects. Initial implementation present.
2. Inventory MVP: Windows structured CIM and Linux PCI/USB, CLI scan, JSON, UI, unit fixtures. Validate each platform; capture anonymized evidence. ARM64/cross-publish are not real ARM64 testing.
3. Diagnostics: SetupAPI/Configuration Manager coverage, device present/configuration semantics, detailed DriverStore association and Linux platform buses. Acceptance: known cases, denied sources, stale/disappearing devices and no false missing-driver classification.
4. Audio Diagnostics: Windows Core Audio endpoints/defaults and read-only Linux ALSA/PipeWire/PulseAudio service/routing inventory. Playback/recording requires a separate explicit consent flow, no background capture. Endpoint availability is not controller health.
5. Recommendations: supported Windows Update/distro catalog queries; exact hardware/OEM/subsystem, architecture/build/kernel applicability, trust/provenance/signature rules. Offline/failure cases must not fabricate candidates or URLs. Provide manual official-source fallback when unsupported.
6. Safe Install: separate administrative boundary, per-install approval, verified package and signed chain, risk display, backup/rollback when supported, VM failure/recovery tests. Firmware separate high-risk flow. No installer in current MVP.
7. Release: tested Windows/Linux x64 graphical environments; x64/ARM64 packaging matrix with actual runtime evidence per supported target; PDF export, historical comparison/SQLite only with a concrete privacy/retention need. No production-ready certification claim.

Study: OS hardware enumeration and ID semantics → driver association versus health → sysfs binding and distro policy → compatibility/trust → UI threading/cancellation/export safety. Challenge: explain why a missing binding, signed metadata or newer version cannot independently justify installation.
