using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using OpenDriverGuardian.Core;
using OpenDriverGuardian.Windows;
using OpenDriverGuardian.Linux;

namespace OpenDriverGuardian.App;
public sealed class GuardianApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new GuardianWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
public sealed partial class GuardianWindow : Window
{
    private Snapshot? snapshot;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Button scan = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TabControl tabs = new();
    private readonly StackPanel body = new() { Spacing = 14, Margin = new Thickness(24) };
    private bool english;
    private bool scanning;
    private string T(string pt, string en) => english ? en : pt;
    public GuardianWindow()
    {
        Title = "OpenDriver Guardian"; Width = 1180; Height = 800; MinWidth = 760; MinHeight = 500;
        Content = new ScrollViewer { Content = body }; Closed += (_, _) => { lifetime.Cancel(); };
        scan.Click += async (_, _) => await Scan();
        Build();
    }
    private static TextBlock Text(string value, double size = 15) => new() { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
    private void Build()
    {
        body.Children.Clear();
        body.Children.Add(Text("OpenDriver Guardian", 28));
        body.Children.Add(Text(T("Inventário local • Downloads por solicitação • Sem instalação de drivers", "Local inventory • Downloads on request • No driver installation")));
        scan.Content = T("Analisar este computador", "Scan this computer"); scan.IsEnabled = !scanning && !driverBusy;
        body.Children.Add(scan); body.Children.Add(status);
        var items = new List<TabItem>();
        var s = snapshot?.System;
        items.Add(Tab(T("Visão geral", "Dashboard"), snapshot is null ? T("Execute uma análise para obter dados reais.", "Run a scan to collect real data.")
            : $"{s!.Os} {s.OsVersion} | {s.Architecture} | kernel/build {s.Kernel}\nCPU: {s.Cpu ?? "UNKNOWN"}\nBoard: {s.Board ?? "UNKNOWN"}\nBIOS: {s.Bios ?? "UNKNOWN"}\nRAM: {s.MemoryBytes?.ToString() ?? "UNKNOWN"} bytes\n{snapshot.Devices.Count} devices | {snapshot.Devices.Count(d => d.ProblemCode > 0)} OS problem codes\n{snapshot.CapturedAt:O}\n" + string.Join("\n", snapshot.Warnings)));
        items.Add(Tab(T("Inventário", "Hardware inventory"), snapshot is null ? "—" : string.Join("\n\n", snapshot.Devices.Select(d => $"{d.Name} [{d.Category}]\n{string.Join("; ", d.HardwareIds)}\nDriver: {d.Driver ?? "UNKNOWN"} | Version: {d.DriverVersion ?? "UNKNOWN"} | Provider: {d.DriverProvider ?? "UNKNOWN"}\nINF: {d.InfPath ?? "UNKNOWN"} | Signed (OS metadata): {d.IsSigned?.ToString() ?? "UNKNOWN"} | Problem: {d.ProblemCode?.ToString() ?? "UNKNOWN"} | {d.State}"))));
        items.Add(Tab(T("Diagnóstico / recomendações", "Diagnostics / recommendations"), T("Consulte Drivers e Downloads para candidatos e evidências. Não inferimos compatibilidade pela versão.", "See Drivers and Downloads for candidates and evidence. Version does not establish compatibility.") + "\n\n" + (snapshot is null ? "—" : string.Join("\n\n", snapshot.Devices.Where(d => d.ProblemCode > 0 || d.State == "no-binding-observed").Select(d => { var f = Diagnostics.Assess(d); return $"{d.Name}: {f.Classification}\n{f.Reason}\n{f.Action}\nSource: {f.Source}"; })))));
        items.Add(DriversTab());
        items.Add(Tab(T("Diagnóstico de áudio", "Audio diagnostics"), T("MVP: apenas controladores/endereços observados. Rotas, padrão, serviços e testes de áudio ainda não implementados. Microfone não é aberto.", "MVP: observed controllers/endpoints only. Routing, defaults, services and audio tests are not implemented. Microphone is never opened.") + "\n\n" + (snapshot is null ? "—" : string.Join("\n", snapshot.Devices.Where(Diagnostics.IsAudio).Select(d => $"{d.Name} | {d.State} | {d.ProblemCode?.ToString() ?? "UNKNOWN"}")))));
        var reports = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        reports.Children.Add(Text(T("Exportação local JSON/HTML. PDF e histórico SQLite: planejados. Revise o relatório antes de compartilhar.", "Local JSON/HTML export. PDF and SQLite history: planned. Review reports before sharing.")));
        foreach (var format in new[] { "json", "html" }) { var button = new Button { Content = "Export " + format.ToUpperInvariant(), IsEnabled = snapshot is not null }; button.Click += async (_, _) => await Export(format); reports.Children.Add(button); }
        items.Add(new TabItem { Header = T("Relatórios", "Reports"), Content = reports });
        var settings = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        settings.Children.Add(Text(T("Sem telemetria, upload ou busca automática de drivers. Exportações só por ação explícita. Nenhum teste de reprodução/gravação disponível neste MVP.", "No telemetry, upload or automatic driver lookup. Exports require an explicit action. No recording/playback tests in this MVP.")));
        var language = new Button { Content = english ? "Português" : "English" }; language.Click += (_, _) => { english = !english; Build(); }; settings.Children.Add(language);
        items.Add(new TabItem { Header = T("Configurações", "Settings"), Content = settings });
        var selected = tabs.SelectedIndex;
        tabs.ItemsSource = items; tabs.SelectedIndex = Math.Max(0, selected); tabs.Height = 580; body.Children.Add(tabs);
    }
    private static TabItem Tab(string name, string value) => new() { Header = name, Content = new ScrollViewer { Content = new SelectableTextBlock { Text = value, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16) } } };
    private async Task Scan()
    {
        if (scanning || driverBusy) return;
        scanning = true; scan.IsEnabled = false; status.Text = T("Coletando dados locais…", "Collecting local data…");
        Build();
        try
        {
            IInventoryScanner scanner = OperatingSystem.IsWindows() ? new WindowsScanner() : OperatingSystem.IsLinux() ? new LinuxScanner() : throw new PlatformNotSupportedException();
            snapshot = await Task.Run(() => scanner.ScanAsync(lifetime.Token), lifetime.Token);
            discovery = null; downloads.Clear();
            status.Text = T("Análise concluída. Consulte avisos e limites da coleta.", "Scan completed. Review collection warnings and limits.");
        }
        catch (OperationCanceledException) { status.Text = T("Análise cancelada ou tempo limite atingido.", "Scan cancelled or timed out."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or System.Text.Json.JsonException)
        { status.Text = T("Falha na coleta; nenhum driver foi alterado. ", "Collection failed; no driver was changed. ") + ex.GetType().Name; }
        finally { scanning = false; if (!lifetime.IsCancellationRequested) Build(); }
    }
    private async Task Export(string format)
    {
        if (snapshot is null) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export inventory", SuggestedFileName = "guardian-report." + format, DefaultExtension = format });
            if (file is null) return;
            var path = file.TryGetLocalPath();
            if (path is null) { status.Text = T("Selecione um destino local.", "Choose a local destination."); return; }
            await Reports.WriteFileAsync(path, format == "json" ? Reports.Json(snapshot) : Reports.Html(snapshot), overwrite: true);
            status.Text = T("Relatório exportado localmente.", "Report exported locally.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Export failed: " + ex.GetType().Name; }
    }
}
