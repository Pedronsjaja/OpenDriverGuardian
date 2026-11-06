# Initial diagnosis and plan

2026-11-06. New repository; local .NET 10 runtime exists but SDK is missing. Read-only inventory is the initial boundary. Critical: do not infer compatibility/update availability from version or missing bindings; never mutate drivers or record audio. High: validate Windows CIM and Linux sysfs on their own platforms; sanitize identifiers and export paths. Medium: show partial collection and explicit unknowns. Low: history/database and extra views after the scan contract is stable.

Architecture: Core models/diagnostics/JSON+HTML reports; separate Windows and Linux inventory adapters; CLI and Avalonia UI consume the same scanner. No SQLite until durable history has a concrete need. No installer/downloader in the MVP. .NET 10 and Avalonia versions are pinned after verifying official compatibility and NuGet availability. Detailed projects/backlog to follow working implementation.

Validation: real Windows inventory, sanitized device fixtures, unit tests and Linux CI real scan. Linux fixtures are explicitly synthetic until Linux CI supplies real evidence. Desktop UI requires manual launch verification. Cross-publishing is not runtime testing on ARM64. Keep research licenses and external code separate; no third-party driver code copied.
