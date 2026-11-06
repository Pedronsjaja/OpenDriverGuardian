using OpenDriverGuardian.Core;
using OpenDriverGuardian.Linux;
using Xunit;
namespace OpenDriverGuardian.Tests;
public sealed class InventoryTests
{
    private static Device Example(int? code = null, string state = "unknown", string name = "Synthetic device") => new("fixture-key", name, "Display", "Example", ["PCI\\VEN_1234&DEV_5678"], null, null, null, null, code, state, "SYNTHETIC");
    private static Snapshot SnapshotOf(Device d) => new(DateTimeOffset.Parse("2026-11-06T00:00:00Z"), new("Synthetic", "1", "X64", "fixture", null, null, null, null, null), [d], []);

    [Theory]
    [InlineData(28, "DriverMissing")]
    [InlineData(22, "ManualIntervention")]
    [InlineData(10, "DeviceProblem")]
    [InlineData(0, "CompatibilityUnverified")]
    public void ProblemCodesAreNotInferredFromVersion(int code, string classification)
        => Assert.Equal(classification, Diagnostics.Assess(Example(code)).Classification);
    [Fact]
    public void UnboundLinuxDeviceDoesNotProveMissingDriver()
        => Assert.Equal("CompatibilityUnverified", Diagnostics.Assess(Example(null, "no-binding-observed")).Classification);
    [Fact]
    public void JsonRoundTripPreservesNullUnknowns()
    {
        var parsed = Reports.Parse(Reports.Json(SnapshotOf(Example())));
        Assert.Null(parsed.Devices[0].DriverVersion);
        Assert.Null(parsed.Devices[0].ProblemCode);
        Assert.Equal("fixture-key", parsed.Devices[0].Key);
    }
    [Fact]
    public void HtmlEscapesUntrustedDeviceLabels()
    {
        var html = Reports.Html(SnapshotOf(Example(name: "<script>alert('x')</script>")));
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }
    [Theory]
    [InlineData("MEDIA", true)]
    [InlineData("AudioEndpoint", true)]
    [InlineData("sound", true)]
    [InlineData("Display", false)]
    [InlineData("PCI class 040300", true)]
    [InlineData("USB class 01", true)]
    public void AudioClassificationDoesNotOpenDevices(string category, bool expected)
        => Assert.Equal(expected, Diagnostics.IsAudio(Example() with { Category = category }));
    [Fact]
    public async Task LinuxMissingSourcesProduceWarnings()
    {
        using var fixture = new Tree();
        var snapshot = await new LinuxScanner(fixture.Root).ScanAsync();
        Assert.Empty(snapshot.Devices); Assert.NotEmpty(snapshot.Warnings);
        Assert.Null(snapshot.System.Cpu); Assert.Null(snapshot.System.MemoryBytes);
    }
    [Fact]
    public async Task LinuxSyntheticPciAndUsbKeepIdentifiersButExcludeSerials()
    {
        using var t = new Tree();
        t.Put("etc/os-release", "NAME=FixtureLinux\nVERSION_ID=1\n");
        t.Put("proc/cpuinfo", "model name : Synthetic CPU\n");
        t.Put("proc/meminfo", "MemTotal: 1024 kB\n");
        t.Put("proc/sys/kernel/osrelease", "synthetic-kernel");
        foreach (var (name, value) in new[] { ("vendor", "0x1234"), ("device", "0x5678"), ("subsystem_vendor", "0xabcd"), ("subsystem_device", "0x0001"), ("class", "0x030000"), ("modalias", "pci:synthetic") }) t.Put("sys/bus/pci/devices/fixture-slot/"+name, value);
        t.Put("sys/bus/usb/devices/1-1/idVendor", "1234"); t.Put("sys/bus/usb/devices/1-1/idProduct", "5678");
        t.Put("sys/bus/usb/devices/1-1/product", "Synthetic USB"); t.Put("sys/bus/usb/devices/1-1/serial", "SECRET_SERIAL_MUST_NOT_BE_READ");
        var s = await new LinuxScanner(t.Root).ScanAsync();
        Assert.Equal(2, s.Devices.Count); Assert.Equal((ulong)1048576, s.System.MemoryBytes);
        Assert.DoesNotContain("SECRET_SERIAL", Reports.Json(s));
        Assert.All(s.Devices, d => Assert.NotEqual("DriverMissing", Diagnostics.Assess(d).Classification));
        Assert.Contains(s.Devices, d => d.HardwareIds.Any(h => h.Contains("1234") && h.Contains("5678")));
    }
    [Fact]
    public async Task LinuxCancellationIsHonored()
    {
        using var t = new Tree(); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LinuxScanner(t.Root).ScanAsync(cts.Token));
    }
    [Fact]
    public async Task AtomicExportPreservesExistingFileAndCleansTemporaryOnFailure()
    {
        using var t = new Tree(); var path = Path.Combine(t.Root,"report.json");
        await Reports.WriteFileAsync(path,"original");
        await Assert.ThrowsAsync<IOException>(() => Reports.WriteFileAsync(path,"replacement"));
        Assert.Equal("original",File.ReadAllText(path));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reports.WriteFileAsync(path,"cancelled",true,cancel.Token));
        Assert.Equal("original",File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(t.Root));
    }
    [Fact]
    public void AnonymizedRealWindowsFixtureRetainsObservedSchema()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "windows-device.json");
        var snapshot = Reports.Parse(File.ReadAllText(path));
        Assert.Equal("Windows", snapshot.System.Os); Assert.NotEmpty(snapshot.Devices);
        Assert.All(snapshot.Devices, d => Assert.Equal("anonymized-device", d.Key));
    }
    private sealed class Tree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "guardian-fixture-"+Guid.NewGuid().ToString("N"));
        public Tree() => Directory.CreateDirectory(Root);
        public void Put(string relative, string text) { var p = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p,text); }
        public void Dispose() => Directory.Delete(Root,true);
    }
}
