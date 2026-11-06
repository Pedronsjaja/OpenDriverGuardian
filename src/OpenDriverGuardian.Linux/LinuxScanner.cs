using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OpenDriverGuardian.Core;

namespace OpenDriverGuardian.Linux;

/// <summary>
/// Read-only PCI/USB inventory from documented text attributes. A missing binding is
/// an observation, not evidence that a driver is required, compatible, or missing.
/// Other buses and devices hidden by permissions/namespaces are outside this scan.
/// </summary>
public sealed class LinuxScanner : IInventoryScanner
{
    private const int MaxDevicesPerBus = 8192;
    private const int MaxTextCharacters = 65536;
    private readonly string root;

    public LinuxScanner(string root = "/")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        this.root = Path.GetFullPath(root);
    }

    public Task<Snapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        var devices = new List<Device>();
        string? Read(string path) => ReadText(path, warnings, cancellationToken);
        var release = ParseAssignments(Read(At("etc/os-release")));
        var cpuText = Read(At("proc/cpuinfo"));
        var memoryText = Read(At("proc/meminfo"));
        string? cpu = null;
        foreach (var line in (cpuText ?? "").Split('\n'))
        {
            var pair = line.Split(':', 2);
            if (pair.Length == 2 && pair[0].Trim() is "model name" or "Processor" or "Hardware")
            {
                cpu = Clean(pair[1]);
                if (cpu is not null) break;
            }
        }

        ulong? memory = null;
        foreach (var line in (memoryText ?? "").Split('\n'))
        {
            if (!line.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && parts[2] == "kB" &&
                ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kib) &&
                kib <= ulong.MaxValue / 1024) memory = kib * 1024;
            break;
        }

        var kernel = Clean(Read(At("proc/sys/kernel/osrelease"))) ?? "UNKNOWN";
        var system = new SystemInfo("Linux", release.GetValueOrDefault("VERSION_ID") ?? "UNKNOWN",
            RuntimeInformation.OSArchitecture.ToString(), kernel, cpu,
            Join(Read(At("sys/class/dmi/id/board_vendor")), Read(At("sys/class/dmi/id/board_name"))),
            Join(Read(At("sys/class/dmi/id/bios_vendor")), Read(At("sys/class/dmi/id/bios_version")),
                Read(At("sys/class/dmi/id/bios_date"))),
            memory, release.GetValueOrDefault("PRETTY_NAME") ?? release.GetValueOrDefault("NAME"));

        ScanBus("pci", devices, warnings, cancellationToken);
        ScanBus("usb", devices, warnings, cancellationToken);
        warnings.Add("Scope: read-only PCI/USB sysfs observations; names may be identifier labels. " +
            "No binding does not prove a missing driver. Compatibility, updates and other buses are not assessed.");
        if (cpu is null) warnings.Add("CPU description unavailable from /proc/cpuinfo.");
        if (memory is null) warnings.Add("Memory total unavailable from /proc/meminfo.");
        if (kernel == "UNKNOWN") warnings.Add("Kernel release unavailable from /proc/sys/kernel/osrelease.");
        if (release.Count == 0) warnings.Add("Distribution information unavailable from /etc/os-release.");
        if (root != Path.GetPathRoot(root))
            warnings.Add("Alternate filesystem root selected; architecture describes the scanner process host, not a verified fixture architecture.");
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new Snapshot(DateTimeOffset.UtcNow, system, devices, warnings));
    }

    private void ScanBus(string bus, List<Device> devices, List<string> warnings, CancellationToken token)
    {
        var directory = At($"sys/bus/{bus}/devices");
        var entries = new List<string>();
        try
        {
            foreach (var entry in Directory.EnumerateDirectories(directory))
            {
                token.ThrowIfCancellationRequested();
                if (entries.Count == MaxDevicesPerBus)
                {
                    warnings.Add($"{bus}: inventory truncated at {MaxDevicesPerBus} entries.");
                    break;
                }
                entries.Add(entry);
            }
        }
        catch (Exception ex) when (IsAccessError(ex))
        {
            warnings.Add($"{bus}: sysfs directory unavailable or enumeration incomplete ({ex.GetType().Name}).");
        }

        foreach (var entry in entries.Order(StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            var key = Path.GetFileName(entry);
            string? Read(string name) => ReadText(Path.Combine(entry, name), warnings, token);
            var idRoot = entry;
            var interfaceIndex = bus == "usb" ? key.IndexOf(':') : -1;
            if (interfaceIndex >= 0) idRoot = Path.Combine(directory, key[..interfaceIndex]);
            string? Id(string name, int digits) => Hex(ReadText(Path.Combine(idRoot, name), warnings, token), digits);
            var vendor = Id(bus == "pci" ? "vendor" : "idVendor", 4);
            var product = Id(bus == "pci" ? "device" : "idProduct", 4);
            var ids = new List<string>();
            if (vendor is not null && product is not null) ids.Add($"{bus.ToUpperInvariant()}:{vendor}:{product}");
            else
            {
                if (vendor is not null) ids.Add($"{bus.ToUpperInvariant()}:VENDOR:{vendor}");
                if (product is not null) ids.Add($"{bus.ToUpperInvariant()}:DEVICE:{product}");
                warnings.Add($"{bus}/{key}: vendor/product identifiers incomplete; device may have disappeared.");
            }

            string? deviceClass;
            if (bus == "pci")
            {
                var subVendor = Id("subsystem_vendor", 4);
                var subDevice = Id("subsystem_device", 4);
                if (subVendor is not null && subDevice is not null) ids.Add($"PCI:SUBSYSTEM:{subVendor}:{subDevice}");
                deviceClass = Hex(Read("class"), 6);
            }
            else deviceClass = Hex(Read(interfaceIndex >= 0 ? "bInterfaceClass" : "bDeviceClass"), 2);
            var modalias = Clean(Read("modalias"));
            if (modalias is not null) ids.Add($"MODALIAS:{modalias}");
            var driver = ReadLink(Path.Combine(entry, "driver"), warnings, token, out var bindingKnown);
            var module = driver is null ? null : ReadLink(Path.Combine(entry, "driver", "module"), warnings, token, out _);
            var version = module is null ? null : Clean(Read("driver/module/version")) ??
                Clean(ReadText(At($"sys/module/{module}/version"), warnings, token));
            var name = bus == "usb" ? Clean(ReadText(Path.Combine(idRoot, "product"), warnings, token)) : null;
            name ??= ids.FirstOrDefault() ?? $"{bus.ToUpperInvariant()} {key}";
            var manufacturer = bus == "usb" ? Clean(ReadText(Path.Combine(idRoot, "manufacturer"), warnings, token)) : null;
            if (!Directory.Exists(entry))
            {
                bindingKnown = false;
                warnings.Add($"{bus}/{key}: sysfs entry disappeared or became inaccessible during collection; partial observations retained.");
            }
            devices.Add(new Device($"{bus}:{key}", name,
                deviceClass is null ? bus.ToUpperInvariant() : $"{bus.ToUpperInvariant()} class {deviceClass}",
                manufacturer, ids.ToArray(), driver, version, null, null, null,
                driver is not null ? "bound" : bindingKnown ? "no-binding-observed" : "unknown",
                $"Observed sysfs {bus}/{key}; identifiers/class, driver symlink and optional module version. " +
                "PCI manufacturer names and driver providers are not resolved; no compatibility inference."));
        }
    }

    private string At(string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string? ReadText(string path, List<string> warnings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            var buffer = new char[MaxTextCharacters + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                token.ThrowIfCancellationRequested();
                var read = reader.Read(buffer, count, Math.Min(4096, buffer.Length - count));
                if (read == 0) break;
                count += read;
            }
            if (count > MaxTextCharacters) warnings.Add($"Text attribute truncated: {SafePath(path)}.");
            return new string(buffer, 0, Math.Min(count, MaxTextCharacters));
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception ex) when (IsAccessError(ex))
        {
            warnings.Add($"Text attribute unavailable: {SafePath(path)} ({ex.GetType().Name}).");
            return null;
        }
    }

    private static string? ReadLink(string path, List<string> warnings, CancellationToken token, out bool known)
    {
        token.ThrowIfCancellationRequested();
        known = true;
        try
        {
            var target = new DirectoryInfo(path).LinkTarget;
            if (target is null) return null;
            var name = Path.GetFileName(target.TrimEnd('/', '\\'));
            // Module names are subsequently used as one path component only.
            if (name.Length == 0 || name is "." or ".." || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')))
            {
                known = false;
                warnings.Add("Invalid driver/module link component omitted.");
                return null;
            }
            return name;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception ex) when (IsAccessError(ex))
        {
            known = false;
            warnings.Add($"Driver/module binding unavailable ({ex.GetType().Name}).");
            return null;
        }
    }

    private static bool IsAccessError(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;
    private static string SafePath(string path)
    {
        // Do not expose an alternate fixture root, which can include a username.
        var normalized = path.Replace('\\', '/');
        var index = normalized.IndexOf("/sys/", StringComparison.Ordinal);
        if (index < 0) index = normalized.IndexOf("/proc/", StringComparison.Ordinal);
        if (index < 0) index = normalized.IndexOf("/etc/", StringComparison.Ordinal);
        return index >= 0 ? normalized[index..] : "inventory attribute";
    }
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = new string(value.Trim().Where(c => !char.IsControl(c)).Take(512).ToArray());
        return clean.Length == 0 ? null : clean;
    }
    private static string? Join(params string?[] values)
    {
        var parts = values.Select(Clean).Where(v => v is not null).ToArray();
        return parts.Length == 0 ? null : string.Join(" / ", parts);
    }
    private static string? Hex(string? value, int digits)
    {
        var text = value?.Trim();
        if (text?.StartsWith("0x", StringComparison.OrdinalIgnoreCase) == true) text = text[2..];
        return text?.Length == digits && text.All(char.IsAsciiHexDigit) ? text.ToUpperInvariant() : null;
    }
    private static Dictionary<string, string> ParseAssignments(string? text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in (text ?? "").Split('\n'))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2 || parts[0] is not ("NAME" or "PRETTY_NAME" or "VERSION_ID")) continue;
            var value = parts[1].Trim();
            if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
                value = value[1..^1];
            var cleaned = Clean(value);
            if (cleaned is not null) result[parts[0]] = cleaned;
        }
        return result;
    }
}
