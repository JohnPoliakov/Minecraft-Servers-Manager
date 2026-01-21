using Microsoft.Win32;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace Minecraft_Server_Manager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;

        public ObservableCollection<ServerProfile> Servers { get; set; }

        private bool _showSidebar = true;
        public bool ShowSidebar
        {
            get { return _showSidebar; }
            set { _showSidebar = value; OnPropertyChanged(); }
        }

        public object CurrentView
        {
            get { return _currentView; }
            set { _currentView = value; OnPropertyChanged(); }
        }

        private string ConfigFolderPath
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "Minecraft Servers Manager");
            }
        }

        public ICommand EditServerCommand { get; private set; }
        public ICommand MonitorServerCommand { get; private set; }
        public ICommand AddServerCommand { get; private set; }

        public MainViewModel()
        {
            Servers = new ObservableCollection<ServerProfile>();
            EditServerCommand = new RelayCommand(param => EditServer((ServerProfile)param));
            MonitorServerCommand = new RelayCommand(param => MonitorServer((ServerProfile)param));
            AddServerCommand = new RelayCommand(o => AddServer());

            LoadServers();

            if (Servers.Count > 0)
                MonitorServer(Servers[0]);
        }

        public void EditServer(ServerProfile profile)
        {
            if (profile == null) return;

            var editorVm = new ServerEditorViewModel(profile.FolderPath, profile);

            editorVm.OnConfigurationSaved += (savedProfile) => MonitorServer(savedProfile);

            editorVm.OnConfigurationDeleted += (profileToDelete) => DeleteServerImplementation(profileToDelete);

            CurrentView = editorVm;
        }

        private void DeleteServerImplementation(ServerProfile profile)
        {
            try
            {
                string appDataPath = ConfigFolderPath;
                string jsonFile = Path.Combine(appDataPath, $"{profile.Id}.json");

                if (File.Exists(jsonFile))
                {
                    File.Delete(jsonFile);
                }

                Servers.Remove(profile);

                if (Servers.Count > 0)
                {
                    MonitorServer(Servers[0]);
                }
                else
                {
                    CurrentView = null;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur lors de la suppression : {ex.Message}", "Erreur", MessageBoxType.Info);
            }
        }

        public void MonitorServer(ServerProfile profile)
        {
            if (profile == null) return;

            CurrentView = new ServerMonitorViewModel(profile);
        }

        private void AddServer()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Sélectionnez le dossier racine de votre serveur Minecraft",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = dialog.FolderName;

                string propFile = Path.Combine(folderPath, "server.properties");
                string eulaFile = Path.Combine(folderPath, "eula.txt");

                if (!File.Exists(propFile) || !File.Exists(eulaFile))
                {
                    CustomMessageBox.Show(
                        "Ce dossier ne semble pas contenir un serveur Minecraft valide.\n\nFichiers manquants : server.properties et/ou eula.txt",
                        "Dossier Invalide",
                        MessageBoxType.Error);
                    return;
                }

                var newProfile = new ServerProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = new DirectoryInfo(folderPath).Name,
                    FolderPath = folderPath,
                    JdkPath = "java",
                    JvmArguments = "-Xmx2G -Xms2G"
                };

                SaveNewProfile(newProfile);

                Servers.Add(newProfile);
                EditServer(newProfile);
            }
        }

        private void SaveNewProfile(ServerProfile profile)
        {
            try
            {
                string appDataPath = ConfigFolderPath;
                Directory.CreateDirectory(appDataPath);

                string jsonFile = Path.Combine(appDataPath, $"{profile.Id}.json");
                string jsonString = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(jsonFile, jsonString);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur lors de la sauvegarde du profil : {ex.Message}", "Erreur", MessageBoxType.Info);
            }
        }
        private void LoadServers()
        {
            string appDataPath = ConfigFolderPath;

            if (!Directory.Exists(appDataPath)) return;

            string[] files = Directory.GetFiles(appDataPath, "*.json");

            foreach (string file in files)
            {
                try
                {
                    string jsonString = File.ReadAllText(file);

                    ServerProfile profile = JsonSerializer.Deserialize<ServerProfile>(jsonString);

                    if (profile != null)
                    {
                        Servers.Add(profile);
                    }
                }
                catch
                {

                }
            }
        }

        public async Task StopAllServersAsync()
        {
            var tasks = new List<Task>();

            foreach (var server in Servers)
            {
                // On vérifie si le serveur a un processus actif
                if (server.IsRunning && server.ServerProcess != null && !server.ServerProcess.HasExited)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            server.ServerProcess.StandardInput.WriteLine("stop");

                            // On attend jusqu'à 15 secondes que le serveur s'éteigne proprement
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                            {
                                try
                                {
                                    await server.ServerProcess.WaitForExitAsync(cts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    server.ServerProcess.Kill();
                                }
                            }
                        }
                        catch
                        {
                        }
                    }));
                }
            }

            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}