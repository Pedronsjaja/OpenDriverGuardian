using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("OpenDriverGuardian.Tests")]
namespace OpenDriverGuardian.Core;

public sealed record SignatureResult(string Status, string? SignerThumbprint = null);
public interface IPackageSignatureVerifier
{
    Task<SignatureResult> VerifyAsync(string path, CancellationToken token);
}
public sealed record DownloadProgress(long BytesReceived, long? TotalBytes);
public sealed record DownloadResult(string OperationId, string Status, string? Path, string? Sha256,
    string HashStatus, string SignatureStatus, string Reason)
{
    public string? SignerThumbprint { get; init; }
}

/// <summary>Explicit, single-package transfers. Never executes or installs downloaded content.</summary>
public sealed class DriverDownloads : IDisposable
{
    private readonly HttpClient client;
    private readonly IPackageSignatureVerifier signatures;
    private readonly string root;
    public const long MaximumBytes = 2L * 1024 * 1024 * 1024;
    public DriverDownloads(string root, IPackageSignatureVerifier signatures)
        : this(root, signatures, new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None }) { }
    internal DriverDownloads(string root, IPackageSignatureVerifier signatures, HttpMessageHandler transport)
    {
        this.root = System.IO.Path.GetFullPath(root); this.signatures = signatures;
        client = new HttpClient(transport) { Timeout = Timeout.InfiniteTimeSpan };
    }
    public async Task<DownloadResult> DownloadAsync(DriverCandidate candidate, Device device, SystemInfo system,
        IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(root);
        // Each operation owns a fresh directory. Never accept package titles/IDs as filesystem paths.
        RejectLink(root);
        var directory = System.IO.Path.Combine(root, id); Directory.CreateDirectory(directory); RejectLink(directory);
        var temporary = System.IO.Path.Combine(directory, "payload.part");
        string? hash = null, retained = null, finalUrl = null, signerThumbprint = null;
        var hashStatus = "NOT_VERIFIED"; var signatureStatus = "NOT_VERIFIED";
        DownloadResult Result(string status, string reason) => new(id, status, retained, hash, hashStatus, signatureStatus, reason) { SignerThumbprint = signerThumbprint };
        DownloadResult result;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(20));
        var token = timeout.Token;
        try
        {
            if (!DriverMatching.Evaluate(device, system, candidate).Confirmed || !OfficialSources.CanDownload(candidate))
                throw new InvalidDataException("No confirmed match and validated official HTTPS direct URL.");
            var uri = new Uri(candidate.DownloadUrl!);
            HttpResponseMessage? response = null;
            try
            {
                for (var hop = 0; hop <= 5; hop++)
                {
                    if (!OfficialSources.IsDownloadUri(uri)) throw new InvalidDataException("Unauthorized redirect destination.");
                    response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                    if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
                    {
                        var location = response.Headers.Location ?? throw new InvalidDataException("Redirect lacks a destination.");
                        uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                        response.Dispose(); response = null;
                        continue;
                    }
                    break;
                }
                if (response is null) throw new InvalidDataException("Redirect limit exceeded.");
                response.EnsureSuccessStatusCode(); finalUrl = uri.AbsoluteUri;
                var length = response.Content.Headers.ContentLength;
                if (length > MaximumBytes || candidate.SizeBytes > MaximumBytes) throw new InvalidDataException("Package exceeds download limit.");
                // WUA MaxDownloadSize is an estimate, not an exact Content-Length.
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                {
                    var buffer = new byte[81920]; long count = 0; int read;
                    while ((read = await input.ReadAsync(buffer, token)) > 0)
                    {
                        count += read; if (count > MaximumBytes) throw new InvalidDataException("Package exceeds download limit.");
                        digest.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token);
                        progress?.Report(new(count, length));
                    }
                    if (count == 0 || (length.HasValue && count != length)) throw new InvalidDataException("Empty or incomplete response.");
                    await output.FlushAsync(token);
                }
                hash = Convert.ToHexString(digest.GetHashAndReset());
                if (candidate.Sha256 is not null)
                {
                    hashStatus = hash.Equals(candidate.Sha256, StringComparison.OrdinalIgnoreCase) ? "MATCHED" : "FAILED";
                    if (hashStatus == "FAILED") throw new InvalidDataException("Trusted SHA-256 mismatch.");
                }
                else hashStatus = "COMPUTED_ONLY_NO_TRUSTED_REFERENCE";
                var extension = System.IO.Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
                if (extension is not (".exe" or ".cab" or ".msu" or ".zip" or ".deb" or ".rpm")) extension = ".bin";
                var verificationPath = System.IO.Path.Combine(directory, "payload" + extension);
                File.Move(temporary, verificationPath); temporary = verificationPath;
                var signature = await signatures.VerifyAsync(temporary, token); signatureStatus = signature.Status; signerThumbprint = signature.SignerThumbprint;
                if (signatureStatus == "INVALID") throw new InvalidDataException("Package signature verification failed.");
                var verified = signatureStatus == "VALID";
                retained = System.IO.Path.Combine(directory, verified ? "package" + extension : "package" + extension + ".unverified");
                File.Move(temporary, retained);
                result = Result(verified ? "DownloadCompleted" : "VerificationIncomplete",
                    verified ? "Authenticode valid; SHA-256 status recorded. Requires manual installation; package applicability beyond WUA was not inspected."
                        : "Downloaded from an allowed official host, but signature validation was not completed. Quarantined; do not install.");
            }
            finally { response?.Dispose(); }
        }
        catch (OperationCanceledException) { result = Result("Cancelled", cancellationToken.IsCancellationRequested ? "Cancelled by caller." : "Download or verification timed out."); }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { result = Result(ex is InvalidDataException ? "VerificationFailed" : "SourceUnavailable", ex is InvalidDataException ? ex.Message : "Network, source or local storage unavailable; no installation attempted."); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        // Per-operation journal: no complete inventory, account name or local absolute path in the journal.
        var journal = new { Timestamp = DateTimeOffset.UtcNow, Candidate = candidate.Id, candidate.Source,
            InitialUrl = candidate.DownloadUrl, FinalUrl = finalUrl, result.OperationId, result.Status,
            result.Sha256, result.HashStatus, result.SignatureStatus, result.SignerThumbprint, result.Reason };
        await Reports.WriteFileAsync(System.IO.Path.Combine(directory, "result.json"), JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }
    private static void RejectLink(string path)
    {
        // Reject redirecting roots, including any existing ancestor junction/symlink.
        for (DirectoryInfo? directory = new(path); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Download root contains a link/junction.");
    }
    public void Dispose() => client.Dispose();
}
