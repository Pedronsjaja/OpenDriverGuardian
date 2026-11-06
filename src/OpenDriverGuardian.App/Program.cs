using Avalonia;
namespace OpenDriverGuardian.App;
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => AppBuilder.Configure<GuardianApplication>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
