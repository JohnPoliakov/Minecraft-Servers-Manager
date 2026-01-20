using Minecraft_Server_Manager.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Minecraft_Server_Manager.ViewModels
{
    public class ServerMonitorViewModel : INotifyPropertyChanged
    {
        private ServerProfile _serverProfile;

        public ServerProfile ServerProfile
        {
            get => _serverProfile;
            set { _serverProfile = value; OnPropertyChanged(); }
        }

        public string DisplayName => _serverProfile.DisplayName;

        private string _serverLogs = "";
        public string ServerLogs
        {
            get => _serverLogs;
            set { _serverLogs = value; OnPropertyChanged(); }
        }

        private string _commandInput;
        public string CommandInput
        {
            get => _commandInput;
            set { _commandInput = value; OnPropertyChanged(); }
        }

        private bool _isSwitchEnabled = true;
        public bool IsSwitchEnabled
        {
            get => _isSwitchEnabled;
            set { _isSwitchEnabled = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _serverProfile.IsRunning;
            set
            {
                if (_serverProfile.IsRunning != value)
                {
                    _serverProfile.IsRunning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ServerStatusText));
                    OnPropertyChanged(nameof(StatusColor));

                    // On lance l'action seulement si le switch est activable (sécurité)
                    if (IsSwitchEnabled)
                    {
                        if (value) StartServer();
                        else StopServer();
                    }
                }
            }
        }

        public string ServerStatusText
        {
            get
            {
                if (_serverProfile.IsBusy) return "EN COURS..."; // État intermédiaire
                return IsRunning ? "EN LIGNE" : "HORS LIGNE";
            }
        }

        public Brush StatusColor => IsRunning
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7f8c8d"));

        public ICommand ToggleServerCommand { get; set; }
        public ICommand SendCommand { get; set; }
        public ICommand SettingsCommand { get; set; }

        public ServerMonitorViewModel(ServerProfile server)
        {
            _serverProfile = server;

            ServerLogs = _serverProfile.CachedLogs.ToString();

            _serverProfile.LogReceived += OnNewLogReceived;

            if (_serverProfile.IsBusy)
            {
                IsSwitchEnabled = false;
            }

            if (_serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited)
            {
                _serverProfile.IsRunning = true;
            }

            ToggleServerCommand = new RelayCommand(o => {  });
            SendCommand = new RelayCommand(ExecuteConsoleCommand);
        }

        private void StartServer()
        {
            if (_serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited) return;

            SetBusyState(true);

            _serverProfile.AddLog(">>> Démarrage du serveur...\n");

            try
            {
                string javaExec = string.IsNullOrWhiteSpace(_serverProfile.JdkPath) ? "java" : _serverProfile.JdkPath;
                string args = $"{_serverProfile.JvmArguments} -jar \"{_serverProfile.JarName}\" nogui";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = javaExec,
                    Arguments = args,
                    WorkingDirectory = _serverProfile.FolderPath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                _serverProfile.ServerProcess = new Process { StartInfo = psi };

                _serverProfile.ServerProcess.OutputDataReceived += (s, e) => _serverProfile.AddLog(e.Data);
                _serverProfile.ServerProcess.ErrorDataReceived += (s, e) => _serverProfile.AddLog(e.Data);

                _serverProfile.ServerProcess.EnableRaisingEvents = true;
                _serverProfile.ServerProcess.Exited += OnServerProcessExited;

                _serverProfile.ServerProcess.Start();

                _serverProfile.ServerProcess.BeginOutputReadLine();
                _serverProfile.ServerProcess.BeginErrorReadLine();

                SetBusyState(false);
            }
            catch (Exception ex)
            {
                _serverProfile.AddLog($"[ERREUR] {ex.Message}");
                IsRunning = false;
                SetBusyState(false);
            }
        }

        private void StopServer()
        {
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;

            _serverProfile.AddLog(">>> Arrêt du serveur demandé...");

            SetBusyState(true);

            ExecuteConsoleCommand("stop");

        }

        private void OnServerProcessExited(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _serverProfile.AddLog(">>> Le serveur s'est arrêté.");
                IsRunning = false;

                SetBusyState(false);
            });
        }

        private void OnNewLogReceived(string text)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServerLogs += text + "\n";
            });
        }

        private void ExecuteConsoleCommand(object commandObj)
        {
            string cmd = commandObj as string ?? CommandInput;
            if (string.IsNullOrWhiteSpace(cmd)) return;
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;

            _serverProfile.ServerProcess.StandardInput.WriteLine(cmd);
            CommandInput = "";
        }

        private void SetBusyState(bool busy)
        {
            _serverProfile.IsBusy = busy;
            IsSwitchEnabled = !busy;
            OnPropertyChanged(nameof(ServerStatusText));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}