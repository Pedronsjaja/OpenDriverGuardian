namespace OpenDriverGuardian.Core;

public sealed record DriverCandidate(string Id, string Title, string Manufacturer, string? Version,
    string Source, string OfficialPage, string[] HardwareIds, string Os, string OsVersion, string Architecture,
    string Kernel, string? Board, bool ApplicabilityConfirmed, long? SizeBytes, string PackageType,
    string? DownloadUrl, string? Sha256, string Evidence);
public sealed record DiscoveryResult(IReadOnlyList<DriverCandidate> Candidates, IReadOnlyList<string> Warnings);
public interface IDriverDiscovery
{
    Task<DiscoveryResult> DiscoverAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
}
public sealed record MatchResult(bool Confirmed, string Reason);

public static class DriverMatching
{
    public static MatchResult Evaluate(Device device, SystemInfo system, DriverCandidate candidate)
    {
        static bool Known(string value) => !string.IsNullOrWhiteSpace(value)
            && !new[] { "UNKNOWN", "NOT_VERIFIED", "NOT_MEASURED" }.Contains(value, StringComparer.OrdinalIgnoreCase);
        static bool Same(string a, string b) => Known(a) && Known(b) && a.Equals(b, StringComparison.OrdinalIgnoreCase);
        if (!Same(system.Os, candidate.Os) || !Same(system.OsVersion, candidate.OsVersion)) return new(false, "OS/version mismatch or unknown.");
        if (!Same(system.Architecture, candidate.Architecture)) return new(false, "Architecture mismatch or unknown.");
        if (!Same(system.Kernel, candidate.Kernel)) return new(false, "Kernel/build mismatch or unknown.");
        if (candidate.Board is not null && !Same(system.Board ?? "", candidate.Board)) return new(false, "OEM board mismatch or unknown.");
        // Full identifiers only: never substring-match VID/PID, subsystem, revisions or modalias globs.
        var ids = device.HardwareIds.Concat(device.CompatibleIds);
        if (!candidate.HardwareIds.Any(id => Known(id) && ids.Contains(id, StringComparer.OrdinalIgnoreCase))) return new(false, "No exact hardware/compatible ID or modalias match.");
        if (!candidate.ApplicabilityConfirmed || candidate.Source != "WindowsUpdate") return new(false, "Native recommendation only; package applicability/provenance not confirmed.");
        return new(true, "Exact ID and Windows Update applicability on this OS/build/architecture. Not an installation recommendation or verification of package contents.");
    }
}

public static class OfficialSources
{
    public static readonly string[] Pages = ["https://www.catalog.update.microsoft.com/", "https://www.nvidia.com/Download/index.aspx",
        "https://www.amd.com/en/support", "https://www.intel.com/content/www/us/en/download-center/home.html",
        "https://ubuntu.com/server/docs/nvidia-drivers-installation"];
    public static bool CanDownload(DriverCandidate candidate) => candidate.Source == "WindowsUpdate"
        && Uri.TryCreate(candidate.DownloadUrl, UriKind.Absolute, out var uri) && IsDownloadUri(uri);
    public static bool IsDownloadUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != "https" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0) return false;
        var host = uri.IdnHost;
        return host.Equals("download.windowsupdate.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".download.windowsupdate.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("dl.delivery.mp.microsoft.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("download.microsoft.com", StringComparison.OrdinalIgnoreCase);
    }
    public static bool IsPage(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.UserInfo.Length == 0
        && Pages.Contains(uri.AbsoluteUri, StringComparer.Ordinal);
}
