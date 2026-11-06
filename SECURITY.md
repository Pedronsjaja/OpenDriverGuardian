# Security and privacy

Read-only MVP: no download/install/remove/update, firmware operations, elevation request, network/catalog lookup, telemetry, microphone stream or personal-file analysis. Process execution is confined to a fixed embedded Windows collector through the system PowerShell path with explicit arguments, timeout and bounded output. It does not weaken execution policy. Linux reads documented text attributes only.

Do not commit full real inventories. The collector excludes requested serial/MAC/account fields and hashes Windows instance IDs, but names and hardware IDs can still identify host configuration. Export requires a user action and should be reviewed before sharing. HTML escapes external labels; CLI refuses overwrites. A GUI Save dialog can replace a user-selected existing report after the platform's confirmation.

IsSigned is CIM metadata, not an Authenticode chain guarantee. Code 0 is no OS problem reported, not compatibility certification. Unknown data stays null/UNKNOWN. No database or background retention is created. Future official catalog access, signatures, safe install and rollback require separate review, per-operation approval and VM validation. Firmware must not share a generic install path.

Report security concerns privately to the maintainer through GitHub contact options where available. Do not publish credentials, reports or exploit details in public issues. No response SLA is claimed.
