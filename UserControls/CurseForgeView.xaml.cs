using Microsoft.Web.WebView2.Core;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.ViewModels;
using Minecraft_Server_Manager.Views;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class CurseForgeView : System.Windows.Controls.UserControl
    {
        #region Fields
        private MainViewModel _mainVM;
        private bool _isInitialized = false;
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

                // 3. Abonnement aux événements
                webView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;

                webView.Source = new Uri("https://www.curseforge.com/minecraft/search?class=modpacks&page=1&pageSize=20");

                if (System.Windows.Application.Current.MainWindow.DataContext is MainViewModel vm)
                {
                    _mainVM = vm;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur navigateur : {ex.Message}", "Erreur WebView2");
            }
        }
        #endregion

        #region WebView2 Events Handlers
        /// <summary>
        /// Filtre la navigation pour empêcher de sortir de la section Minecraft/Modpacks.
        /// </summary>
        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string targetUrl = e.Uri;

            bool hasMinecraft = targetUrl.Contains("minecraft", StringComparison.OrdinalIgnoreCase);
            bool hasModpacks = targetUrl.Contains("modpacks", StringComparison.OrdinalIgnoreCase);

            if (!hasMinecraft || !hasModpacks)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Intercepte le téléchargement pour l'installer manuellement.
        /// </summary>
        private void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string fileName = Path.GetFileName(e.ResultFilePath);

            bool isZip = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            bool isServerPack = fileName.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isZip || !isServerPack)
            {
                e.Cancel = true;
                e.Handled = true;

                CustomMessageBox.Show(
                    $"Le fichier '{fileName}' a été bloqué.\n\n" +
                    "Seuls les packs serveur sont autorisés.",
                    "Téléchargement Refusé", MessageBoxType.Info);
                return;
            }

            webView.Visibility = Visibility.Hidden;
            ServerInstallBlock.Visibility = Visibility.Visible;

            LoadingBar.IsIndeterminate = false;
            LoadingBar.Value = 0;
            ServerInstallStatus.Text = "Téléchargement du serveur...";
            if (ProgressText != null) ProgressText.Text = "0%";

            string tempFolder = Path.Combine(Path.GetTempPath(), "MSM_Downloads");
            Directory.CreateDirectory(tempFolder);
            string destinationPath = Path.Combine(tempFolder, fileName);

            e.ResultFilePath = destinationPath;
            e.Handled = true;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null) mainWindow.IsEnabled = false;

            e.DownloadOperation.BytesReceivedChanged += (s, args) =>
            {
                var downloadOp = (CoreWebView2DownloadOperation)s;
                Dispatcher.Invoke(() =>
                {
                    UpdateProgress(downloadOp.BytesReceived, downloadOp.TotalBytesToReceive);
                });
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
                        CustomMessageBox.Show("Le téléchargement a été interrompu.", "Erreur");
                    });
                }
            };
        }
        #endregion

        #region Installation Logic
        /// <summary>
        /// Logique métier d'extraction et d'installation du serveur.
        /// </summary>
        private async void InstallModpack(string zipPath)
        {
            try
            {
                ServerInstallStatus.Text = "Installation du serveur...\nCela peut prendre quelques instants.";
                LoadingBar.IsIndeterminate = true;
                if (ProgressText != null) ProgressText.Text = "";

                string packName = Path.GetFileNameWithoutExtension(zipPath);

                ConfigManager.Load();
                string baseInstallPath = ConfigManager.Settings.DefaultServerPath;
                string finalServerFolder = Path.Combine(baseInstallPath, packName);

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
                            File.Move(file, dest);
                        }
                        foreach (var dir in Directory.GetDirectories(subDir))
                        {
                            string dest = Path.Combine(finalServerFolder, new DirectoryInfo(dir).Name);
                            Directory.Move(dir, dest);
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
                    JarName = DetectServerJar(finalServerFolder)
                };

                if (_mainVM != null)
                {
                    _mainVM.Servers.Add(newProfile);
                    _mainVM.ShowHomeCommand.Execute(null);
                }

                CustomMessageBox.Show($"Le modpack '{packName}' est installé !", "Succès");

                File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Erreur durant l'installation : " + ex.Message);
            }
            finally
            {
                ResetUI();
            }
        }
        #endregion

        #region UI Management Helpers
        public void UpdateProgress(long bytesReceived, ulong? totalBytes)
        {
            if (totalBytes == null || totalBytes == 0)
            {
                LoadingBar.IsIndeterminate = true;
                return;
            }

            double percentage = (double)bytesReceived / (double)totalBytes * 100;
            LoadingBar.Value = percentage;

            if (ProgressText != null)
                ProgressText.Text = $"{percentage:0}%";
        }

        private void ResetUI()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null) mainWindow.IsEnabled = true;

            webView.Visibility = Visibility.Visible;
            ServerInstallBlock.Visibility = Visibility.Hidden;
        }
        #endregion

        #region Utility Methods
        private string DetectServerJar(string folder)
        {
            var jars = Directory.GetFiles(folder, "*.jar");
            foreach (var jar in jars)
            {
                string name = Path.GetFileName(jar).ToLower();
                if (name.Contains("server") || name.Contains("forge") || name.Contains("fabric"))
                    return Path.GetFileName(jar);
            }
            return jars.Length > 0 ? Path.GetFileName(jars[0]) : "";
        }
        #endregion
    }
}