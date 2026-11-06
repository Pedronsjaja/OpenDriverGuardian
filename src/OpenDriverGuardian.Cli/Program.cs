using OpenDriverGuardian.Core;
using OpenDriverGuardian.Windows;
using OpenDriverGuardian.Linux;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("OpenDriver Guardian: scan [--output report.json] [--html report.html]\nLocal read-only inventory; never installs/downloads drivers or records audio.");
    return 0;
}
if (args[0] != "scan") { Console.Error.WriteLine("Unknown command. Use --help."); return 2; }
string? output = null, html = null;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] is not ("--output" or "--html") || i + 1 >= args.Length)
    { Console.Error.WriteLine("Invalid scan options."); return 2; }
    var flag = args[i++]; if (flag == "--output") output = args[i]; else html = args[i];
}
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
try
{
    IInventoryScanner scanner = OperatingSystem.IsWindows() ? new WindowsScanner()
        : OperatingSystem.IsLinux() ? new LinuxScanner() : throw new PlatformNotSupportedException("Only Windows and Linux are supported.");
    var snapshot = await scanner.ScanAsync(cancel.Token);
    if (output is null) Console.WriteLine(Reports.Json(snapshot));
    else await Reports.WriteFileAsync(output, Reports.Json(snapshot), token: cancel.Token);
    if (html is not null) await Reports.WriteFileAsync(html, Reports.Html(snapshot), token: cancel.Token);
    Console.Error.WriteLine($"Collected {snapshot.Devices.Count} devices; {snapshot.Warnings.Count} collection warnings. No changes to drivers.");
    return snapshot.Warnings.Count > 0 ? 1 : 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Scan cancelled or timed out."); return 3; }
catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Text.Json.JsonException)
{ Console.Error.WriteLine($"Operation failed ({e.GetType().Name}). Check permissions, supported OS and a new export filename."); return 2; }
