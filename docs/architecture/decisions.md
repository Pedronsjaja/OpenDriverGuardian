# Decisions

## ADR 001 — Original read-only MVP (accepted 2026-11-06)
Problem: broad updater scope would make unsafe compatibility and installation claims. Constraints: native data, vendor neutrality, offline inventory and explicit privacy. Alternatives: integrate existing updater, query third-party driver packs, or original structured collectors. Decision: original native collectors and shared DTOs; dependencies only Avalonia/test frameworks. No update/download/install interfaces until verified candidate sources and administrative design exist. Consequence: explainable inventory now; advanced diagnostics remain limited. Reference licenses do not establish device coverage.

## ADR 002 — Native adapters (accepted)
Windows uses structured CIM properties via an embedded fixed read-only PowerShell command, not localized pnputil tables. SetupAPI/Configuration Manager can replace it if measured needs justify the P/Invoke surface. No execution-policy weakening. Linux uses documented proc/sysfs text, independent of optional distro commands. Consequence: no lspci/lsusb requirements, but coverage is limited to exposed PCI/USB buses and available DMI. Partial failures remain warnings. Test sources can be injected without pretending they are live hardware.

## ADR 003 — Toolchain and persistence (accepted)
.NET10 LTS is supported per Microsoft policy checked 2026-11-06; NuGet provided Avalonia12.1.5. Exact versions and lockfiles are committed. SQLite is deferred because scan has no durable history requirement yet; JSON/HTML exports are explicit. PDF, endpoint-level audio, catalog queries and ARM64 runtime validation have their own backlog gates. Cross-compilation alone is not validation.
