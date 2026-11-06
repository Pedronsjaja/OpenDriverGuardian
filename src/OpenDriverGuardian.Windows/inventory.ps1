$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$warnings = [Collections.Generic.List[string]]::new()
function Read-Cim($class) {
    $properties = switch ($class) {
        'Win32_OperatingSystem' { @('Version','BuildNumber','TotalVisibleMemorySize') }
        'Win32_BaseBoard' { @('Manufacturer','Product') }
        'Win32_BIOS' { @('SMBIOSBIOSVersion') }
        'Win32_Processor' { @('Name') }
        'Win32_PnPSignedDriver' { @('DeviceID','DriverVersion','DriverProviderName','InfName','IsSigned') }
        'Win32_PnPEntity' { @('Present','Name','PNPClass','Manufacturer','HardwareID','ConfigManagerErrorCode','PNPDeviceID','Service') }
        default { throw 'Unsupported CIM source' }
    }
    try { @(Get-CimInstance -ClassName $class -Property $properties -ErrorAction Stop) }
    catch { $warnings.Add("CIM source unavailable: $class"); @() }
}
$os = @(Read-Cim 'Win32_OperatingSystem') | Select-Object -First 1
$board = @(Read-Cim 'Win32_BaseBoard') | Select-Object -First 1
$bios = @(Read-Cim 'Win32_BIOS') | Select-Object -First 1
$cpus = @(Read-Cim 'Win32_Processor')
$signed = @{}
foreach ($s in @(Read-Cim 'Win32_PnPSignedDriver')) {
    if ($s.DeviceID) { $signed[$s.DeviceID] = $s }
}
$devices = @(foreach ($p in @(Read-Cim 'Win32_PnPEntity')) {
    if ($p.Present -eq $false) { continue }
    $s = $signed[$p.PNPDeviceID]
    # Do not export instance IDs, USB serials, system/user names or network addresses.
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $key = [BitConverter]::ToString($hash.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]$p.PNPDeviceID))).Replace('-','').ToLowerInvariant() }
    finally { $hash.Dispose() }
    $problem = if ($null -eq $p.ConfigManagerErrorCode) { $null } else { [int]$p.ConfigManagerErrorCode }
    [ordered]@{
        Key = $key; Name = [string]$p.Name; Category = [string]$p.PNPClass
        Manufacturer = $p.Manufacturer; HardwareIds = @($p.HardwareID | Where-Object { $_ })
        Driver = $p.Service; DriverVersion = $s.DriverVersion; DriverProvider = $s.DriverProviderName
        InfPath = $s.InfName; IsSigned = $s.IsSigned; ProblemCode = $problem
        State = $(if ($null -eq $problem) { 'unknown' } elseif ($problem -eq 0) { 'no-os-problem-reported' } else { 'os-problem-reported' })
        Evidence = 'Win32_PnPEntity + Win32_PnPSignedDriver'
    }
})
if ($devices.Count -eq 0) { $warnings.Add('No PnP devices returned; inventory is incomplete.') }
$memory = if ($null -ne $os.TotalVisibleMemorySize) { [uint64]$os.TotalVisibleMemorySize * 1024 } else { $null }
[ordered]@{
    CapturedAt = [DateTimeOffset]::UtcNow.ToString('O'); SchemaVersion = '1.0'
    System = [ordered]@{ Os = 'Windows'; OsVersion = $os.Version; Architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString(); Kernel = $os.BuildNumber; Cpu = (($cpus | ForEach-Object { $_.Name }) -join '; '); Board = "$($board.Manufacturer) $($board.Product)".Trim(); Bios = $bios.SMBIOSBIOSVersion; MemoryBytes = $memory; Distribution = $null }
    Devices = $devices; Warnings = @($warnings)
} | ConvertTo-Json -Depth 7 -Compress
