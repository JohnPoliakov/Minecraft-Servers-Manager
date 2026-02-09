using Microsoft.Win32;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.UserControls;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

namespace Minecraft_Server_Manager.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Fields
        private object _currentView;
        private bool _showSidebar = true;
        private bool _isLoading = false;
        #endregion

        #region Properties
        /// <summary>
        /// Collection principale des profils de serveurs.
        /// </summary>
        public ObservableCollection<ServerProfile> Servers { get; set; }

        /// <summary>
        /// Contrôle la visibilité de la barre latérale.
        /// </summary>
        public bool ShowSidebar
        {
            get { return _showSidebar; }
            set { _showSidebar = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// La vue actuellement affichée dans la zone principale (ContentControl).
        /// </summary>
        public object CurrentView
        {
            get { return _currentView; }
            set
            {
                // Cleanup de l'ancien ViewModel pour éviter les fuites mémoire
                if (_currentView is ServerMonitorViewModel oldMonitorVm)
                {
                    oldMonitorVm.Cleanup();
                }

                _currentView = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Commands
        public ICommand EditServerCommand { get; private set; }
        public ICommand MonitorServerCommand { get; private set; }
        public ICommand AddServerCommand { get; private set; }
        public ICommand ShowHomeCommand { get; private set; }
        public ICommand ShowBrowserCommand { get; private set; }
        public ICommand ShowSettingsCommand { get; private set; }
        #endregion

        #region Constructor
        public MainViewModel()
        {
            // Appliquer le thème sauvegardé sans instancier un SettingsViewModel complet
            ApplySavedTheme();

            Servers = new ObservableCollection<ServerProfile>();

            EditServerCommand = new RelayCommand(param => EditServer((ServerProfile)param));
            MonitorServerCommand = new RelayCommand(param => MonitorServer((ServerProfile)param));
            AddServerCommand = new RelayCommand(o => AddServer());
            ShowHomeCommand = new RelayCommand(o => ShowHome());
            ShowBrowserCommand = new RelayCommand(o => ShowBrowser());
            ShowSettingsCommand = new RelayCommand(o => CurrentView = new SettingsViewModel());

            Servers.CollectionChanged += Servers_CollectionChanged;

            LoadServers();

            ShowHome();
        }
        #endregion

        #region Theme Initialization
        private void ApplySavedTheme()
        {
            ConfigManager.Load();

            var themes = new Dictionary<string, (string Primary, string Secondary)>
            {
                ["Dark Blue"] = ("#2c3e50", "#34495e"),
                ["Midnight Black"] = ("#000000", "#1a1a1a"),
                ["Forest Green"] = ("#1e392a", "#27ae60"),
                ["Deep Purple"] = ("#4a235a", "#8e44ad"),
                ["Ocean Blue"] = ("#154360", "#2980b9")
            };

            string themeName = ConfigManager.Settings.SelectedTheme ?? "Dark Blue";
            if (themes.TryGetValue(themeName, out var colors))
            {
                var primaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colors.Primary);
                var secondaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colors.Secondary);

                System.Windows.Application.Current.Resources["PrimaryBrush"] = new System.Windows.Media.SolidColorBrush(primaryColor);
                System.Windows.Application.Current.Resources["SecondaryBrush"] = new System.Windows.Media.SolidColorBrush(secondaryColor);
            }
        }
        #endregion

        #region Navigation Methods
        private void ShowHome()
        {
            CurrentView = new HomeViewModel(Servers, (profile) => MonitorServer(profile));
        }

        private void ShowBrowser()
        {
            CurrentView = new CurseForgeView();
        }

        public void MonitorServer(ServerProfile profile)
        {
            if (profile == null) return;
            CurrentView = new ServerMonitorViewModel(profile);
        }

        public void EditServer(ServerProfile profile)
        {
            if (profile == null) return;

            var editorVm = new ServerEditorViewModel(profile.FolderPath, profile);

            editorVm.OnConfigurationSaved += (savedProfile) => MonitorServer(savedProfile);
            editorVm.OnConfigurationDeleted += (profileToDelete) => DeleteServerImplementation(profileToDelete);

            CurrentView = editorVm;
        }
        #endregion

        #region Server Management (CRUD)

        private void Servers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Ne pas sauvegarder pendant le chargement initial
            if (_isLoading) return;

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                if (e.NewItems != null)
                {
                    foreach (ServerProfile newProfile in e.NewItems)
                    {
                        SaveNewProfile(newProfile);
                    }
                }
            }
        }

        private void LoadServers()
        {
            string appDataPath = ConfigFolderPath;

            if (!Directory.Exists(appDataPath)) return;

            string[] files = Directory.GetFiles(appDataPath, "*.json");

            _isLoading = true;
            try
            {
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
            finally
            {
                _isLoading = false;
            }
        }

        private void AddServer()
        {
            var dialog = new OpenFolderDialog
            {
                Title = ResourceHelper.GetString("Loc_SelectRootFolder"),
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
                        ResourceHelper.GetString("Loc_InvalidFolderMsg"),
                        ResourceHelper.GetString("Loc_InvalidFolderTitle"),
                        MessageBoxType.Error);
                    return;
                }

                var newProfile = new ServerProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    DisplayName = new DirectoryInfo(folderPath).Name,
                    FolderPath = folderPath,
                    JdkPath = "java",
                    JvmArguments = $"-Xmx{ConfigManager.Settings.DefaultRam}G -Xms{ConfigManager.Settings.DefaultRam}G"
                };

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
                string msg = string.Format(ResourceHelper.GetString("Loc_ErrorSave"), ex.Message);
                CustomMessageBox.Show(msg, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Info);
            }
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
                string msg = string.Format(ResourceHelper.GetString("Loc_ErrorDelete"), ex.Message);
                CustomMessageBox.Show(msg, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Info);
            }
        }
        #endregion

        #region Application Lifecycle
        /// <summary>
        /// Arrête proprement tous les serveurs actifs (appelé à la fermeture de l'app).
        /// </summary>
        public async Task StopAllServersAsync()
        {
            // Cleanup du ViewModel courant
            if (_currentView is ServerMonitorViewModel monitorVm)
            {
                monitorVm.Cleanup();
            }

            var tasks = new List<Task>();

            foreach (var server in Servers)
            {
                if (server.IsRunning && server.ServerProcess != null && !server.ServerProcess.HasExited)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            server.ServerProcess.StandardInput.WriteLine("stop");

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

                            // Dispose du Process après l'arrêt
                            server.ServerProcess?.Dispose();
                            server.ServerProcess = null;
                        }
                        catch
                        {
                            // Ignorer les erreurs de fermeture en cascade
                        }
                    }));
                }
            }

            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }
        #endregion

        #region Helpers & Config
        private string ConfigFolderPath
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "Minecraft Servers Manager");
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        #endregion
    }
}