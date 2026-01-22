using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Minecraft_Server_Manager.Models
{
    public class ServerProfile : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }
        public string FolderPath { get; set; }
        public string JdkPath { get; set; }
        public string JvmArguments { get; set; }
        public string JarName { get; set; } = "server.jar";
        private string _iconBase64;
        public string IconBase64
        {
            get => _iconBase64;
            set
            {
                if (_iconBase64 != value)
                {
                    _iconBase64 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ServerIcon));
                }
            }
        }
        public bool IsRunning { get; set; }

        private bool _autoRestartEnabled;
        public bool AutoRestartEnabled
        {
            get => _autoRestartEnabled;
            set { _autoRestartEnabled = value; OnPropertyChanged(); }
        }

        private string _autoRestartTime = "04:00";
        public string AutoRestartTime
        {
            get => _autoRestartTime;
            set { _autoRestartTime = value; OnPropertyChanged(); }
        }

        private string _discordWebhookUrl;
        public string DiscordWebhookUrl
        {
            get => _discordWebhookUrl;
            set { _discordWebhookUrl = value; OnPropertyChanged(); }
        }

        private int _playerCount = 0;
        [JsonIgnore]
        public int PlayerCount
        {
            get { return _playerCount; }
            set
            {
                if (_playerCount != value)
                {
                    _playerCount = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonIgnore]
        public DateTime LastAutoRestart { get; set; } = DateTime.MinValue;

        [JsonIgnore]
        public StringBuilder CachedLogs { get; } = new StringBuilder();

        [JsonIgnore]
        public bool IsBusy { get; set; }

        [JsonIgnore]
        public Process ServerProcess { get; set; }

        [JsonIgnore]
        public ImageSource ServerIcon
        {
            get
            {
                if (string.IsNullOrEmpty(IconBase64)) return null;

                try
                {
                    byte[] binaryData = Convert.FromBase64String(IconBase64);
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.StreamSource = new MemoryStream(binaryData);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
                catch
                {
                    return null;
                }
            }
        }

        public event Action<string> LogReceived;

        public void AddLog(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            CachedLogs.AppendLine(data);
            LogReceived?.Invoke(data);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}