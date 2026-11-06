using System.Diagnostics;
using System.Text;
namespace OpenDriverGuardian.Core;

public static class NativeCommand
{
    public static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, TimeSpan limit,
        CancellationToken token, IReadOnlyDictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null) foreach (var pair in environment) start.Environment[pair.Key] = pair.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(limit);
        using var process = Process.Start(start) ?? throw new IOException("Native adapter could not start.");
        void Stop() { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        async Task<string> Read(StreamReader reader)
        {
            var result = new StringBuilder(); var buffer = new char[4096]; int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0)
            {
                if (result.Length + count > 2_000_000) { Stop(); throw new IOException("Native adapter output limit exceeded."); }
                result.Append(buffer, 0, count);
            }
            return result.ToString();
        }
        var output = Read(process.StandardOutput); var errors = Read(process.StandardError);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var value = await output; await errors;
            if (process.ExitCode != 0) throw new IOException("Native source unavailable or refused the query.");
            return value;
        }
        catch
        {
            Stop();
            try { await Task.WhenAll(output, errors); } catch { /* Observe failed readers before disposing the process. */ }
            throw;
        }
    }
}
