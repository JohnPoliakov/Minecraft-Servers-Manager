using Microsoft.Web.WebView2.Core;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.ViewModels;
using Minecraft_Server_Manager.Views;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace Minecraft_Server_Manager.UserControls;

public partial class CurseForgeView : System.Windows.Controls.UserControl, IDisposable, IAsyncDisposable
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operationCancellation;
    private CoreWebView2DownloadOperation? _download;
    private Action? _detachDownload;
    private bool _disposed;
    private string? _iconUrl;
    private Task _operationTask = Task.CompletedTask;

    public CurseForgeView() { InitializeComponent(); Loaded += Initialize; }
    private async void Initialize(object sender, RoutedEventArgs e)
    {
        Loaded -= Initialize;
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, AppPaths.BrowserData);
            if (_disposed) return;
            await webView.EnsureCoreWebView2Async(environment);
            if (_disposed) return;
            webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            webView.CoreWebView2.NavigationStarting += NavigationStarting;
            webView.CoreWebView2.WebMessageReceived += MessageReceived;
            webView.CoreWebView2.DownloadStarting += DownloadStarting;
            webView.CoreWebView2.NewWindowRequested += NewWindowRequested;
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('click', e => { if(e.target.closest('.download-cta')) { const logo = document.querySelector('.project-logo'); if(logo && logo.src) window.chrome.webview.postMessage(logo.src); } });");
            if (!_disposed) webView.Source = new Uri("https://www.curseforge.com/minecraft/search?class=modpacks&page=1&pageSize=20");
        }
        catch (Exception ex) { if (!_disposed) ShowError(ex); }
    }
    private void NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!ServerInstaller.IsTrustedUri(e.Uri)) e.Cancel = true;
        _iconUrl = null;
    }
    private void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (ServerInstaller.IsTrustedUri(e.Uri)) webView.CoreWebView2.Navigate(e.Uri);
    }
    private void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!ServerInstaller.IsTrustedUri(e.Source)) return;
        try { string url = e.TryGetWebMessageAsString(); if (ServerInstaller.IsTrustedUri(url, image: true)) _iconUrl = url; }
        catch (ArgumentException) { }
    }
    private void DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        string filename = Path.GetFileName(e.ResultFilePath);
        if (_disposed || _operationCancellation != null || !ServerInstaller.IsTrustedUri(e.DownloadOperation.Uri) || !filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        { e.Cancel = true; e.Handled = true; return; }
        try
        {
            string folder = Path.Combine(Path.GetTempPath(), "MSM-Download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            e.ResultFilePath = Path.Combine(folder, filename); e.Handled = true;
            _download = e.DownloadOperation;
            _operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _operationTask = DownloadAndInstallAsync(_download, e.ResultFilePath, _iconUrl, _operationCancellation.Token);
        }
        catch (Exception ex) { e.Cancel = true; ShowError(ex); }
    }
    private async Task DownloadAndInstallAsync(CoreWebView2DownloadOperation operation, string path, string? iconUrl, CancellationToken token)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<object> state = (_, _) =>
        {
            if (operation.State == CoreWebView2DownloadState.Completed) finished.TrySetResult();
            else if (operation.State == CoreWebView2DownloadState.Interrupted) finished.TrySetException(new IOException("Téléchargement interrompu : " + operation.InterruptReason));
        };
        EventHandler<object> progress = (_, _) =>
        {
            if (_disposed) return;
            double value = operation.TotalBytesToReceive is > 0 ? Math.Clamp(operation.BytesReceived / (double)operation.TotalBytesToReceive.Value, 0, 1) : 0;
            StopProgress.Offset = value; StopProgressFade.Offset = value; ProgressText.Text = $"{value:P0}";
        };
        operation.StateChanged += state; operation.BytesReceivedChanged += progress;
        _detachDownload = () => { operation.StateChanged -= state; operation.BytesReceivedChanged -= progress; };
        using var registration = token.Register(() => finished.TrySetCanceled(token));
        webView.Visibility = Visibility.Hidden; ServerInstallBlock.Visibility = Visibility.Visible;
        ServerInstallStatus.Text = ResourceHelper.GetString("Loc_ServerDownloading");
        try
        {
            await finished.Task;
            token.ThrowIfCancellationRequested();
            LoadingBarInfinite.Visibility = Visibility.Visible; LoadingBarDeterminate.Visibility = Visibility.Hidden;
            ServerInstallStatus.Text = ResourceHelper.GetString("Loc_InstallingPack");
            var profile = await ServerInstaller.InstallAsync(path, ConfigManager.Settings.DefaultServerPath, ConfigManager.Settings.DefaultRam, token);
            // Once publication succeeds, finish registering it even if navigation was requested.
            if (System.Windows.Application.Current.MainWindow.DataContext is MainViewModel main)
            {
                profile.IconBase64 = await DownloadIcon(iconUrl);
                main.RegisterServer(profile);
                if (!_disposed) main.EditServer(profile);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) ShowError(ex); }
        finally
        {
            _detachDownload?.Invoke(); _detachDownload = null;
            _download = null; _operationCancellation?.Dispose(); _operationCancellation = null;
            if (!_disposed)
            {
                webView.Visibility = Visibility.Visible; ServerInstallBlock.Visibility = Visibility.Hidden;
                LoadingBarInfinite.Visibility = Visibility.Hidden; LoadingBarDeterminate.Visibility = Visibility.Visible;
            }
            try { File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)!); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static async Task<string?> DownloadIcon(string? url)
    {
        if (url == null || !ServerInstaller.IsTrustedUri(url, image: true)) return null;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is not { } final || !ServerInstaller.IsTrustedUri(final.AbsoluteUri, image: true)) return null;
            if (response.Content.Headers.ContentLength is > 2097152) return null;
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            byte[] buffer = new byte[8192]; int read;
            while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (output.Length + read > 2097152) return null;
                output.Write(buffer, 0, read);
            }
            return Convert.ToBase64String(output.ToArray());
        }
        catch (HttpRequestException) { return null; }
        catch (OperationCanceledException) { return null; }
    }
    private void CancelInstall(object sender, RoutedEventArgs e)
    {
        _operationCancellation?.Cancel(); _download?.Cancel();
    }
    private static void ShowError(Exception ex) => CustomMessageBox.Show(ex.Message, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Loaded -= Initialize;
        _lifetime.Cancel(); _download?.Cancel(); _detachDownload?.Invoke(); _detachDownload = null;
        if (webView.CoreWebView2 is { } core)
        {
            core.NavigationStarting -= NavigationStarting; core.WebMessageReceived -= MessageReceived;
            core.DownloadStarting -= DownloadStarting; core.NewWindowRequested -= NewWindowRequested;
        }
        webView.Dispose(); _lifetime.Dispose();
    }
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _operationTask;
    }
}
