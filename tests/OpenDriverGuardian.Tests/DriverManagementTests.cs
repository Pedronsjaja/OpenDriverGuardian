using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenDriverGuardian.Core;
using OpenDriverGuardian.Linux;
using OpenDriverGuardian.Windows;
using Xunit;
namespace OpenDriverGuardian.Tests;

public sealed class DriverManagementTests
{
    private static readonly SystemInfo System = new("Windows", "10.0.26100", "X64", "26100", null, "Example OEM", null, null, null);
    private static readonly Device Device = new("synthetic", "Synthetic audio", "MEDIA", "Example", [@"PCI\VEN_1234&DEV_5678&SUBSYS_12345678"], null, null, null, null, 28, "os-problem-reported", "SIMULATED")
        { CompatibleIds = [@"PCI\VEN_1234&DEV_5678"] };
    private static DriverCandidate Candidate => new("synthetic-package", "Synthetic signed-package scenario", "Example", "1.0", "WindowsUpdate",
        OfficialSources.Pages[0], Device.HardwareIds, System.Os, System.OsVersion, System.Architecture, System.Kernel, null, true,
        null, "Synthetic CAB", "https://download.windowsupdate.com/test/fixture.cab", null, "SIMULATED: not a published driver URL");

    [Fact] public void FullIdAndNativeApplicabilityMatch() => Assert.True(DriverMatching.Evaluate(Device, System, Candidate).Confirmed);
    [Fact] public void CompatibleIdRequiresNativeApplicability()
    {
        Assert.True(DriverMatching.Evaluate(Device, System, Candidate with { HardwareIds = Device.CompatibleIds }).Confirmed);
        Assert.False(DriverMatching.Evaluate(Device, System, Candidate with { ApplicabilityConfirmed = false }).Confirmed);
    }
    [Fact] public void MatchingUnknownValuesDoesNotConfirmCompatibility()
    {
        Assert.False(DriverMatching.Evaluate(Device, System with { Architecture = "UNKNOWN" }, Candidate with { Architecture = "UNKNOWN" }).Confirmed);
        Assert.False(DriverMatching.Evaluate(Device with { HardwareIds = ["UNKNOWN"], CompatibleIds = [] }, System, Candidate with { HardwareIds = ["UNKNOWN"] }).Confirmed);
    }
    [Theory]
    [InlineData("OS")][InlineData("Version")][InlineData("Arch")][InlineData("Kernel")][InlineData("OEM")][InlineData("ID")][InlineData("Source")]
    public void RejectsInapplicablePackages(string field)
    {
        var candidate = field switch
        {
            "OS" => Candidate with { Os = "Linux" }, "Version" => Candidate with { OsVersion = "10.0.99999" },
            "Arch" => Candidate with { Architecture = "Arm64" }, "Kernel" => Candidate with { Kernel = "99999" },
            "OEM" => Candidate with { Board = "Different OEM" }, "ID" => Candidate with { HardwareIds = [@"PCI\VEN_1234&DEV_5678&SUBSYS_99999999"] },
            _ => Candidate with { Source = "Untrusted" }
        };
        Assert.False(DriverMatching.Evaluate(Device, System, candidate).Confirmed);
    }
    [Theory]
    [InlineData("http://download.windowsupdate.com/a.cab")]
    [InlineData("https://download.windowsupdate.com.evil.example/a.cab")]
    [InlineData("https://evil.example/download.windowsupdate.com/a.cab")]
    [InlineData("https://download.windowsupdate.com:444/a.cab")]
    [InlineData("https://user@download.windowsupdate.com/a.cab")]
    [InlineData("https://127.0.0.1/a.cab")]
    public void UnauthorizedUrlIsRejected(string url) => Assert.False(OfficialSources.IsDownloadUri(new Uri(url)));
    [Fact] public void NoCandidateIsNotAnInventedUpdate()
    {
        var result = UbuntuDriverDiscovery.Parse("", System with { Os = "Linux" });
        Assert.Empty(result.Candidates); Assert.NotEmpty(result.Warnings);
    }
    [Fact] public void UbuntuRecommendationIsNotConfirmedPackageTrust()
    {
        var result = UbuntuDriverDiscovery.Parse("== /sys/devices/example ==\nmodalias : pci:v00001234d00005678\ndriver : example-driver - distro non-free recommended\n", System with { Os = "Linux" });
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("MODALIAS:pci:v00001234d00005678", candidate.HardwareIds[0]);
        Assert.False(candidate.ApplicabilityConfirmed); Assert.Null(candidate.DownloadUrl);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void WuaMetadataDoesNotInventVersionsOrMultipartDownload(int count)
    {
        var json = global::System.Text.Json.JsonSerializer.Serialize(new[] { new { Id = "fixture", Title = "Example driver 9999.0", Manufacturer = "Example",
            HardwareId = Device.HardwareIds[0], SizeBytes = 1024, Urls = Enumerable.Repeat(Candidate.DownloadUrl, count).ToArray() } });
        var candidate = Assert.Single(WindowsDriverDiscovery.Parse(json, System).Candidates);
        Assert.Null(candidate.Version);
        Assert.Equal(count == 1, OfficialSources.CanDownload(candidate));
        Assert.Equal(Device.HardwareIds[0], candidate.HardwareIds[0]);
    }
    [Fact] public void EmptyWuaResponseHasNoCandidate() => Assert.Empty(WindowsDriverDiscovery.Parse("[]", System).Candidates);
    [Theory][InlineData("[{}]")][InlineData("[null]")][InlineData("null")][InlineData("not-json")]
    public void MalformedMetadataIsNotTrusted(string json) => Assert.Throws<global::System.Text.Json.JsonException>(() => WindowsDriverDiscovery.Parse(json, System));
    [Fact] public async Task WrongSessionCannotTriggerWindowsUpdateQuery()
    {
        var result = await new WindowsDriverDiscovery().DiscoverAsync(new Snapshot(DateTimeOffset.UtcNow, System with { OsVersion = "0.0.0" }, [Device], []));
        Assert.Empty(result.Candidates); Assert.NotEmpty(result.Warnings);
    }

    [Fact] public async Task LocalServerDownloadHasRealProgressHashAndJournal()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 200_000));
        await using var server = new FixtureServer(async (stream, token) => await WriteResponse(stream, data, token));
        var root = TemporaryRoot(); var verifier = new StubSignature("VALID"); var updates = new List<DownloadProgress>();
        try
        {
            using var manager = new DriverDownloads(root, verifier, new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate with { Sha256 = Convert.ToHexString(SHA256.HashData(data)) }, Device, System, new SyncProgress(updates.Add));
            Assert.Equal("DownloadCompleted", result.Status); Assert.Equal("MATCHED", result.HashStatus); Assert.Equal("VALID", result.SignatureStatus);
            Assert.Equal(data, await File.ReadAllBytesAsync(result.Path!));
            Assert.Equal(data.Length, updates.Last().BytesReceived); Assert.Equal(data.Length, updates.Last().TotalBytes);
            Assert.Contains("DownloadCompleted", await File.ReadAllTextAsync(Path.Combine(root, result.OperationId, "result.json")));
            Assert.True(verifier.Called);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory][InlineData("HASH", "VerificationFailed")][InlineData("SIGNATURE", "VerificationFailed")][InlineData("UNSIGNED", "VerificationIncomplete")]
    public async Task VerificationFailuresNeverLookVerified(string scenario, string status)
    {
        await using var server = new FixtureServer(async (stream, token) => await WriteResponse(stream, [1, 2, 3, 4], token));
        var root = TemporaryRoot(); var verifier = new StubSignature(scenario == "SIGNATURE" ? "INVALID" : "NOT_VERIFIED");
        try
        {
            using var manager = new DriverDownloads(root, verifier, new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate with { Sha256 = scenario == "HASH" ? new string('0', 64) : null }, Device, System);
            Assert.Equal(status, result.Status);
            if (scenario == "UNSIGNED") Assert.EndsWith(".unverified", result.Path);
            else { Assert.Null(result.Path); Assert.Single(Directory.GetFiles(Path.Combine(root, result.OperationId))); }
            if (scenario == "HASH") Assert.False(verifier.Called);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task InterruptedDownloadDeletesPartialAndRecordsCancellation()
    {
        await using var server = new FixtureServer(async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 100000\r\nConnection: close\r\n\r\nhello"), token);
            await stream.FlushAsync(token); await Task.Delay(TimeSpan.FromSeconds(20), token);
        });
        var root = TemporaryRoot(); using var cancellation = new CancellationTokenSource();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate, Device, System, new SyncProgress(_ => cancellation.Cancel()), cancellation.Token);
            Assert.Equal("Cancelled", result.Status); Assert.Null(result.Path); Assert.Single(Directory.GetFiles(Path.Combine(root, result.OperationId)));
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory][InlineData(503)][InlineData(302)]
    public async Task UnavailableSourceOrUnauthorizedRedirectCannotProducePackage(int code)
    {
        await using var server = new FixtureServer(async (stream, token) =>
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {code} Test\r\nLocation: http://evil.example/driver.exe\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token));
        var root = TemporaryRoot();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate, Device, System);
            Assert.Equal(code == 503 ? "SourceUnavailable" : "VerificationFailed", result.Status); Assert.Null(result.Path);
            Assert.Equal(1, server.Requests);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task OfflineTransportDoesNotFabricateDownload()
    {
        var root = TemporaryRoot();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new OfflineTransport());
            var result = await manager.DownloadAsync(Candidate, Device, System);
            Assert.Equal("SourceUnavailable", result.Status); Assert.Null(result.Path);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task UnauthorizedUrlNeverReachesTransport()
    {
        var root = TemporaryRoot(); var transport = new OfflineTransport();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), transport);
            var result = await manager.DownloadAsync(Candidate with { DownloadUrl = "http://evil.example/file" }, Device, System);
            Assert.Equal("VerificationFailed", result.Status); Assert.False(transport.Called);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task TruncatedNetworkResponseDoesNotLeaveAPackage()
    {
        await using var server = new FixtureServer(async (stream, token) => await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1000\r\nConnection: close\r\n\r\nshort"), token));
        var root = TemporaryRoot();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate, Device, System);
            Assert.NotEqual("DownloadCompleted", result.Status); Assert.Null(result.Path);
            Assert.Single(Directory.GetFiles(Path.Combine(root, result.OperationId)));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task AllowedRedirectIsFollowedAndPackageNameCannotEscapeRoot()
    {
        var count = 0;
        await using var server = new FixtureServer(async (stream, token) =>
        {
            if (++count == 1) await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: https://download.windowsupdate.com/next.cab\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), token);
            else await WriteResponse(stream, [1, 2, 3], token);
        });
        var root = TemporaryRoot();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate with { Id = "../../escape", Title = "../../escape.exe" }, Device, System);
            Assert.Equal("DownloadCompleted", result.Status); Assert.Equal(2, server.Requests);
            Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, result.Path);
            Assert.DoesNotContain("escape", result.Path);
            Assert.Contains("https://download.windowsupdate.com/next.cab", await File.ReadAllTextAsync(Path.Combine(root, result.OperationId, "result.json")));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task OversizedResponseIsRejectedBeforeWritingPayload()
    {
        await using var server = new FixtureServer(async (stream, token) => await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {DriverDownloads.MaximumBytes + 1}\r\nConnection: close\r\n\r\n"), token));
        var root = TemporaryRoot();
        try
        {
            using var manager = new DriverDownloads(root, new StubSignature("VALID"), new LocalRelay(server.Address));
            var result = await manager.DownloadAsync(Candidate, Device, System);
            Assert.Equal("VerificationFailed", result.Status); Assert.Null(result.Path);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task WindowsAuthenticodeChecksRealSystemSignatureWithoutExecution()
    {
        var verifier = new WindowsSignatureVerifier();
        if (!OperatingSystem.IsWindows()) { Assert.Equal("NOT_SUPPORTED", (await verifier.VerifyAsync("unused", default)).Status); return; }
        var binary = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var result = await verifier.VerifyAsync(binary, default);
        Assert.Equal("VALID", result.Status); Assert.False(string.IsNullOrEmpty(result.SignerThumbprint));
        var root = TemporaryRoot();
        try
        {
            var bytes = await File.ReadAllBytesAsync(binary); bytes[1024] ^= 0xFF;
            var corrupt = Path.Combine(root, "corrupted.exe"); await File.WriteAllBytesAsync(corrupt, bytes);
            // A catalog-only signature can become NotSigned after the content hash changes.
            Assert.NotEqual("VALID", (await verifier.VerifyAsync(corrupt, default)).Status);
        }
        finally { Directory.Delete(root, true); }
    }
    private static string TemporaryRoot() { var root = Path.Combine(Path.GetTempPath(), "guardian-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }
    private static async Task WriteResponse(NetworkStream stream, byte[] data, CancellationToken token)
    {
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {data.Length}\r\nConnection: close\r\n\r\n"), token);
        await stream.WriteAsync(data, token);
    }
    private sealed class StubSignature(string status) : IPackageSignatureVerifier
    {
        public bool Called { get; private set; }
        public Task<SignatureResult> VerifyAsync(string path, CancellationToken token) { Called = true; return Task.FromResult(new SignatureResult(status)); }
    }
    private sealed class SyncProgress(Action<DownloadProgress> update) : IProgress<DownloadProgress> { public void Report(DownloadProgress value) => update(value); }
    private sealed class OfflineTransport : HttpMessageHandler
    {
        public bool Called { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Called = true; throw new HttpRequestException("SIMULATED offline"); }
    }
    // Test-only transport rewrite. Production still requires official HTTPS and validates every redirect.
    private sealed class LocalRelay(Uri address) : HttpMessageHandler
    {
        private readonly HttpClient relay = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false });
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => relay.SendAsync(new HttpRequestMessage(HttpMethod.Get, address), HttpCompletionOption.ResponseHeadersRead, token);
        protected override void Dispose(bool disposing) { if (disposing) relay.Dispose(); base.Dispose(disposing); }
    }
    private sealed class FixtureServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new(); private readonly Task serving;
        public Uri Address { get; }
        public int Requests { get; private set; }
        public FixtureServer(Func<NetworkStream, CancellationToken, Task> reply)
        {
            listener.Start(); Address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/fixture");
            serving = Task.Run(async () =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        using var connection = await listener.AcceptTcpClientAsync(stop.Token);
                        using var stream = connection.GetStream();
                        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                        while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 }) { }
                        Requests++; await reply(stream, stop.Token);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException) { }
            });
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await serving; stop.Dispose(); }
    }
}
