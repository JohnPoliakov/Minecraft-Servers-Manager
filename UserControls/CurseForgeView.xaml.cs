using Microsoft.Web.WebView2.Core;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.ViewModels;
using Minecraft_Server_Manager.Views;
using System.IO;
using System.IO.Compression;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class CurseForgeView : System.Windows.Controls.UserControl
    {
        private MainViewModel _mainVM;
        private bool _isInitialized = false;

        public CurseForgeView()
        {
            InitializeComponent();

            this.Loaded += CurseForgeView_Loaded;
        }

        private async void CurseForgeView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Loaded -= CurseForgeView_Loaded;

            if (!_isInitialized)
            {
                await InitializeWebView();
                _isInitialized = true;
            }
        }

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

                webView.Source = new Uri("https://www.curseforge.com/minecraft/search?class=serverpacks");

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

        private void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            if (!e.ResultFilePath.EndsWith(".zip")) return;

            string tempFolder = Path.Combine(Path.GetTempPath(), "MSM_Downloads");
            Directory.CreateDirectory(tempFolder);

            string fileName = Path.GetFileName(e.ResultFilePath);
            string destinationPath = Path.Combine(tempFolder, fileName);

            e.ResultFilePath = destinationPath;

            e.DownloadOperation.StateChanged += (s, args) =>
            {
                var download = (CoreWebView2DownloadOperation)s;

                if (download.State == CoreWebView2DownloadState.Completed)
                {
                    InstallModpack(destinationPath);
                }
            };

            e.Handled = true;
        }

        private async void InstallModpack(string zipPath)
        {
            try
            {
                string packName = Path.GetFileNameWithoutExtension(zipPath);

                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string serverRoot = Path.Combine(documents, "Minecraft Servers Manager");
                string finalServerFolder = Path.Combine(serverRoot, packName);

                if (Directory.Exists(finalServerFolder))
                {
                    finalServerFolder += "_" + DateTime.Now.Ticks;
                }


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
                    JvmArguments = "-Xmx4G -Xms4G",
                    JarName = DetectServerJar(finalServerFolder)
                };

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _mainVM.Servers.Add(newProfile);

                    Views.CustomMessageBox.Show($"Le modpack '{packName}' a été installé avec succès !", "Installation Terminée");

                    _mainVM.ShowHomeCommand.Execute(null);
                });

                File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    Views.CustomMessageBox.Show("Erreur installation : " + ex.Message));
            }
        }

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
    }
}