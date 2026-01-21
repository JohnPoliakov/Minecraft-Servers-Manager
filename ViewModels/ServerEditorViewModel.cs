using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
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
        private ServerProfile _serverProfile;
        public ObservableCollection<PropertyItem> ServerProperties { get; set; }
        public ObservableCollection<string> DetectedJavaPaths { get; set; } = new ObservableCollection<string>();


        private string ConfigFolderPath
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documents, "Minecraft Servers Manager");
            }
        }
        public string DisplayName
        {
            get => _serverProfile.DisplayName;
            set { _serverProfile.DisplayName = value; OnPropertyChanged(); }
        }

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

        public string FolderPath => _serverProfile.FolderPath;

        public ICommand SaveCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand SelectImageCommand { get; set; }
        public ICommand SelectJdkCommand { get; set; }
        public ICommand SelectJarCommand { get; set; }
        public ICommand ApplyAikarFlagsCommand { get; set; }
        public ICommand TestWebhookCommand { get; set; }

        public event Action<ServerProfile> OnConfigurationSaved;
        public event Action<ServerProfile> OnConfigurationDeleted;

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

            ScanForJavaInstallations();
            if (!string.IsNullOrEmpty(JavaPath) && !DetectedJavaPaths.Contains(JavaPath))
            {
                DetectedJavaPaths.Insert(0, JavaPath);
            }

            ServerProperties = new ObservableCollection<PropertyItem>();
            SaveCommand = new RelayCommand(SaveConfiguration);
            DeleteCommand = new RelayCommand(DeleteServer);
            SelectImageCommand = new RelayCommand(SelectImage);
            SelectJdkCommand = new RelayCommand(SelectJdk);
            SelectJarCommand = new RelayCommand(SelectJar);
            ApplyAikarFlagsCommand = new RelayCommand(ApplyAikarFlags);
            TestWebhookCommand = new RelayCommand(TestWebhook);

            LoadImageFromBase64();

            LoadServerProperties();
        }

        private async void TestWebhook(object obj)
        {
            if (string.IsNullOrWhiteSpace(DiscordWebhookUrl))
            {
                CustomMessageBox.Show("Veuillez d'abord entrer une URL Webhook valide.", "URL Manquante", MessageBoxType.Error);
                return;
            }

            await DiscordService.SendNotification(DiscordWebhookUrl, "Test de Notification", "Si vous voyez ceci, la configuration Discord fonctionne !", DiscordService.ColorBlue);

            CustomMessageBox.Show("Une notification de test a été envoyée sur Discord.", "Test Envoyé", MessageBoxType.Info);
        }

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
                "Les flags d'optimisation Aikar (8GB) ont été appliqués !\n\n" +
                "Note : Si votre PC a moins de 16Go de RAM, baissez le -Xms et -Xmx manuellement.",
                "Optimisation Appliquée",
                MessageBoxType.Info
            );
        }

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
                    DetectedJavaPaths.Add(path);
                }

                if (!string.IsNullOrEmpty(JavaPath) && !DetectedJavaPaths.Contains(JavaPath))
                {
                    DetectedJavaPaths.Insert(0, JavaPath);
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
                catch (Exception ex)
                {
                }
            }
        }

        private void SelectJdk(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables Java (java.exe)|java.exe|Tous les fichiers|*.*",
                Title = "Sélectionner l'exécutable Java (bin/java.exe)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;

                if (!DetectedJavaPaths.Contains(selectedPath))
                {
                    DetectedJavaPaths.Insert(0, selectedPath);
                }

                JavaPath = selectedPath;
            }
        }

        private void SelectJar(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Fichiers Executable Java (*.jar)|*.jar",
                Title = "Sélectionner le fichier de lancement du serveur",
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

        private void SelectImage(object obj)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Choisir une miniature pour le serveur"
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
                    CustomMessageBox.Show("Erreur lors du chargement de l'image : " + ex.Message);
                }
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

        private void DeleteServer(object obj)
        {
            bool? result = CustomMessageBox.Show(
                $"Êtes-vous sûr de vouloir supprimer le profil \"{DisplayName}\" ?\n\nCela supprimera la configuration du Manager, mais ne touchera PAS aux fichiers du serveur.",
                "Confirmation de suppression",
                MessageBoxType.Confirmation);

            if (result == true)
            {
                OnConfigurationDeleted?.Invoke(_serverProfile);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}