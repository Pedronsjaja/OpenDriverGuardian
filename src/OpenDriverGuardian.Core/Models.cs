using System.Runtime.InteropServices;
namespace OpenDriverGuardian.Core;
public interface IInventoryScanner { Task<Snapshot> ScanAsync(CancellationToken cancellationToken = default); }
public sealed record Device(string Key, string Name, string Category, string? Manufacturer, string[] HardwareIds,
    string? Driver, string? DriverVersion, string? DriverProvider, string? InfPath, int? ProblemCode, string State, string Evidence)
{
    public bool? IsSigned { get; init; }
    public string[] CompatibleIds { get; init; } = [];
}
public sealed record SystemInfo(string Os, string OsVersion, string Architecture, string Kernel,
    string? Cpu, string? Board, string? Bios, ulong? MemoryBytes, string? Distribution);
public sealed record Snapshot(DateTimeOffset CapturedAt, SystemInfo System, IReadOnlyList<Device> Devices,
    IReadOnlyList<string> Warnings, string SchemaVersion = "1.0");
public sealed record Finding(string DeviceKey, string Classification, string Reason, string Risk, string Source, string Action);

public static class Diagnostics
{
    public static Finding Assess(Device device)
    {
        const string source = "https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-pnpentity";
        return device.ProblemCode switch
        {
            28 => new(device.Key, "DriverMissing", "Windows reports configuration-manager code 28.", "Review", source, "Check Windows Update, then the exact OEM device support page."),
            22 => new(device.Key, "ManualIntervention", "Windows reports code 22: device disabled; a missing driver is not established.", "Review", source, "Review whether the device was intentionally disabled in Device Manager."),
            > 0 => new(device.Key, "DeviceProblem", $"Windows reports problem code {device.ProblemCode}; root cause is not established.", "Review", source, "Inspect Device Manager details before changing drivers."),
            _ => new(device.Key, "CompatibilityUnverified", device.State == "no-binding-observed"
                ? "No driver binding observed; some devices do not require a dedicated driver."
                : "No compatible official update candidate has been queried. Installed version alone is insufficient.",
                "Informational", device.Evidence, "Keep the current configuration unless an identified issue or verified official candidate justifies change.")
        };
    }
    public static bool IsAudio(Device d) => d.Category.Contains("audio", StringComparison.OrdinalIgnoreCase)
        || d.Category.Equals("MEDIA", StringComparison.OrdinalIgnoreCase) || d.Category.Equals("sound", StringComparison.OrdinalIgnoreCase)
        || d.Category.StartsWith("PCI class 0401", StringComparison.OrdinalIgnoreCase)
        || d.Category.StartsWith("PCI class 0403", StringComparison.OrdinalIgnoreCase)
        || d.Category.Equals("USB class 01", StringComparison.OrdinalIgnoreCase);
}
