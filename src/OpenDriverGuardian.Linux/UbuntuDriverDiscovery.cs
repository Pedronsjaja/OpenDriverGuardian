using System.ComponentModel;
using System.Text.RegularExpressions;
using OpenDriverGuardian.Core;
namespace OpenDriverGuardian.Linux;

/// <summary>Local ubuntu-drivers recommendation inventory; never refreshes repositories or installs packages.</summary>
public sealed class UbuntuDriverDiscovery : IDriverDiscovery
{
    public async Task<DiscoveryResult> DiscoverAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/ubuntu-drivers") || !File.Exists("/etc/os-release")
            || !File.ReadLines("/etc/os-release").Any(line => line is "ID=ubuntu" or "ID=\"ubuntu\""))
            return new([], ["Only Ubuntu with /usr/bin/ubuntu-drivers is supported. apt/dnf/pacman download and firmware/LVFS are not implemented."]);
        try
        {
            var output = await NativeCommand.RunAsync("/usr/bin/ubuntu-drivers", ["devices"], TimeSpan.FromSeconds(90), cancellationToken,
                new Dictionary<string, string> { ["LC_ALL"] = "C", ["LANG"] = "C" });
            return Parse(output, snapshot.System);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new([], ["ubuntu-drivers query timed out."]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        { return new([], ["ubuntu-drivers unavailable; no candidates fabricated."]); }
    }
    public static DiscoveryResult Parse(string output, SystemInfo system)
    {
        var candidates = new List<DriverCandidate>(); string? modalias = null;
        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("==", StringComparison.Ordinal)) modalias = null;
            var match = Regex.Match(line, @"^\s*modalias\s*:\s*(\S+)\s*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (match.Success) modalias = match.Groups[1].Value;
            match = Regex.Match(line, @"^\s*driver\s*:\s*([a-z0-9][a-z0-9+.-]*)\s+-\s+(.*)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (!match.Success || modalias is null) continue;
            var package = match.Groups[1].Value;
            candidates.Add(new(package, package, "UNKNOWN", null, "UbuntuDrivers", OfficialSources.Pages[4], ["MODALIAS:" + modalias],
                "Linux", system.OsVersion, system.Architecture, system.Kernel, null, false, null, "Distro package guidance", null, null,
                "Local ubuntu-drivers devices: " + match.Groups[2].Value + ". Repository origin, package version/hash/signature and running-kernel compatibility NOT_VERIFIED. Use distribution tools manually; third-party candidates are not trusted by this application."));
        }
        return new(candidates, ["Local cache recommendations only; no apt refresh/download, repository trust validation or package signature validation. Candidates remain unverified even when the tool says recommended."]);
    }
}
