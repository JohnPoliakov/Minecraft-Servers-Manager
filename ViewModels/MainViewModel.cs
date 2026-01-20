using Minecraft_Server_Manager.Models;
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

        public ICommand EditServerCommand { get; private set; }
        public ICommand MonitorServerCommand { get; private set; }

        public MainViewModel()
        {
            Servers = new ObservableCollection<ServerProfile>();
            EditServerCommand = new RelayCommand(param => EditServer((ServerProfile)param));
            MonitorServerCommand = new RelayCommand(param => MonitorServer((ServerProfile)param));

            LoadServers();

            MonitorServer(Servers[0]);
        }

        public void EditServer(ServerProfile profile)
        {
            if (profile == null) return;

            CurrentView = new ServerEditorViewModel(profile.FolderPath, profile);
        }

        public void MonitorServer(ServerProfile profile)
        {
            if (profile == null) return;

            CurrentView = new ServerMonitorViewModel(profile);
        }

        private void LoadServers()
        {
            string appDataPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "configs");

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}