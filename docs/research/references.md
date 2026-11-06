# Sources and comparative audit

Checked 2026-11-06. Source code was inspected for licensing/purpose; external applications were not executed and no code/assets/driver packs were copied. Maintenance observations are point-in-time, not quality/security guarantees.

## Primary API/platform sources
- [DriverHardwareID](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iwindowsdriverupdate-get_driverhardwareid): hardware or compatible ID required for installability; compare full IDs, not product-name/version heuristics.
- [DownloadContents](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdate-get_downloadcontents) and [DownloadUrl](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdatedownloadcontent-get_downloadurl): documented content enumeration; URL availability does not establish HTTPS, completeness or signature validity.
- [Get-AuthenticodeSignature](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/get-authenticodesignature?view=powershell-5.1): native signature status, not OEM compatibility. Import the system module explicitly to avoid inherited module-path conflicts.
- [Ubuntu driver guidance](https://ubuntu.com/server/docs/nvidia-drivers-installation): distro-specific selection; local recommendations are not independent package trust verification. No GPL implementation copied.
- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy): toolchain lifecycle; does not prove application OS coverage.
- [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms): graphical/native prerequisites and vendor tiers; project validation is separately recorded.
- [Win32_PnPEntity](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity): HardwareID, Present, ConfigManagerErrorCode. Codes are OS observations, not inferred root causes.
- [Win32_PnPSignedDriver](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpsigneddriver): associated driver metadata; IsSigned does not replace independent chain validation.
- [Linux PCI sysfs](https://docs.kernel.org/PCI/sysfs-pci.html): IDs/subsystem and class text attributes; writable controls and resource memory are excluded.

## Project comparisons
| Reference | Verified purpose / technology | License source | Useful lesson and limit |
|---|---|---|---|
| [OpenDriverUpdater](https://github.com/OpenDriverUpdater/OpenDriverUpdater) | C#/.NET8 WinUI updater, WMI/PnP | [MIT](https://github.com/OpenDriverUpdater/OpenDriverUpdater/blob/main/LICENSE) | Layer separation; updater claims not validated here |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | C# sensors/monitoring; some reads require privileges | [MPL-2.0](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE) plus third-party notices | Optional future monitoring; not universal Linux/device coverage |
| [DriverStoreExplorer](https://github.com/lostindark/DriverStoreExplorer) | Windows C#/.NET Framework DriverStore tooling | [GPLv2](https://github.com/lostindark/DriverStoreExplorer/blob/master/LICENSE), per-file only/or-later scope not fully audited | Installed/unbound is not safe-to-remove; no code integration |
| [ubuntu-drivers-common](https://github.com/canonical/ubuntu-drivers-common) | Python, Ubuntu/APT modalias selection | [GPLv2](https://github.com/canonical/ubuntu-drivers-common/blob/master/COPYING), per-file scope not fully audited | Distro-specific selection, not generic Linux matching |
| [fwupd](https://github.com/fwupd/fwupd) | C daemon/tools for supported firmware/LVFS | [LGPLv2.1](https://github.com/fwupd/fwupd/blob/main/COPYING), per-component terms require review | Firmware separate from driver; no update/refresh/upload invoked |
| [alsa-utils](https://github.com/alsa-project/alsa-utils) | C ALSA tools | [GPLv2](https://github.com/alsa-project/alsa-utils/blob/master/COPYING), per-component terms require review | Read-only enumeration must be separate from audio capture/playback/mixer writes |
| [Snappy Driver Installer Origin](https://sourceforge.net/projects/snappy-driver-installer-origin/) | C/C++ Win32 driver installation/packs | [GPL-3.0-or-later source header](https://sourceforge.net/p/snappy-driver-installer-origin/code/HEAD/tree/trunk/source/license.cpp); other SourceForge licensing scope unclear | Identification lessons only; driver redistribution rights need independent review |

All six GitHub references were public/non-archived. Last default-branch commits observed: OpenDriverUpdater 5f34eaa (2026-11-06); LibreHardwareMonitor 2808d80 (2026-11-03); DriverStoreExplorer 1be602d (2026-11-06); ubuntu-drivers-common ffb948b (2026-10-07); fwupd 4c11570 (2026-11-06); alsa-utils f3537ed (2026-09-16). SDIO tree reported r889 (2026-10-18). No conclusion about safety or compatibility is derived from activity alone.

NVIDIA Skill Finder catalog was checked using its list-only workflow; no vendor-specific dependency or new skill was needed for an OS-native inventory MVP. No NVIDIA skill was installed. No hardware serial or user report was sent to a vendor.
