using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OpenDriverGuardian.Core;
public static class Reports
{
    public static async Task WriteFileAsync(string path, string content, bool overwrite = false, CancellationToken token = default)
    {
        var destination = Path.GetFullPath(path);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream))
            { await writer.WriteAsync(content.AsMemory(), token); await writer.FlushAsync(token); }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string Json(Snapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);
    public static Snapshot Parse(string json) => JsonSerializer.Deserialize<Snapshot>(json, Options)
        ?? throw new InvalidDataException("Inventory JSON was null.");
    public static string Html(Snapshot snapshot)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "UNKNOWN");
        var b = new StringBuilder("<!doctype html><html lang='en'><meta charset='utf-8'><title>OpenDriver Guardian</title><style>body{font:15px system-ui;margin:2rem}table{border-collapse:collapse}td,th{border:1px solid #bbb;padding:.4rem}code{overflow-wrap:anywhere}</style><h1>OpenDriver Guardian</h1><p>Read-only inventory. Compatibility and update availability are not established.</p>");
        b.Append($"<p>{H(snapshot.System.Os)} | {H(snapshot.System.OsVersion)} | {H(snapshot.System.Architecture)} | {H(snapshot.CapturedAt.ToString("O"))}</p><h2>Collection warnings</h2><ul>");
        foreach (var warning in snapshot.Warnings) b.Append($"<li>{H(warning)}</li>");
        b.Append("</ul><table><tr><th>Device</th><th>Class</th><th>Hardware identifiers</th><th>Driver/version</th><th>Observed state</th><th>Assessment</th></tr>");
        foreach (var d in snapshot.Devices) b.Append($"<tr><td>{H(d.Name)}</td><td>{H(d.Category)}</td><td><code>{H(string.Join("; ", d.HardwareIds))}</code></td><td>{H(d.Driver)} / {H(d.DriverVersion)}</td><td>{H(d.State)}</td><td>{H(Diagnostics.Assess(d).Reason)}</td></tr>");
        return b.Append("</table><p>Audio inventory only; no recording or playback performed. No serial numbers, account names or MAC addresses are requested. Review device labels before sharing.</p></html>").ToString();
    }
}
