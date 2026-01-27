using Microsoft.Web.WebView2.Core;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.ViewModels;
using Minecraft_Server_Manager.Views;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class CurseForgeView : System.Windows.Controls.UserControl
    {
        #region Fields
        private MainViewModel _mainVM;
        private bool _isInitialized = false;
        private string _tempIconUrl = null;

        private string _currentIconBase64 = null;
        #endregion

        #region Constructor & Lifecycle
        public CurseForgeView()
        {
            InitializeComponent();
            this.Loaded += CurseForgeView_Loaded;
        }

        private async void CurseForgeView_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= CurseForgeView_Loaded;
            if (!_isInitialized)
            {
                await InitializeWebView();
                _isInitialized = true;
            }
        }
        #endregion

        #region Initialization
        private async Task InitializeWebView()
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Minecraft Servers Manager",
                    "BrowserData");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    document.addEventListener('click', function(e) {
                        var target = e.target.closest('.download-cta'); 
                        if (target) {
                            var logo = document.querySelector('.project-logo');
                            if (logo && logo.src) {
                                window.chrome.webview.postMessage(logo.src);
                            }
                        }
                    });
                ");

                webView.Source = new Uri("https://www.curseforge.com/minecraft/search?class=modpacks&page=1&pageSize=20");

                if (System.Windows.Application.Current.MainWindow.DataContext is MainViewModel vm)
                {
                    _mainVM = vm;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur navigateur : {ex.Message}", "Erreur WebView2", MessageBoxType.Error);
            }
        }
        #endregion

        #region WebView2 Events Handlers
        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string url = e.TryGetWebMessageAsString();
            if (!string.IsNullOrEmpty(url))
            {
                _tempIconUrl = url;
            }
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string targetUrl = e.Uri;
            bool hasMinecraft = targetUrl.Contains("minecraft", StringComparison.OrdinalIgnoreCase);
            bool hasModpacks = targetUrl.Contains("modpacks", StringComparison.OrdinalIgnoreCase);
            bool isDownload = targetUrl.Contains("/download", StringComparison.OrdinalIgnoreCase)
                           || targetUrl.Contains("api-key", StringComparison.OrdinalIgnoreCase)
                           || targetUrl.Contains(".zip", StringComparison.OrdinalIgnoreCase);

            if (isDownload || (hasMinecraft && hasModpacks)) return;
            e.Cancel = true;
        }

        private void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string fileName = Path.GetFileName(e.ResultFilePath);
            bool isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            bool isServerPack = fileName.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isZip || !isServerPack)
            {
                e.Cancel = true;
                e.Handled = true;
                Dispatcher.Invoke(() =>
                {
                    CustomMessageBox.Show(
                        $"Le fichier '{fileName}' a été ignoré.\nSeuls les fichiers '.zip' contenant 'Server' sont acceptés.",
                        ResourceHelper.GetString("Loc_Error"), MessageBoxType.Info);
                });
                return;
            }

            string iconUrlToDownload = _tempIconUrl;
            _currentIconBase64 = null;

            Dispatcher.Invoke(() =>
            {
                webView.Visibility = Visibility.Hidden;
                ServerInstallBlock.Visibility = Visibility.Visible;

                ServerPackIcon.Source = null;

                SetLoadingMode(false);
                StopProgress.Offset = 0;
                StopProgressFade.Offset = 0;
                ServerInstallStatus.Text = ResourceHelper.GetString("Loc_ServerDownloading");
                if (ProgressText != null) ProgressText.Text = "0%";

                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null) mainWindow.IsEnabled = false;
            });

            if (!string.IsNullOrEmpty(iconUrlToDownload))
            {
                Task.Run(async () => await LoadPreviewImage(iconUrlToDownload));
            }

            string tempFolder = Path.Combine(Path.GetTempPath(), "MSM_Downloads");
            Directory.CreateDirectory(tempFolder);
            string destinationPath = Path.Combine(tempFolder, fileName);

            e.ResultFilePath = destinationPath;
            e.Handled = true;

            e.DownloadOperation.BytesReceivedChanged += (s, args) =>
            {
                var downloadOp = (CoreWebView2DownloadOperation)s;
                Dispatcher.Invoke(() => UpdateProgress(downloadOp.BytesReceived, downloadOp.TotalBytesToReceive));
            };

            e.DownloadOperation.StateChanged += (s, args) =>
            {
                var downloadOp = (CoreWebView2DownloadOperation)s;
                if (downloadOp.State == CoreWebView2DownloadState.Completed)
                {
                    Dispatcher.Invoke(() => InstallModpack(destinationPath));
                }
                else if (downloadOp.State == CoreWebView2DownloadState.Interrupted)
                {
                    Dispatcher.Invoke(() =>
                    {
                        ResetUI();
                        CustomMessageBox.Show("Le téléchargement a été interrompu.", ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
                    });
                }
            };
        }
        #endregion

        #region Image Handling (NOUVEAU)

        private async Task LoadPreviewImage(string url)
        {
            string base64 = await DownloadImageAsBase64(url);
            if (base64 != null)
            {
                _currentIconBase64 = base64;

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        ServerPackIcon.Source = ResourceHelper.CreateImageFromBase64(base64);
                        ServerPackIcon.Margin = new Thickness(0, 0, 0, 20);
                    }
                    catch { }
                });
            }
        }
        #endregion

        #region Installation Logic
        private async void InstallModpack(string zipPath)
        {
            try
            {
                ServerInstallStatus.Text = ResourceHelper.GetString("Loc_ModpackDownloading");
                SetLoadingMode(true);
                if (ProgressText != null) ProgressText.Text = "";

                string packName = Path.GetFileNameWithoutExtension(zipPath);
                ConfigManager.Load();
                string finalServerFolder = Path.Combine(ConfigManager.Settings.DefaultServerPath, packName);

                if (Directory.Exists(finalServerFolder))
                    finalServerFolder += "_" + DateTime.Now.Ticks;

                await Task.Run(() =>
                {
                    Directory.CreateDirectory(finalServerFolder);
                    ZipFile.ExtractToDirectory(zipPath, finalServerFolder);

                    var subDirs = Directory.GetDirectories(finalServerFolder);
                    var files = Directory.GetFiles(finalServerFolder);
                    if (files.Length == 0 && subDirs.Length == 1)
                    {
                        string subDir = subDirs[0];
                        foreach (var file in Directory.GetFiles(subDir))
                        {
                            string dest = Path.Combine(finalServerFolder, Path.GetFileName(file));
                            if (!File.Exists(dest)) File.Move(file, dest);
                        }
                        foreach (var dir in Directory.GetDirectories(subDir))
                        {
                            string dest = Path.Combine(finalServerFolder, new DirectoryInfo(dir).Name);
                            if (!Directory.Exists(dest)) Directory.Move(dir, dest);
                        }
                        Directory.Delete(subDir);
                    }
                });

                var newProfile = new ServerProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = packName,
                    FolderPath = finalServerFolder,
                    JdkPath = "java",
                    JvmArguments = $"-Xmx{ConfigManager.Settings.DefaultRam}G -Xms{ConfigManager.Settings.DefaultRam}G",
                    JarName = DetectServerJar(finalServerFolder),
                    IconBase64 = _currentIconBase64
                };

                if (_mainVM != null)
                {
                    _mainVM.Servers.Add(newProfile);
                    _mainVM.MonitorServer(newProfile);

                }

                CustomMessageBox.Show($"Le modpack '{packName}' est installé !", ResourceHelper.GetString("Loc_Success"), MessageBoxType.Info);
                try { File.Delete(zipPath); } catch { }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur durant l'installation : {ex.Message}", ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
            }
            finally
            {
                ResetUI();
            }
        }

        private async Task<string> DownloadImageAsBase64(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    byte[] imageBytes = await client.GetByteArrayAsync(url);
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region UI Management Helpers
        public void UpdateProgress(long bytesReceived, ulong? totalBytes)
        {
            if (totalBytes == null || totalBytes == 0)
            {
                SetLoadingMode(false);
                StopProgress.Offset = 0;
                StopProgressFade.Offset = 0;
                return;
            }
            double ratio = (double)bytesReceived / (double)totalBytes;

            StopProgress.Offset = ratio;
            StopProgressFade.Offset = ratio;
            if (ProgressText != null)
                ProgressText.Text = $"{ratio * 100:0}%";
        }

        private void ResetUI()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null) mainWindow.IsEnabled = true;

            webView.Visibility = Visibility.Visible;
            ServerInstallBlock.Visibility = Visibility.Hidden;
            ServerPackIcon.Source = null;
        }
        #endregion

        #region Utility Methods
        private string DetectServerJar(string folder)
        {
            if (!Directory.Exists(folder)) return "";
            var jars = Directory.GetFiles(folder, "*.jar");
            foreach (var jar in jars)
            {
                string name = Path.GetFileName(jar).ToLower();
                if (name.Contains("server") || name.Contains("forge") || name.Contains("fabric") || name.Contains("run"))
                    return Path.GetFileName(jar);
            }
            return jars.Length > 0 ? Path.GetFileName(jars[0]) : "";
        }

        private void SetLoadingMode(bool isIndeterminate)
        {
            if (isIndeterminate)
            {
                LoadingBarInfinite.Visibility = Visibility.Visible;
                LoadingBarDeterminate.Visibility = Visibility.Hidden;
                if (ProgressText != null) ProgressText.Text = "";
            }
            else
            {
                LoadingBarInfinite.Visibility = Visibility.Hidden;
                LoadingBarDeterminate.Visibility = Visibility.Visible;
            }
        }
        #endregion
    }
}