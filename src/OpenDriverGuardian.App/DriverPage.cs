using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using OpenDriverGuardian.Core;
using OpenDriverGuardian.Windows;
using OpenDriverGuardian.Linux;
namespace OpenDriverGuardian.App;

public sealed partial class GuardianWindow
{
    private DiscoveryResult? discovery;
    private bool driverBusy;
    private CancellationTokenSource? driverOperation;
    private readonly Dictionary<string, DownloadResult> downloads = [];
    private TabItem DriversTab()
    {
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        panel.Children.Add(Text(T("O scan é offline. A consulta abaixo é separada e não baixa nem instala pacotes. Uma versão maior não comprova compatibilidade.",
            "Scanning is offline. Discovery is separate and does not download or install packages. A newer version does not establish compatibility.")));
        var consent = new CheckBox { Content = Text(T("Autorizo consultar o Windows Update: a Microsoft pode receber dados de aplicabilidade do sistema/dispositivos. No Ubuntu, consultar recomendações locais.",
            "Allow Windows Update lookup: Microsoft may receive OS/device applicability information. On Ubuntu, query local recommendations.")), IsEnabled = !driverBusy && !scanning };
        panel.Children.Add(consent);
        var query = new Button { Content = T("Consultar fontes oficiais", "Query official sources"), IsEnabled = false };
        consent.IsCheckedChanged += (_, _) => query.IsEnabled = consent.IsChecked == true && snapshot is not null && !driverBusy && !scanning;
        query.Click += async (_, _) => await DiscoverDrivers(); panel.Children.Add(query);
        var cancel = new Button { Content = T("Cancelar operação", "Cancel operation"), IsEnabled = driverBusy };
        cancel.Click += (_, _) => driverOperation?.Cancel(); panel.Children.Add(cancel);
        panel.Children.Add(Text(T("Fontes oficiais para consulta manual (abrir uma página não confirma um candidato):", "Official manual sources (opening a page does not confirm a candidate):")));
        foreach (var page in OfficialSources.Pages)
        {
            var link = new Button { Content = page }; link.Click += (_, _) => OpenOfficialPage(page); panel.Children.Add(link);
        }
        if (snapshot is null) panel.Children.Add(Text(T("Execute uma análise primeiro.", "Run a scan first.")));
        else
        {
            var problems = snapshot.Devices.Where(d => d.ProblemCode > 0).ToArray();
            panel.Children.Add(Text(problems.Length == 0 ? (snapshot.Devices.Count > 0 && snapshot.Devices.All(d => d.ProblemCode == 0)
                ? T("Nenhuma ação necessária pelos códigos PnP observados. Atualizações permanecem não verificadas até a consulta.", "No action needed based on observed PnP codes. Updates remain unverified until discovery.")
                : T("Sem problema confirmado; códigos de diagnóstico ausentes não comprovam ausência de falhas.", "No confirmed problem; missing diagnostic codes do not establish absence of faults."))
                : T("Problemas detectados:", "Detected problems:")));
            foreach (var device in problems) panel.Children.Add(Text(device.Name + ": " + (device.ProblemCode == 28 ? T("Driver ausente", "Driver missing") : T("Problema detectado", "Problem detected")) + " — " + device.ProblemCode));
            if (discovery is not null)
            {
                foreach (var warning in discovery.Warnings) panel.Children.Add(Text(warning));
                if (discovery.Candidates.Count == 0) panel.Children.Add(Text(T("Nenhum candidato confirmado encontrado. Isso não comprova ausência de atualizações.", "No confirmed candidate found. This does not prove that no updates exist.")));
                foreach (var candidate in discovery.Candidates)
                {
                    var devices = snapshot.Devices.Where(d => candidate.HardwareIds.Any(id => d.HardwareIds.Concat(d.CompatibleIds).Contains(id, StringComparer.OrdinalIgnoreCase))).ToArray();
                    if (devices.Length == 0) panel.Children.Add(Text(candidate.Title + " — " + T("Atualização não verificada: identificador não associado ao inventário.", "Unverified update: identifier not associated with the inventory.")));
                    foreach (var device in devices)
                    {
                        var match = DriverMatching.Evaluate(device, snapshot.System, candidate);
                        panel.Children.Add(Text($"{device.Name}\n{candidate.Title}\n{candidate.Manufacturer} | {candidate.Version ?? "UNKNOWN"} | {candidate.Source}\n{candidate.SizeBytes?.ToString() ?? "UNKNOWN"} bytes (estimate) | {candidate.PackageType}\n" +
                            (match.Confirmed ? T("Atualização compatível confirmada pela aplicabilidade WUA", "Compatible update confirmed by WUA applicability") : T("Atualização não verificada", "Unverified update")) + $"\n{match.Reason}\n{candidate.Evidence}"));
                        var key = device.Key + ":" + candidate.Id;
                        var officialPage = new Button { Content = T("Abrir página oficial", "Open official page") };
                        officialPage.Click += (_, _) => OpenOfficialPage(candidate.OfficialPage); panel.Children.Add(officialPage);
                        if (downloads.TryGetValue(key, out var result)) panel.Children.Add(Text(DownloadState(result) + "\n" + result.Reason + "\nSHA-256: " + (result.Sha256 ?? "UNKNOWN") + "\n" + result.HashStatus + " | " + result.SignatureStatus + "\n" + result.Path));
                        var allowed = match.Confirmed && OfficialSources.CanDownload(candidate);
                        var download = new Button { Content = allowed ? T("Download disponível — baixar e verificar", "Download available — download and verify") : T("Download direto indisponível — consulta manual", "Direct download unavailable — manual lookup"), IsEnabled = allowed && !driverBusy && !scanning };
                        download.Click += async (_, _) => await DownloadDriver(candidate, device, key); panel.Children.Add(download);
                    }
                }
            }
        }
        return new TabItem { Header = T("Drivers e Downloads", "Drivers and Downloads"), Content = new ScrollViewer { Content = panel } };
    }
    private string DownloadState(DownloadResult result) => result.Status switch
    {
        "DownloadCompleted" => T("Download concluído • Requer instalação manual", "Download completed • Manual installation required"),
        "VerificationIncomplete" => T("Verificação incompleta • Arquivo em quarentena", "Verification incomplete • File quarantined"),
        "VerificationFailed" => T("Verificação falhou", "Verification failed"),
        "Cancelled" => T("Operação cancelada", "Operation cancelled"),
        _ => T("Fonte indisponível ou falha de armazenamento", "Source unavailable or storage failure")
    };
    private async Task DiscoverDrivers()
    {
        if (snapshot is null || driverBusy || scanning) return;
        driverBusy = true; driverOperation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Build();
        status.Text = T("Consultando fontes…", "Querying sources…");
        try
        {
            IDriverDiscovery provider = OperatingSystem.IsWindows() ? new WindowsDriverDiscovery() : new UbuntuDriverDiscovery();
            discovery = await provider.DiscoverAsync(snapshot, driverOperation.Token);
            status.Text = T("Consulta concluída. Revise evidências e limites.", "Discovery completed. Review evidence and limitations.");
        }
        catch (OperationCanceledException) { status.Text = T("Consulta cancelada.", "Discovery cancelled."); }
        finally { driverBusy = false; driverOperation.Dispose(); driverOperation = null; if (!lifetime.IsCancellationRequested) Build(); }
    }
    private async Task DownloadDriver(DriverCandidate candidate, Device device, string key)
    {
        if (snapshot is null || driverBusy || scanning) return;
        driverBusy = true; driverOperation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Build();
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDriverGuardian", "Downloads");
            using var manager = new DriverDownloads(root, new WindowsSignatureVerifier());
            var progress = new Progress<DownloadProgress>(p => status.Text = $"{T("Recebidos", "Received")}: {p.BytesReceived:N0} / {p.TotalBytes?.ToString("N0") ?? "UNKNOWN"} bytes");
            downloads[key] = await manager.DownloadAsync(candidate, device, snapshot.System, progress, driverOperation.Token);
            status.Text = DownloadState(downloads[key]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { status.Text = T("Falha ao gravar download/registro local. Nenhum pacote foi instalado.", "Failed to write download/local journal. No package was installed."); }
        finally { driverBusy = false; driverOperation.Dispose(); driverOperation = null; if (!lifetime.IsCancellationRequested) Build(); }
    }
    private void OpenOfficialPage(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !OfficialSources.IsPage(uri)) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { status.Text = T("Não foi possível abrir o navegador.", "Could not open the browser."); }
    }
}
