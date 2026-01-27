using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Minecraft_Server_Manager.ViewModels
{
    public class ServerEditorViewModel : INotifyPropertyChanged
    {
        #region Fields & Events
        private ServerProfile _serverProfile;

        public event Action<ServerProfile> OnConfigurationSaved;
        public event Action<ServerProfile> OnConfigurationDeleted;
        #endregion

        #region Collections
        public ObservableCollection<PropertyItem> ServerProperties { get; set; }
        public ObservableCollection<JavaInstallation> DetectedJavaPaths { get; set; } = new ObservableCollection<JavaInstallation>();
        public ObservableCollection<WhitelistPlayer> WhitelistedPlayers { get; set; } = new ObservableCollection<WhitelistPlayer>();
        public ObservableCollection<OpPlayer> OpPlayers { get; set; } = new ObservableCollection<OpPlayer>();
        #endregion

        #region Bindable Properties

        // --- General Info ---
        public string DisplayName
        {
            get => _serverProfile.DisplayName;
            set { _serverProfile.DisplayName = value; OnPropertyChanged(); }
        }

        public string FolderPath => _serverProfile.FolderPath;

        // --- Java Configuration ---
        public string JavaPath
        {
            get => _serverProfile.JdkPath;
            set { _serverProfile.JdkPath = value; OnPropertyChanged(); }
        }

        public string JvmArguments
        {
            get => _serverProfile.JvmArguments;
            set { _serverProfile.JvmArguments = value; OnPropertyChanged(); }
        }

        public string JarName
        {
            get => _serverProfile.JarName;
            set { _serverProfile.JarName = value; OnPropertyChanged(); }
        }

        // --- Visuals ---
        private ImageSource _serverIconSource;
        public ImageSource ServerIconSource
        {
            get => _serverIconSource;
            set { _serverIconSource = value; OnPropertyChanged(); }
        }

        public string DiscordWebhookUrl
        {
            get => _serverProfile.DiscordWebhookUrl;
            set
            {
                if (_serverProfile.DiscordWebhookUrl != value)
                {
                    _serverProfile.DiscordWebhookUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoRestartEnabled
        {
            get => _serverProfile.AutoRestartEnabled;
            set
            {
                if (_serverProfile.AutoRestartEnabled != value)
                {
                    _serverProfile.AutoRestartEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public string AutoRestartTime
        {
            get => _serverProfile.AutoRestartTime;
            set
            {
                if (_serverProfile.AutoRestartTime != value)
                {
                    _serverProfile.AutoRestartTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsJavaMode
        {
            get => _serverProfile.LaunchMode == "Java";
            set
            {
                if (value) _serverProfile.LaunchMode = "Java";
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBatchMode));
            }
        }

        public bool IsBatchMode
        {
            get => _serverProfile.LaunchMode == "Batch";
            set
            {
                if (value) _serverProfile.LaunchMode = "Batch";
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsJavaMode));
            }
        }

        public string BatchFilename
        {
            get => _serverProfile.BatchFilename;
            set { _serverProfile.BatchFilename = value; OnPropertyChanged(); }
        }

        public bool ClearLogsOnStart
        {
            get => _serverProfile.ClearLogsOnStart;
            set
            {
                if (_serverProfile.ClearLogsOnStart != value)
                {
                    _serverProfile.ClearLogsOnStart = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region Commands
        public ICommand SaveCommand { get; set; }
        public ICommand DeleteCommand { get; set; }

        // File Pickers
        public ICommand SelectImageCommand { get; set; }
        public ICommand SelectJdkCommand { get; set; }
        public ICommand SelectJarCommand { get; set; }
        public ICommand SelectBatCommand { get; set; }

        // Tools
        public ICommand ApplyAikarFlagsCommand { get; set; }

        // Player Management
        public ICommand AddWhitelistCommand { get; set; }
        public ICommand RemoveWhitelistCommand { get; set; }
        public ICommand AddOpCommand { get; set; }
        public ICommand RemoveOpCommand { get; set; }
        #endregion

        #region Constructor
        public ServerEditorViewModel(string folderPath, ServerProfile? serverProfile = null)
        {
            _serverProfile = serverProfile ?? new ServerProfile
            {
                Id = Guid.NewGuid().ToString(),
                JdkPath = "java",
                JvmArguments = "-Xmx1024M -Xms1024M",
                FolderPath = folderPath,
                DisplayName = new DirectoryInfo(folderPath).Name
            };

            ServerProperties = new ObservableCollection<PropertyItem>();

            InitializeCommands();

            ScanForJavaInstallations();
            LoadImageFromBase64();
            LoadServerProperties();
            LoadPlayerLists();
        }

        private void InitializeCommands()
        {
            SaveCommand = new RelayCommand(SaveConfiguration);
            DeleteCommand = new RelayCommand(DeleteServer);

            SelectImageCommand = new RelayCommand(SelectImage);
            SelectJdkCommand = new RelayCommand(SelectJdk);
            SelectJarCommand = new RelayCommand(SelectJar);
            SelectBatCommand = new RelayCommand(SelectBat);

            ApplyAikarFlagsCommand = new RelayCommand(ApplyAikarFlags);

            AddWhitelistCommand = new RelayCommand(AddWhitelistPlayer);
            RemoveWhitelistCommand = new RelayCommand(p => RemovePlayer<WhitelistPlayer>(p, "whitelist.json", WhitelistedPlayers));
            AddOpCommand = new RelayCommand(AddOpPlayer);
            RemoveOpCommand = new RelayCommand(p => RemovePlayer<OpPlayer>(p, "ops.json", OpPlayers));
        }
        #endregion

        #region Initialization Logic (Loaders)
        private void ScanForJavaInstallations()
        {
            var foundPaths = new List<string>();
            var searchRoots = new List<string>
            {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\Zulu"
            };

            foreach (var root in searchRoots)
            {
                if (Directory.Exists(root))
                {
                    try
                    {
                        string[] directories = Directory.GetDirectories(root);
                        foreach (var dir in directories)
                        {
                            string javaExe = Path.Combine(dir, "bin", "java.exe");
                            if (File.Exists(javaExe))
                            {
                                foundPaths.Add(javaExe);
                            }
                        }
                    }
                    catch { }
                }
            }

            foundPaths.Add("java");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                DetectedJavaPaths.Clear();
                foreach (var path in foundPaths)
                {
                    DetectedJavaPaths.Add(new JavaInstallation
                    {
                        Path = path,
                        Name = GenerateFriendlyJavaName(path)
                    });
                }

                if (!string.IsNullOrEmpty(JavaPath) && File.Exists(JavaPath) && !DetectedJavaPaths.Any(x => x.Path == JavaPath))
                {
                    DetectedJavaPaths.Insert(0, new JavaInstallation
                    {
                        Path = JavaPath,
                        Name = $"{GenerateFriendlyJavaName(JavaPath)} (Personnalisé)"
                    });
                }
            });
        }

        private void LoadImageFromBase64()
        {
            if (!string.IsNullOrEmpty(_serverProfile.IconBase64))
            {
                try
                {
                    byte[] binaryData = Convert.FromBase64String(_serverProfile.IconBase64);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();

                    ServerIconSource = bi;
                }
                catch { }
            }
        }

        private void LoadServerProperties()
        {
            string path = Path.Combine(_serverProfile.FolderPath, "server.properties");

            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains("="))
                    {
                        var parts = line.Split(new[] { '=' }, 2);
                        ServerProperties.Add(new PropertyItem
                        {
                            Key = parts[0].Trim(),
                            Value = parts.Length > 1 ? parts[1].Trim() : ""
                        });
                    }
                }
            }
        }

        private void LoadPlayerLists()
        {
            LoadList("whitelist.json", WhitelistedPlayers);
            LoadList("ops.json", OpPlayers);
        }

        private void LoadList<T>(string fileName, ObservableCollection<T> collection)
        {
            string path = Path.Combine(_serverProfile.FolderPath, fileName);
            if (File.Exists(path))
            {
                try
                {
                    var items = JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path));
                    collection.Clear();
                    foreach (var item in items) collection.Add(item);
                }
                catch { }
            }
        }
        #endregion

        #region Command Actions - Configuration & Lifecycle
        private void SaveConfiguration(object obj)
        {
            string appDataPath = ConfigFolderPath;
            Directory.CreateDirectory(appDataPath);

            string jsonFile = Path.Combine(appDataPath, $"{_serverProfile.Id}.json");
            string jsonString = JsonSerializer.Serialize(_serverProfile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonFile, jsonString);

            SaveServerPropertiesFile();

            OnConfigurationSaved?.Invoke(_serverProfile);
        }

        private void DeleteServer(object obj)
        {
            string msg = string.Format(ResourceHelper.GetString("Loc_ConfirmDeleteMsg"), DisplayName);
            bool? result = CustomMessageBox.Show(
                msg,
                ResourceHelper.GetString("Loc_ConfirmDeleteTitle"),
                MessageBoxType.Confirmation);

            if (result == true)
            {
                OnConfigurationDeleted?.Invoke(_serverProfile);
            }
        }

        private void SaveServerPropertiesFile()
        {
            string path = Path.Combine(_serverProfile.FolderPath, "server.properties");
            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.WriteLine("#Minecraft Server Properties");
                writer.WriteLine($"#{DateTime.Now}");
                foreach (var item in ServerProperties)
                {
                    writer.WriteLine($"{item.Key}={item.Value}");
                }
            }
        }
        #endregion

        #region Command Actions - File Selection
        private void SelectJdk(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables Java (java.exe)|java.exe|Tous les fichiers|*.*",
                Title = ResourceHelper.GetString("Loc_SelectJavaTitle")
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;

                if (!DetectedJavaPaths.Any(x => x.Path == selectedPath))
                {
                    DetectedJavaPaths.Insert(0, new JavaInstallation
                    {
                        Path = selectedPath,
                        Name = $"{GenerateFriendlyJavaName(selectedPath)} ({ResourceHelper.GetString("Loc_JavaManual")})"
                    });
                }

                JavaPath = selectedPath;
            }
        }

        private void SelectJar(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Fichiers Executable Java (*.jar)|*.jar",
                Title = ResourceHelper.GetString("Loc_SelectJarTitle"),
                InitialDirectory = _serverProfile.FolderPath
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string fullPath = openFileDialog.FileName;
                string serverFolder = _serverProfile.FolderPath;

                if (fullPath.StartsWith(serverFolder, StringComparison.OrdinalIgnoreCase))
                {
                    JarName = fullPath.Substring(serverFolder.Length).TrimStart('\\', '/');
                }
                else
                {
                    JarName = fullPath;
                }
            }
        }

        private void SelectBat(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Batch Files (*.bat;*.cmd)|*.bat;*.cmd",
                Title = ResourceHelper.GetString("Loc_SelectBatTitle"),
                InitialDirectory = _serverProfile.FolderPath
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string fullPath = openFileDialog.FileName;
                if (fullPath.StartsWith(_serverProfile.FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    BatchFilename = Path.GetFileName(fullPath);
                }
                else
                {
                    BatchFilename = fullPath;
                }
            }
        }

        private void SelectImage(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = ResourceHelper.GetString("Loc_SelectIconTitle")
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string filePath = openFileDialog.FileName;
                    byte[] imageBytes = File.ReadAllBytes(filePath);

                    _serverProfile.IconBase64 = Convert.ToBase64String(imageBytes);
                    LoadImageFromBase64();
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(string.Format(ResourceHelper.GetString("Loc_ErrorImageLoad"), ex.Message));
                }
            }
        }
        #endregion

        #region Command Actions - Tools & Optimization
        private void ApplyAikarFlags(object obj)
        {
            string aikar = "-Xms8G -Xmx8G " +
                   "-XX:+UseG1GC " +
                   "-XX:+ParallelRefProcEnabled " +
                   "-XX:MaxGCPauseMillis=200 " +
                   "-XX:+UnlockExperimentalVMOptions " +
                   "-XX:+DisableExplicitGC " +
                   "-XX:+AlwaysPreTouch " +
                   "-XX:G1NewSizePercent=30 " +
                   "-XX:G1MaxNewSizePercent=40 " +
                   "-XX:G1HeapRegionSize=8M " +
                   "-XX:G1ReservePercent=20 " +
                   "-XX:G1HeapWastePercent=5 " +
                   "-XX:G1MixedGCCountTarget=4 " +
                   "-XX:InitiatingHeapOccupancyPercent=15 " +
                   "-XX:G1MixedGCLiveThresholdPercent=90 " +
                   "-XX:G1RSetUpdatingPauseTimePercent=5 " +
                   "-XX:SurvivorRatio=32 " +
                   "-XX:+PerfDisableSharedMem " +
                   "-XX:MaxTenuringThreshold=1 " +
                   "-Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true";

            JvmArguments = aikar;

            CustomMessageBox.Show(
                ResourceHelper.GetString("Loc_AikarMsg"),
                ResourceHelper.GetString("Loc_AikarTitle"),
                MessageBoxType.Info
            );
        }
        #endregion

        #region Command Actions - Player Management
        private async void AddWhitelistPlayer(object obj)
        {
            string username = CustomMessageBox.ShowInput(
                ResourceHelper.GetString("Loc_InputWhitelistMsg"),
                ResourceHelper.GetString("Loc_InputWhitelistTitle"));
            if (string.IsNullOrWhiteSpace(username)) return;

            var player = await MojangService.GetPlayerProfileAsync(username);

            if (player == null)
            {
                CustomMessageBox.Show(ResourceHelper.GetString("Loc_PlayerNotFound"), ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
                return;
            }

            if (WhitelistedPlayers.Any(p => p.Uuid == player.Uuid)) return;

            if (_serverProfile.IsRunning && _serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited)
            {
                try
                {
                    await _serverProfile.ServerProcess.StandardInput.WriteLineAsync($"whitelist add {player.Name}");
                }
                catch
                {
                }
            }

            WhitelistedPlayers.Add(player);
            SaveList("whitelist.json", WhitelistedPlayers);
        }

        private async void AddOpPlayer(object obj)
        {
            string username = CustomMessageBox.ShowInput(ResourceHelper.GetString("Loc_InputOpMsg"), ResourceHelper.GetString("Loc_InputOpTitle"));
            if (string.IsNullOrWhiteSpace(username)) return;

            var player = await MojangService.GetPlayerProfileAsync(username);
            if (player == null)
            {
                CustomMessageBox.Show(ResourceHelper.GetString("Loc_PlayerNotFound"), ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
                return;
            }

            if (OpPlayers.Any(p => p.Uuid == player.Uuid)) return;

            if (_serverProfile.IsRunning && _serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited)
            {
                try
                {
                    await _serverProfile.ServerProcess.StandardInput.WriteLineAsync($"op {player.Name}");
                }
                catch
                {
                }
            }

            var op = new OpPlayer { Name = player.Name, Uuid = player.Uuid, Level = 4, BypassesPlayerLimit = false };

            OpPlayers.Add(op);
            SaveList("ops.json", OpPlayers);
        }

        private void RemovePlayer<T>(object param, string fileName, ObservableCollection<T> collection)
        {
            if (param is T player)
            {
                collection.Remove(player);
                SaveList(fileName, collection);

                if (_serverProfile.IsRunning && _serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited)
                {
                    if (fileName == "ops.json")
                    {
                        try
                        {
                            if (player is OpPlayer opPlayer)
                            {
                                _serverProfile.ServerProcess.StandardInput.WriteLineAsync($"deop {opPlayer.Name}");
                            }
                        }
                        catch
                        {
                        }
                    }
                    else if (fileName == "whitelist.json")
                    {
                        try
                        {
                            if (player is OpPlayer opPlayer)
                            {
                                _serverProfile.ServerProcess.StandardInput.WriteLineAsync($"whitelist remove {opPlayer.Name}");
                            }
                        }
                        catch
                        {
                        }
                    }
                }


            }
        }

        private void SaveList<T>(string fileName, ObservableCollection<T> collection)
        {
            try
            {
                string path = Path.Combine(_serverProfile.FolderPath, fileName);
                string json = JsonSerializer.Serialize(collection, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Erreur sauvegarde {fileName} : {ex.Message}");
            }
        }
        #endregion

        #region Helpers & Utils
        private string ConfigFolderPath
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "Minecraft Servers Manager");
            }
        }

        private string GenerateFriendlyJavaName(string javaPath)
        {
            if (javaPath == "java") return "Java (Défaut Système)";

            try
            {
                FileInfo fileInfo = new FileInfo(javaPath);
                DirectoryInfo binDir = fileInfo.Directory;
                DirectoryInfo versionDir = binDir?.Parent;
                DirectoryInfo vendorDir = versionDir?.Parent;

                string versionName = versionDir?.Name ?? "Version Inconnue";
                string vendorName = vendorDir?.Name ?? "Local";

                if (vendorName.Contains("Program Files")) vendorName = "Système";

                return $"{versionName} ({vendorName})";
            }
            catch
            {
                return javaPath;
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}