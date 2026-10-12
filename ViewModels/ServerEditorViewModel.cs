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

namespace Minecraft_Server_Manager.ViewModels
{
    public class ServerEditorViewModel : INotifyPropertyChanged
    {
        #region Fields & Events
        private ServerProfile _serverProfile;
        private readonly ServerProfile _originalProfile;
        private readonly ServerSession _session;
        private readonly ServerCatalog _catalog;
        private readonly ServerConfiguration _configuration;

        public event Action<ServerProfile>? OnConfigurationSaved;
        public event Func<ServerProfile, Task>? OnConfigurationDeleted;
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
        private ImageSource? _serverIconSource;
        public ImageSource? ServerIconSource
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
        public ServerEditorViewModel(string folderPath, ServerProfile serverProfile, ServerSession session, ServerCatalog catalog)
        {
            _originalProfile = serverProfile;
            _serverProfile = ServerCatalog.Clone(serverProfile);
            _session = session;
            _catalog = catalog;
            _configuration = new ServerConfiguration(folderPath);

            ServerProperties = new ObservableCollection<PropertyItem>();

            InitializeCommands();

            ScanForJavaInstallations();
            LoadImageFromBase64();
            LoadServerProperties();
            LoadPlayerLists();
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(SaveCommand), nameof(DeleteCommand), nameof(SelectImageCommand),
            nameof(SelectJdkCommand), nameof(SelectJarCommand), nameof(SelectBatCommand), nameof(ApplyAikarFlagsCommand),
            nameof(AddWhitelistCommand), nameof(RemoveWhitelistCommand), nameof(AddOpCommand), nameof(RemoveOpCommand))]
        private void InitializeCommands()
        {
            SaveCommand = new AsyncRelayCommand(_ => { SaveConfiguration(null); return Task.CompletedTask; }, ShowError);
            DeleteCommand = new AsyncRelayCommand(DeleteServer, ShowError);

            SelectImageCommand = new RelayCommand(SelectImage);
            SelectJdkCommand = new RelayCommand(SelectJdk);
            SelectJarCommand = new RelayCommand(SelectJar);
            SelectBatCommand = new RelayCommand(SelectBat);

            ApplyAikarFlagsCommand = new RelayCommand(ApplyAikarFlags);

            AddWhitelistCommand = new AsyncRelayCommand(AddWhitelistPlayer, ShowError);
            RemoveWhitelistCommand = new AsyncRelayCommand(p => RemovePlayer(p, false), ShowError);
            AddOpCommand = new AsyncRelayCommand(AddOpPlayer, ShowError);
            RemoveOpCommand = new AsyncRelayCommand(p => RemovePlayer(p, true), ShowError);
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
                    ServerIconSource = ResourceHelper.CreateImageFromBase64(_serverProfile.IconBase64);
                }
                catch { }
            }
        }

        private void LoadServerProperties()
        {
            foreach (var item in _configuration.Read()) ServerProperties.Add(item);
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
                    foreach (var item in items ?? []) collection.Add(item);
                }
                catch (Exception ex) { ShowError(ex); }
            }
        }
        #endregion

        #region Command Actions - Configuration & Lifecycle
        private void SaveConfiguration(object? obj)
        {
            ServerCatalog.Validate(_serverProfile);
            _configuration.Save(ServerProperties);
            _catalog.Save(_serverProfile);
            ServerCatalog.Apply(_serverProfile, _originalProfile);
            OnConfigurationSaved?.Invoke(_originalProfile);
        }

        private async Task DeleteServer(object? obj)
        {
            string msg = string.Format(ResourceHelper.GetString("Loc_ConfirmDeleteMsg"), DisplayName);
            if (_session.Snapshot.IsActive) msg += "\n\n" + ResourceHelper.GetString("Loc_DeleteStopsServer");
            if (CustomMessageBox.Show(msg, ResourceHelper.GetString("Loc_ConfirmDeleteTitle"), MessageBoxType.Confirmation) == true &&
                OnConfigurationDeleted != null)
                await OnConfigurationDeleted(_originalProfile);
        }

        private static void ShowError(Exception ex) =>
            CustomMessageBox.Show(ex.Message, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);


        #endregion

        #region Command Actions - File Selection
        private void SelectJdk(object? obj)
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

        private void SelectJar(object? obj)
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

                JarName = Path.GetRelativePath(serverFolder, fullPath);
            }
        }

        private void SelectBat(object? obj)
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
                BatchFilename = Path.GetRelativePath(_serverProfile.FolderPath, fullPath);
            }
        }

        private void SelectImage(object? obj)
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
        private void ApplyAikarFlags(object? obj)
        {
            JvmArguments = JavaArguments.ApplyG1Preset(JvmArguments ?? "", ConfigManager.Settings.DefaultRam);

            CustomMessageBox.Show(
                ResourceHelper.GetString("Loc_AikarMsg"),
                ResourceHelper.GetString("Loc_AikarTitle"),
                MessageBoxType.Info
            );
        }
        #endregion

        #region Command Actions - Player Management
        private async Task AddWhitelistPlayer(object? obj) => await AddPlayer(false);
        private async Task AddOpPlayer(object? obj) => await AddPlayer(true);
        private async Task AddPlayer(bool op)
        {
            string? username = CustomMessageBox.ShowInput(
                ResourceHelper.GetString(op ? "Loc_InputOpMsg" : "Loc_InputWhitelistMsg"),
                ResourceHelper.GetString(op ? "Loc_InputOpTitle" : "Loc_InputWhitelistTitle"));
            if (string.IsNullOrWhiteSpace(username)) return;
            var player = await MojangService.GetPlayerProfileAsync(username.Trim());
            if (player == null) throw new InvalidOperationException(ResourceHelper.GetString("Loc_PlayerNotFound"));
            await _session.ChangePermissionAsync(player, op, false);
            if (_session.Snapshot.IsActive)
            {
                CustomMessageBox.Show(ResourceHelper.GetString("Loc_PermissionSent"));
                // The server owns the live files; the UI does not claim command success.
                LoadPlayerLists();
            }
            else LoadPlayerLists();
        }
        private async Task RemovePlayer(object? parameter, bool op)
        {
            if (parameter is not WhitelistPlayer player) return;
            await _session.ChangePermissionAsync(player, op, true);
            if (_session.Snapshot.IsActive) CustomMessageBox.Show(ResourceHelper.GetString("Loc_PermissionSent"));
            LoadPlayerLists();
        }


        #endregion

        #region Helpers & Utils
        private string GenerateFriendlyJavaName(string javaPath)
        {
            if (javaPath == "java") return "Java (Défaut Système)";

            try
            {
                FileInfo fileInfo = new FileInfo(javaPath);
                DirectoryInfo? binDir = fileInfo.Directory;
                DirectoryInfo? versionDir = binDir?.Parent;
                DirectoryInfo? vendorDir = versionDir?.Parent;

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
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
