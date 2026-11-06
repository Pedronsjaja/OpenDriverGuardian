using System.Diagnostics;
using System.Text;
using OpenDriverGuardian.Core;
namespace OpenDriverGuardian.Windows;
public sealed class WindowsScanner : IInventoryScanner
{
    public async Task<Snapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows scanner requires Windows.");
        using var resource = typeof(WindowsScanner).Assembly.GetManifestResourceStream("OpenDriverGuardian.Windows.inventory.ps1")
            ?? throw new InvalidOperationException("Inventory adapter resource missing.");
        using var reader = new StreamReader(resource);
        var script = await reader.ReadToEndAsync(cancellationToken);
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }) start.ArgumentList.Add(arg);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        using var process = Process.Start(start) ?? throw new IOException("Could not start the native inventory adapter.");
        try
        {
            void StopChild() { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var output = ReadBounded(process.StandardOutput, timeout.Token, StopChild);
            var errors = ReadBounded(process.StandardError, timeout.Token, StopChild);
            await process.WaitForExitAsync(timeout.Token);
            var json = await output; await errors;
            if (process.ExitCode != 0) throw new IOException("Native inventory adapter failed. No driver changes were requested.");
            return Reports.Parse(json);
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
    }
    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken token, Action stopChild)
    {
        var result = new StringBuilder(); var buffer = new char[8192]; int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (result.Length + count > 8_000_000) { stopChild(); throw new IOException("Inventory adapter output exceeded the safety limit."); }
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }
}
