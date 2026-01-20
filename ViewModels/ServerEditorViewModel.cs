using Microsoft.Win32;
using Minecraft_Server_Manager.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

        private ImageSource _serverIconSource;
        public ImageSource ServerIconSource
        {
            get => _serverIconSource;
            set { _serverIconSource = value; OnPropertyChanged(); }
        }

        public string FolderPath => _serverProfile.FolderPath; // Lecture seule pour la vue

        public ICommand SaveCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand SelectImageCommand { get; set; }

        public ServerEditorViewModel(string folderPath, ServerProfile? serverProfile = null)
        {

            _serverProfile = serverProfile ?? new ServerProfile
            {
                Id = System.Guid.NewGuid().ToString(),
                JdkPath = "java",
                JvmArguments = "-Xmx1024M -Xms1024M",
                FolderPath = folderPath,
                DisplayName = new DirectoryInfo(folderPath).Name
            };

            ServerProperties = new ObservableCollection<PropertyItem>();
            SaveCommand = new RelayCommand(SaveConfiguration);
            SelectImageCommand = new RelayCommand(SelectImage);

            LoadImageFromBase64();

            LoadServerProperties();
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
                catch(Exception ex)
                {
                    Debug.WriteLine("AAAAAAA "+  ex.Message);
                }
            }
        }
        private void SelectImage(object obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
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
                    System.Windows.MessageBox.Show("Erreur lors du chargement de l'image : " + ex.Message);
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
            string appDataPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "configs");
            Directory.CreateDirectory(appDataPath);

            string jsonFile = Path.Combine(appDataPath, $"{_serverProfile.Id}.json");
            string jsonString = JsonSerializer.Serialize(_serverProfile, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(jsonFile, jsonString);

            SaveServerPropertiesFile();
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}