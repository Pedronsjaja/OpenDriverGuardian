# Driver discovery and download increment — requested Phase 3

## Discovery and matching

Inventory remains offline; Windows CompatibleIds is additive. Avalonia requires consent before discovery and explains WUA OS/device applicability traffic. The fixed Microsoft.Update.Session adapter searches noninstalled/nonhidden driver updates using `ssWindowsUpdate`. It never registers services, accepts licenses, creates downloaders/installers, alters policy or uploads the app's Snapshot. Policy/offline/timeouts become warnings. Live online WUA discovery is NOT VERIFIED in this increment.

WUA supplies DriverHardwareID, manufacturer, identity/title, estimated maximum size and DownloadContents URLs if exposed. Version remains UNKNOWN instead of parsed from titles. No URL, HTTP, unapproved hosts or multipart content means manual lookup, never guessed links or incomplete package download. Microsoft Catalog and NVIDIA/AMD/Intel have curated manual pages, not scraped APIs or device-specific OEM recommendations.

DriverMatching compares full hardware/compatible IDs/modalias, OS/version, architecture, kernel/build and an OEM constraint if supplied. It never broadens subsystem/revision IDs with substring matching. WUA confirms native applicability on the scanned OS session. OEM/subsystem applicability beyond IDs is delegated to WUA, not independently validated against INF contents. A match is not an instruction to install or proof of a driver being secure. Version ordering is never a matching criterion.

Ubuntu runs local `ubuntu-drivers devices` only if installed on Ubuntu. Exact modalias/package recommendations retain unverified provenance labels, including third-party entries. No APT refresh, package hash/signature verification or running-kernel/dependency resolution is performed. Candidates are unverified with no direct app download. Other distributions have inventory/manual guidance only. apt/dnf/pacman downloads, vendor APIs and firmware/LVFS are not implemented. Firmware remains separate.

## Transfer and verification

Direct URLs also require WUA EulaAccepted=true on this computer. Unaccepted/unknown licensing disables download and remains manual. The app never calls AcceptEula, bypasses authentication or replaces an HTTP URL with an assumed HTTPS variant.

DriverDownloads owns a nonredirecting HttpClient. Every URI must be HTTPS/443 without userinfo/fragment and an approved Microsoft content hostname. Each redirect is validated, at most five. No HTTP fallback or scheme replacement. Test-only internal transport rewrites synthetic official-shaped URIs to a loopback HTTP server; app/CLI cannot enable it.

Operations own generated directories in LocalApplicationData/OpenDriverGuardian/Downloads. Candidate names/IDs never become paths. Ancestor symlinks/junctions are rejected. Transfers stream into a new file, report actual bytes and Content-Length (or UNKNOWN), enforce 2 GiB/20 minute/cancellation limits and reject empty/truncated content. WUA maximum size is an estimate. SHA-256 is computed; only a trusted reference produces MATCHED. WUA currently supplies none: COMPUTED_ONLY_NO_TRUSTED_REFERENCE.

Windows system Get-AuthenticodeSignature examines the downloaded file without executing it. VALID requires a signer certificate. INVALID rejects/deletes content. Unknown/unsigned/unsupported results retain a quarantined `.unverified` file, never verified completion. Valid signatures produce DownloadCompleted/manual installation required, with independent hash/signature states. CAB-contained catalog/INF chains, dependencies, OEM policy, driver-code security and physical installation are not inspected. JSON journals record identity, initial/final URL, timestamp, outcome, hash and signer thumbprint if available, without complete inventory or absolute path. Journal failure is surfaced as storage failure.

User cancellation applies to discovery and transfer. No background job/install/execute/admin action exists. Browser actions open only curated HTTPS pages on a click. This is not a sandbox against malicious software already running as the user.

## Evidence

[Validation](../validation.md) distinguishes actual loopback transfers from mocked signature outcomes and live WUA/hardware validation. Tests cover matching, OS/arch/OEM/subsystem rejection, progress/hash/journal, cancellation/partial cleanup, truncation, redirects, unavailable/offline sources and verification failures. A separate Windows test checks a real system Authenticode signature and rejects a changed copy without executing it. These results do not establish live WUA download, successful hardware installation or graphical consent/file-picker/layout validation. The original 18 tests remain present.
