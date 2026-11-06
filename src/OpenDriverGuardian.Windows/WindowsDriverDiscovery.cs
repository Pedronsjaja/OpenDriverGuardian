using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using OpenDriverGuardian.Core;
[assembly: InternalsVisibleTo("OpenDriverGuardian.Tests")]
namespace OpenDriverGuardian.Windows;

public sealed class WindowsDriverDiscovery : IDriverDiscovery
{
    public async Task<DiscoveryResult> DiscoverAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) return new([], ["Windows Update requires Windows."]);
        var current = Environment.OSVersion.Version;
        if (snapshot.System.Os != "Windows" || !Version.TryParse(snapshot.System.OsVersion, out var observed)
            || observed.Major != current.Major || observed.Minor != current.Minor || observed.Build != current.Build
            || snapshot.System.Kernel != current.Build.ToString(System.Globalization.CultureInfo.InvariantCulture)
            || snapshot.System.Architecture != RuntimeInformation.OSArchitecture.ToString())
            return new([], ["Inventory does not describe this OS session; run a new scan."]);
        try
        {
            var json = await PowerShellAdapter.RunResourceAsync("discovery.ps1", cancellationToken);
            return Parse(json, snapshot.System);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new([], ["Windows Update query timed out."]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException)
        { return new([], ["Windows Update unavailable, offline or refused by policy. No candidate was fabricated."]); }
    }
    internal static DiscoveryResult Parse(string json, SystemInfo system)
    {
        var rows = JsonSerializer.Deserialize<UpdateRow[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Missing native response.");
        if (rows.Length > 1000 || rows.Any(row => row is null || string.IsNullOrWhiteSpace(row.Id)
            || string.IsNullOrWhiteSpace(row.Title) || string.IsNullOrWhiteSpace(row.HardwareId)
            || row.Urls is null || row.Urls.Length > 32 || row.SizeBytes < 0))
            throw new JsonException("Malformed native candidate metadata.");
        var candidates = rows.Select(row => new DriverCandidate(row.Id, row.Title,
            string.IsNullOrWhiteSpace(row.Manufacturer) ? "UNKNOWN" : row.Manufacturer,
            null, "WindowsUpdate", OfficialSources.Pages[0], [row.HardwareId], system.Os,
            system.OsVersion, system.Architecture, system.Kernel, null, true,
            row.SizeBytes, row.Urls.Length > 1 ? "WUA multipart content (manual assembly required)" : "WUA content",
            row.Urls.Length == 1 ? row.Urls[0] : null, null,
            "Microsoft Windows Update applicability search; DriverHardwareID and provider metadata. Version unavailable (not parsed from title). Subsystem/OEM applicability delegated to WUA; no independent INF validation.")).ToArray();
        return new(candidates, ["WUA may omit direct URLs or return HTTP/multipart content: download is disabled for these. No trusted hash is exposed by this adapter. No packages installed or downloaded by discovery."]);
    }
    private sealed record UpdateRow(string Id, string Title, string Manufacturer, string HardwareId, long? SizeBytes, string[] Urls);
}

internal static class PowerShellAdapter
{
    internal static async Task<string> RunResourceAsync(string resource, CancellationToken token, IReadOnlyDictionary<string, string>? environment = null)
    {
        using var stream = typeof(PowerShellAdapter).Assembly.GetManifestResourceStream("OpenDriverGuardian.Windows." + resource)
            ?? throw new IOException("Native adapter missing.");
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(token);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return await NativeCommand.RunAsync(executable, ["-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script))], TimeSpan.FromMinutes(5), token, environment);
    }
}

public sealed class WindowsSignatureVerifier : IPackageSignatureVerifier
{
    public async Task<SignatureResult> VerifyAsync(string path, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows()) return new("NOT_SUPPORTED");
        try
        {
            var json = await PowerShellAdapter.RunResourceAsync("signature.ps1", token, new Dictionary<string, string> { ["ODG_SIGNATURE_FILE"] = Path.GetFullPath(path) });
            return JsonSerializer.Deserialize<SignatureResult>(json) ?? new("NOT_VERIFIED");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException)
        { return new("NOT_VERIFIED"); }
    }
}
