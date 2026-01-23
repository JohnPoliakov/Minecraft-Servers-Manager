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
        #region Fields
        private string _displayName;
        private string _iconBase64;
        private bool _autoRestartEnabled;
        private string _autoRestartTime = "04:00";
        private string _discordWebhookUrl;
        private int _playerCount = 0;
        private bool _isRunning;
        #endregion

        #region Events
        public event Action<string> LogReceived;
        #endregion

        #region Persistent Properties (Saved to JSON)

        public string Id { get; set; } = Guid.NewGuid().ToString();

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

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoRestartEnabled
        {
            get => _autoRestartEnabled;
            set { _autoRestartEnabled = value; OnPropertyChanged(); }
        }

        public string AutoRestartTime
        {
            get => _autoRestartTime;
            set { _autoRestartTime = value; OnPropertyChanged(); }
        }

        public string DiscordWebhookUrl
        {
            get => _discordWebhookUrl;
            set { _discordWebhookUrl = value; OnPropertyChanged(); }
        }
        #endregion

        #region Runtime Properties (Ignored in JSON)

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

        /// <summary>
        /// Génère l'image WPF à partir de la chaîne Base64 stockée.
        /// </summary>
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
        #endregion

        #region Methods
        /// <summary>
        /// Ajoute un log au cache et déclenche l'événement pour l'UI.
        /// </summary>
        public void AddLog(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            lock (CachedLogs)
            {
                CachedLogs.AppendLine(data);
            }

            LogReceived?.Invoke(data);
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}