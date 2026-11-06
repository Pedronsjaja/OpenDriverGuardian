# Security and privacy

Direct WUA download additionally requires EulaAccepted=true; unknown/unaccepted license state is manual. The app never calls AcceptEula or bypasses authentication/licensing restrictions.

Inventory remains offline and read-only. No install/remove/update, firmware, elevation, telemetry, microphone stream or personal-file analysis. WUA discovery is an explicit consented online action; Microsoft may receive OS/device applicability information. Fixed embedded scripts use system PowerShell with bounded output/timeouts and no execution-policy changes. Signature validation explicitly imports the system security module. Ubuntu runs only `/usr/bin/ubuntu-drivers devices`, without sudo, repository refresh or installation.

Do not commit full real inventories. The collector excludes requested serial/MAC/account fields and hashes Windows instance IDs, but names and hardware IDs can still identify host configuration. Export requires a user action and should be reviewed before sharing. HTML escapes external labels; CLI refuses overwrites. A GUI Save dialog can replace a user-selected existing report after the platform's confirmation.

IsSigned is CIM metadata, not an Authenticode chain guarantee. Code 0 is no OS problem reported, not compatibility certification. Unknown stays null/UNKNOWN. Download journals contain no complete inventory or absolute local path. Safe installation/rollback need separate review and VM validation; firmware must not share a generic install path.

Downloads require confirmed WUA applicability and an approved official HTTPS/443 content host at every redirect (five-hop cap). No cookies, automatic redirects, insecure TLS callbacks or caller-selected package paths. Transfers use generated directories, reject symlink/junction ancestors, enforce 2 GiB/20 minute limits, remove partial/bad payloads and never execute content. Internal test transport injection is unavailable in app/CLI settings. The library is not a sandbox against malicious code running as the same user.

SHA-256 is compared only with a trusted reference (the WUA adapter currently supplies none). Windows Authenticode invalid results reject content; unsigned/unsupported/unknown results quarantine it as `.unverified`. Valid Authenticode is not INF/dependency/OEM policy or physical installation verification. No CAB extraction/catalog reconstruction, offline revocation guarantee or malware scan is claimed. Journals record source, outcome, hash/signature states and signer thumbprint when available.

Report security concerns privately to the maintainer through GitHub contact options where available. Do not publish credentials, reports or exploit details in public issues. No response SLA is claimed.
