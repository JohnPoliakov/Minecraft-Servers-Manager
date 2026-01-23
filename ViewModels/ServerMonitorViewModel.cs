using LiveCharts;
using LiveCharts.Wpf;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Minecraft_Server_Manager.ViewModels
{
    public class ServerMonitorViewModel : INotifyPropertyChanged
    {

        #region Constants & Static Fields
        private readonly System.Threading.SemaphoreSlim _commandLock = new System.Threading.SemaphoreSlim(1, 1);

        private static readonly Regex _loginRegex = new Regex(
            @":\s+([a-zA-Z0-9_]+)\[.*\]\s+logged in with",
            RegexOptions.Compiled);

        private static readonly Regex _logoutRegex = new Regex(
            @":\s+([a-zA-Z0-9_]+).*\s+left the game",
            RegexOptions.Compiled);
        #endregion

        #region Fields
        private ServerProfile _serverProfile;
        private DispatcherTimer _monitorTimer;

        // Monitoring Vars
        private TimeSpan _lastTotalProcessorTime;
        private DateTime _lastTimerTick;
        private double _maxRamMb = 1024;

        // State Vars
        private bool _isRestarting = false;
        private bool _isIntentionalStop = false;

        // Console History
        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = 0;
        #endregion

        #region Events
        public event Action<string> LogEntryReceived;
        #endregion

        #region Properties - UI & Data Binding

        // --- Core Server Data ---
        public ServerProfile ServerProfile
        {
            get => _serverProfile;
            set { _serverProfile = value; OnPropertyChanged(); }
        }

        public string DisplayName => _serverProfile.DisplayName;

        private ImageSource _serverIconSource;
        public ImageSource ServerIconSource
        {
            get => _serverIconSource;
            set { _serverIconSource = value; OnPropertyChanged(); }
        }

        // --- Status & State ---
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
                    OnPropertyChanged(nameof(StatusColor));

                    // Déclenche le démarrage/arrêt si le switch est activé manuellement
                    if (IsSwitchEnabled)
                    {
                        if (value) StartServer();
                        else StopServer();
                    }
                }
            }
        }

        public System.Windows.Media.Brush StatusColor => IsRunning
             ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60")) // Vert
             : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7f8c8d")); // Gris

        private string _uptimeText = "00:00:00";
        public string UptimeText
        {
            get => _uptimeText;
            set { _uptimeText = value; OnPropertyChanged(); }
        }

        public SeriesCollection CpuSeries { get; set; }
        public SeriesCollection RamSeries { get; set; }

        private string _cpuUsageText = "0 %";
        public string CpuUsageText
        {
            get => _cpuUsageText;
            set { _cpuUsageText = value; OnPropertyChanged(); }
        }

        private string _ramUsageText = "0 / 0 MB";
        public string RamUsageText
        {
            get => _ramUsageText;
            set { _ramUsageText = value; OnPropertyChanged(); }
        }

        private string _ramDetailText = "0 / 0 MB";
        public string RamDetailText
        {
            get => _ramDetailText;
            set { _ramDetailText = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ConnectedPlayers { get; set; } = new ObservableCollection<string>();
        public int PlayerCount => ConnectedPlayers.Count;

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

        public bool AutoRestartEnabled
        {
            get => _serverProfile.AutoRestartEnabled;
            set { _serverProfile.AutoRestartEnabled = value; OnPropertyChanged(); }
        }

        public string AutoRestartTime
        {
            get => _serverProfile.AutoRestartTime;
            set { _serverProfile.AutoRestartTime = value; OnPropertyChanged(); }
        }
        #endregion

        #region Commands
        public ICommand ToggleServerCommand { get; set; }
        public ICommand SendCommand { get; set; }
        public ICommand NavigateHistoryCommand { get; set; }
        public ICommand BackupCommand { get; set; }
        #endregion

        #region Constructor
        public ServerMonitorViewModel(ServerProfile server)
        {
            _serverProfile = server;

            LoadImageFromBase64();
            ParseMaxRam();
            ServerLogs = _serverProfile.CachedLogs.ToString();

            _serverProfile.LogReceived += OnNewLogReceived;

            InitializeCharts();
            _monitorTimer = new DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromSeconds(1);
            _monitorTimer.Tick += UpdateStats;

            if (_serverProfile.IsBusy)
            {
                IsSwitchEnabled = false;
            }

            if (_serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited)
            {
                _serverProfile.IsRunning = true;
                _monitorTimer.Start();
            }

            ToggleServerCommand = new RelayCommand(o => { });
            SendCommand = new RelayCommand(ExecuteConsoleCommand);
            NavigateHistoryCommand = new RelayCommand(NavigateHistory);
            BackupCommand = new RelayCommand(o => PerformBackup());
        }
        #endregion

        #region Server Lifecycle Management (Start/Stop/Restart)
        private async void StartServer()
        {
            if (_serverProfile.ServerProcess != null && !_serverProfile.ServerProcess.HasExited) return;

            _isIntentionalStop = false;
            SetBusyState(true);
            _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogStart") + "\n");

            await Task.Run(async () =>
            {
                try
                {
                    string eulaPath = Path.Combine(_serverProfile.FolderPath, "eula.txt");
                    if (File.Exists(eulaPath))
                    {
                        string[] lines = File.ReadAllLines(eulaPath);
                        bool modified = false;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Trim() == "eula=false")
                            {
                                lines[i] = "eula=true";
                                modified = true;
                                _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogEula"));
                            }
                        }
                        if (modified) File.WriteAllLines(eulaPath, lines);
                    }

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

                    try { _serverProfile.ServerProcess.PriorityClass = ProcessPriorityClass.High; } catch { }

                    _serverProfile.IsRunning = true;
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(StatusColor));

                    _serverProfile.ServerProcess.BeginOutputReadLine();
                    _serverProfile.ServerProcess.BeginErrorReadLine();

                    ConnectedPlayers.Clear();
                    OnPropertyChanged(nameof(PlayerCount));

                    _lastTimerTick = DateTime.MinValue;
                    _monitorTimer.Start();
                    _serverProfile.PlayerCount = 0;

                    SetBusyState(false);

                    await Task.Delay(5000);

                    _ = DiscordService.SendNotification(_serverProfile.DiscordWebhookUrl,
                        ResourceHelper.GetString("Loc_ServerStartedTitle"),
                        string.Format(ResourceHelper.GetString("Loc_ServerStartedMsg"), DisplayName),
                        DiscordService.ColorGreen);
                }
                catch (Exception ex)
                {
                    _serverProfile.AddLog($"[ERREUR] {ex.Message}");
                    IsRunning = false;
                    SetBusyState(false);
                }
            });
        }

        private void StopServer()
        {
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;

            _isIntentionalStop = true;
            _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogStopRequested"));
            SetBusyState(true);

            KickAll(ResourceHelper.GetString("Loc_KickReason"));
            ExecuteConsoleCommand("stop");
        }

        private void PerformRestart()
        {
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;

            _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogAutoRestartScheduled"));

            _isIntentionalStop = true;
            _isRestarting = true;

            ExecuteConsoleCommand($"say {ResourceHelper.GetString("Loc_RestartMsg")}");
            ExecuteConsoleCommand("title @a title {\"text\":\"" + ResourceHelper.GetString("Loc_RestartTitle") + "\",\"color\":\"red\"}");

            StopServer();
        }

        private void KickAll(string reason)
        {
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;
            _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogKickAll"));
            ExecuteConsoleCommand("kick @a " + reason);
        }

        private async void OnServerProcessExited(object sender, EventArgs e)
        {

            try
            {

                if (System.Windows.Application.Current == null) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogServerStopped"));
                    IsRunning = false;
                    _monitorTimer.Stop();
                    SetBusyState(false);
                    _serverProfile.PlayerCount = 0;
                });

                if (_isRestarting)
                {
                    _ = DiscordService.SendNotification(_serverProfile.DiscordWebhookUrl,
                        ResourceHelper.GetString("Loc_RestartScheduledTitle"),
                        ResourceHelper.GetString("Loc_RestartScheduledMsg"),
                        DiscordService.ColorOrange);

                    _isRestarting = false;
                    _isIntentionalStop = false;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogRestartingPause"));
                    });

                    await Task.Delay(5000);
                    if (System.Windows.Application.Current != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StartServer());
                    }
                    return;
                }

                if (_isIntentionalStop)
                {
                    _ = DiscordService.SendNotification(_serverProfile.DiscordWebhookUrl,
                           ResourceHelper.GetString("Loc_StopManualTitle"),
                           ResourceHelper.GetString("Loc_StopManualMsg"),
                           DiscordService.ColorRed);
                    return;
                }

                int exitCode = -1;
                try
                {
                    if (_serverProfile.ServerProcess != null)
                    {
                        exitCode = _serverProfile.ServerProcess.ExitCode;
                    }
                    _ = DiscordService.SendNotification(_serverProfile.DiscordWebhookUrl,
                        ResourceHelper.GetString("Loc_CrashTitle"),
                        string.Format(ResourceHelper.GetString("Loc_CrashMsg"), exitCode),
                        DiscordService.ColorRed);
                }
                catch (InvalidOperationException)
                {
                    exitCode = -999;
                }
                catch { }

                if (System.Windows.Application.Current != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _serverProfile.AddLog(string.Format(ResourceHelper.GetString("Loc_LogCrashDetected"), exitCode));
                        _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogRestartIn10s"));
                    });
                }

                await Task.Delay(10000);

                if (System.Windows.Application.Current != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!_isIntentionalStop)
                        {
                            _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogRetryStart"));
                            StartServer();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erreur FATALE dans OnServerProcessExited : {ex.Message}\n{ex.StackTrace}");
            }


        }
        #endregion

        #region Monitoring & Metrics (Update Loop)
        private void InitializeCharts()
        {
            CpuSeries = new SeriesCollection
            {
                new PieSeries { Title = "Used", Values = new ChartValues<double> { 0 }, Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219)), StrokeThickness = 0 },
                new PieSeries { Title = "Rest", Values = new ChartValues<double> { 100 }, Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255)), StrokeThickness = 0 }
            };

            RamSeries = new SeriesCollection
            {
                new PieSeries { Title = "Used", Values = new ChartValues<double> { 0 }, Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113)), StrokeThickness = 0 },
                new PieSeries { Title = "Rest", Values = new ChartValues<double> { 100 }, Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255)), StrokeThickness = 0 }
            };
        }

        private async void UpdateStats(object sender, EventArgs e)
        {
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited)
            {
                _monitorTimer.Stop();
                CpuUsageText = "0 %";
                RamUsageText = "0 MB";
                return;
            }

            if (CpuSeries == null || RamSeries == null) return;

            await Task.Run(() =>
            {
                try
                {
                    _serverProfile.ServerProcess.Refresh();

                    UptimeText = (DateTime.Now - _serverProfile.ServerProcess.StartTime).ToString(@"hh\:mm\:ss");

                    // --- CPU Calcul ---
                    var currentTime = DateTime.Now;
                    var currentTotalProcessorTime = _serverProfile.ServerProcess.TotalProcessorTime;

                    if (_lastTimerTick != DateTime.MinValue)
                    {
                        double cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                        double totalMsPassed = (currentTime - _lastTimerTick).TotalMilliseconds;
                        double cpuUsageTotal = (cpuUsedMs / (totalMsPassed * Environment.ProcessorCount)) * 100;
                        cpuUsageTotal = Math.Max(0, Math.Min(100, cpuUsageTotal));

                        CpuSeries[0].Values[0] = cpuUsageTotal;
                        CpuSeries[1].Values[0] = 100 - cpuUsageTotal;
                        CpuUsageText = $"{cpuUsageTotal:F0} %";
                    }

                    _lastTotalProcessorTime = currentTotalProcessorTime;
                    _lastTimerTick = currentTime;

                    // --- RAM Calcul ---
                    double ramUsedMb = _serverProfile.ServerProcess.WorkingSet64 / 1024.0 / 1024.0;

                    RamSeries[0].Values[0] = ramUsedMb;
                    RamSeries[1].Values[0] = Math.Max(0, _maxRamMb - ramUsedMb);

                    RamUsageText = $"{ramUsedMb:F0} MB";
                    RamDetailText = $"{ramUsedMb:F0} / {_maxRamMb} MB";
                }
                catch { }
            });

            if (_serverProfile.AutoRestartEnabled && IsRunning && !_isRestarting)
            {
                if (TimeSpan.TryParse(_serverProfile.AutoRestartTime, out TimeSpan targetTime))
                {
                    var now = DateTime.Now;
                    if (now.Hour == targetTime.Hours && now.Minute == targetTime.Minutes && _serverProfile.LastAutoRestart.Date != now.Date)
                    {
                        _serverProfile.LastAutoRestart = now;
                        PerformRestart();
                    }
                }
            }
        }

        private void ParseMaxRam()
        {
            if (string.IsNullOrEmpty(_serverProfile.JvmArguments)) return;

            var match = Regex.Match(_serverProfile.JvmArguments, @"-Xmx(\d+)([MG])");
            if (match.Success)
            {
                double value = double.Parse(match.Groups[1].Value);
                string unit = match.Groups[2].Value;
                if (unit == "G") _maxRamMb = value * 1024;
                else _maxRamMb = value;
            }
        }
        #endregion

        #region Console & Log Handling
        private void OnNewLogReceived(string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntryReceived?.Invoke(text);

                if (string.IsNullOrEmpty(text)) return;

                var joinMatch = _loginRegex.Match(text);
                if (joinMatch.Success)
                {
                    string playerName = joinMatch.Groups[1].Value;
                    if (!ConnectedPlayers.Contains(playerName))
                    {
                        ConnectedPlayers.Add(playerName);
                        OnPropertyChanged(nameof(PlayerCount));
                        _serverProfile.PlayerCount = ConnectedPlayers.Count;
                    }
                }

                var leftMatch = _logoutRegex.Match(text);
                if (leftMatch.Success)
                {
                    string playerName = leftMatch.Groups[1].Value;
                    if (ConnectedPlayers.Contains(playerName))
                    {
                        ConnectedPlayers.Remove(playerName);
                        OnPropertyChanged(nameof(PlayerCount));
                        _serverProfile.PlayerCount = ConnectedPlayers.Count;
                    }
                }
            });
        }

        private async void ExecuteConsoleCommand(object commandObj)
        {
            string cmd = commandObj as string ?? CommandInput;

            if (string.IsNullOrWhiteSpace(cmd)) return;
            if (_serverProfile.ServerProcess == null || _serverProfile.ServerProcess.HasExited) return;

            _commandHistory.Add(cmd);
            _historyIndex = _commandHistory.Count;
            CommandInput = "";

            try
            {
                await _commandLock.WaitAsync();

                await _serverProfile.ServerProcess.StandardInput.WriteLineAsync(cmd);
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _commandLock.Release();
            }
        }

        private void NavigateHistory(object direction)
        {
            if (_commandHistory.Count == 0) return;

            string dir = direction as string;
            if (dir == "Up")
            {
                if (_historyIndex > 0) _historyIndex--;
            }
            else if (dir == "Down")
            {
                if (_historyIndex < _commandHistory.Count) _historyIndex++;
            }

            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
                CommandInput = _commandHistory[_historyIndex];
            else
                CommandInput = "";
        }
        #endregion

        #region Backup System
        private async void PerformBackup()
        {
            if (_serverProfile.IsBusy) return;

            SetBusyState(true);
            _serverProfile.AddLog(ResourceHelper.GetString("Loc_BackupStart"));

            string backupDir = Path.Combine(_serverProfile.FolderPath, "Backups");
            Directory.CreateDirectory(backupDir);

            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string zipPath = Path.Combine(backupDir, $"Backup_{timeStamp}.zip");

            await Task.Run(async () =>
            {
                try
                {
                    bool wasRunning = IsRunning;

                    try
                    {
                        var directory = new DirectoryInfo(backupDir);
                        var oldFiles = directory.GetFiles("Backup_*.zip")
                                                .Where(f => f.CreationTime < DateTime.Now.AddDays(-7))
                                                .ToList();

                        if (oldFiles.Any())
                        {
                            _serverProfile.AddLog(string.Format(ResourceHelper.GetString("Loc_LogDeleteOldBackups"), oldFiles.Count));
                            foreach (var file in oldFiles) try { file.Delete(); } catch { }
                        }
                    }
                    catch (Exception ex) { _serverProfile.AddLog(string.Format(ResourceHelper.GetString("Loc_LogBackupCleanupError"), ex.Message)); }

                    if (wasRunning)
                    {
                        ExecuteConsoleCommand("save-off");
                        ExecuteConsoleCommand("save-all");
                        _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogSaveAll"));
                        await Task.Delay(3000);
                    }

                    _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogZipping"));
                    using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(_serverProfile.FolderPath);
                        foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                        {
                            if (file.FullName.Contains("Backups") || file.Name == "session.lock") continue;

                            string relativePath = Path.GetRelativePath(_serverProfile.FolderPath, file.FullName);
                            try { archive.CreateEntryFromFile(file.FullName, relativePath); } catch (IOException) { }
                        }
                    }

                    _serverProfile.AddLog(string.Format(ResourceHelper.GetString("Loc_BackupSuccess"), Path.GetFileName(zipPath)));

                    if (wasRunning)
                    {
                        ExecuteConsoleCommand("save-on");
                        _serverProfile.AddLog(ResourceHelper.GetString("Loc_LogSaveOn"));
                    }
                }
                catch (Exception ex)
                {
                    _serverProfile.AddLog(string.Format(ResourceHelper.GetString("Loc_BackupError"), ex.Message));
                    if (IsRunning) ExecuteConsoleCommand("save-on");
                }
            });

            SetBusyState(false);
        }
        #endregion

        #region Helper Methods
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

        private void SetBusyState(bool busy)
        {
            _serverProfile.IsBusy = busy;
            IsSwitchEnabled = !busy;
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}