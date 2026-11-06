$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
# Explicit online action only. Do not register a service, alter policy, accept EULAs or create a downloader/installer.
$session = New-Object -ComObject Microsoft.Update.Session
$session.ClientApplicationID = 'OpenDriverGuardian'
$searcher = $session.CreateUpdateSearcher()
$searcher.ServerSelection = 2 # ssWindowsUpdate; fail rather than silently accepting an arbitrary WSUS content source.
$searcher.Online = $true
$result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'")
if ([int]$result.ResultCode -ne 2) { throw 'Windows Update search incomplete' }
$rows = @(foreach ($update in $result.Updates) {
    # Some applicable updates expose no downloadable content until a later workflow. Keep them manual.
    $urls = @()
    try { $urls = @(foreach ($content in $update.DownloadContents) { if ($content.DownloadUrl) { [string]$content.DownloadUrl } }) }
    catch { $urls = @() }
    $licenseAccepted = $false
    try { $licenseAccepted = [bool]$update.EulaAccepted } catch { }
    [ordered]@{
        Id = [string]$update.Identity.UpdateID; Title = [string]$update.Title
        Manufacturer = [string]$update.DriverManufacturer; HardwareId = [string]$update.DriverHardwareID
        SizeBytes = [long]$update.MaxDownloadSize; Urls = $urls
        EulaAccepted = $licenseAccepted
    }
})
ConvertTo-Json -InputObject $rows -Depth 6 -Compress
