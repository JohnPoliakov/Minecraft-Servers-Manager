using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;

namespace Minecraft_Server_Manager.ViewModels;

public sealed class ServerMonitorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ServerSession _session;
    private readonly DispatcherTimer _timer;
    private readonly List<string> _history;
    private int _historyOffset;
    private string _historyDraft = "";
    private string _commandInput = "";
    private ServerSnapshot _snapshot;
    private string? _heapArguments, _heapLaunchMode;
    private double _maxRamMb, _lastMaxRamMb;
    private string? _lastStatusText, _lastRamDetailText;
    private bool _disposed;
    public ServerProfile ServerProfile => _session.Profile;
    public string DisplayName => ServerProfile.DisplayName;
    public ImageSource? ServerIconSource => ServerProfile.ServerIcon;
    public bool IsRunning => _snapshot.IsActive || _snapshot.State == ServerState.RestartPending;
    public bool IsSwitchEnabled => !_snapshot.IsBackingUp && _snapshot.State != ServerState.Stopping;
    public System.Windows.Media.Brush StatusColor => _snapshot.State == ServerState.Running ? Brushes.LimeGreen : _snapshot.IsActive ? Brushes.Orange : Brushes.Gray;
    public string ServerStatusText => ResourceHelper.GetString("Loc_State" + _snapshot.State);
    public string UptimeText => $"{(int)_snapshot.Uptime.TotalHours:00}:{_snapshot.Uptime.Minutes:00}:{_snapshot.Uptime.Seconds:00}";
    public double CpuPercent => _snapshot.Metrics.CpuPercent ?? 0;
    public double RamPercent => MaxRamMb > 0 ? Math.Clamp((_snapshot.Metrics.ResidentMegabytes ?? 0) / MaxRamMb * 100, 0, 100) : 0;
    public bool HasHeapLimit => MaxRamMb > 0;
    public string CpuUsageText => _snapshot.Metrics.CpuPercent is { } cpu ? $"{cpu:F1} %" : "—";
    public string RamUsageText => _snapshot.Metrics.ResidentMegabytes is { } memory ? $"{memory:F0} MiB" : "—";
    public string RamDetailText => $"{ResourceHelper.GetString("Loc_HeapLimit")} : {(MaxRamMb > 0 ? $"{MaxRamMb:F0} MiB" : "—")}";
    public double MaxRamMb
    {
        get
        {
            string arguments = ServerProfile.JvmArguments ?? "";
            if (_heapArguments == arguments && _heapLaunchMode == ServerProfile.LaunchMode) return _maxRamMb;
            _heapArguments = arguments; _heapLaunchMode = ServerProfile.LaunchMode; _maxRamMb = 0;
            if (_heapLaunchMode == "Batch") return _maxRamMb;
            var matches = Regex.Matches(arguments, @"-Xmx(\d+)([kmg]?)\b", RegexOptions.IgnoreCase);
            if (matches.Count > 0 && double.TryParse(matches[^1].Groups[1].Value, out var amount))
                _maxRamMb = amount * (matches[^1].Groups[2].Value.ToLowerInvariant() switch { "g" => 1024, "m" => 1, "k" => 1d / 1024, _ => 1d / 1048576 });
            return _maxRamMb;
        }
    }
    public ObservableCollection<string> ConnectedPlayers { get; } = [];
    public int PlayerCount => ConnectedPlayers.Count;
    public string ServerLogs => string.Join(Environment.NewLine, _session.Logs.Snapshot());
    public string CommandInput
    {
        get => _commandInput;
        set
        {
            if (_commandInput == value) return;
            bool wasEmpty = string.IsNullOrWhiteSpace(_commandInput);
            _commandInput = value; Changed();
            if (wasEmpty != string.IsNullOrWhiteSpace(value)) CommandManager.InvalidateRequerySuggested();
        }
    }
    public event Action<string>? LogEntryReceived;
    public event Action? ClearLogsRequested;
    public ICommand ToggleServerCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand NavigateHistoryCommand { get; }
    public ICommand BackupCommand { get; }

    public ServerMonitorViewModel(ServerSession session)
    {
        _session = session; _snapshot = session.Snapshot;
        _history = session.CommandHistory;
        _session.LogReceived += ForwardLog;
        _session.LogsCleared += ClearLogs;
        ServerProfile.PropertyChanged += ProfileChanged;
        ToggleServerCommand = new AsyncRelayCommand(async _ =>
        {
            try
            {
                if (_session.Snapshot.IsActive || _session.Snapshot.State == ServerState.RestartPending) await _session.StopAsync();
                else await _session.StartAsync();
            }
            finally
            {
                Refresh();
                // Restore the toggle's local value even if startup fails or is declined between ticks.
                Changed(nameof(IsRunning));
            }
        }, ShowError, _ => IsSwitchEnabled);
        SendCommand = new AsyncRelayCommand(async _ =>
        {
            string command = CommandInput;
            await _session.SendCommandAsync(command);
            if (!string.IsNullOrWhiteSpace(command))
            {
                _history.Add(command); if (_history.Count > 100) _history.RemoveAt(0);
                _historyOffset = 0; _historyDraft = ""; CommandInput = "";
            }
        }, ShowError, _ => _session.Snapshot.IsActive && !string.IsNullOrWhiteSpace(CommandInput));
        NavigateHistoryCommand = new RelayCommand(direction =>
        {
            if (_historyOffset == 0) _historyDraft = CommandInput;
            _historyOffset = Math.Clamp(_historyOffset + (direction as string == "Up" ? 1 : -1), 0, _history.Count);
            CommandInput = _historyOffset > 0 ? _history[^_historyOffset] : _historyDraft;
        });
        BackupCommand = new AsyncRelayCommand(async _ =>
        {
            if (_session.Snapshot.IsActive && CustomMessageBox.Show(ResourceHelper.GetString("Loc_BackupPause"),
                ResourceHelper.GetString("Loc_BackupTitle"), MessageBoxType.Confirmation) != true) return;
            await _session.BackupAsync();
        }, ShowError, _ => !_session.Snapshot.IsBackingUp && _session.Snapshot.State is ServerState.Stopped or ServerState.Running or ServerState.Faulted);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += Tick; _timer.Start(); Refresh();
    }

    private void Tick(object? sender, EventArgs args) => Refresh();
    private void Refresh()
    {
        var previous = _snapshot;
        _snapshot = _session.Snapshot;
        bool heapChanged = _lastMaxRamMb != MaxRamMb;
        if (heapChanged)
        {
            if ((_lastMaxRamMb > 0) != HasHeapLimit) Changed(nameof(HasHeapLimit));
            _lastMaxRamMb = MaxRamMb; Changed(nameof(MaxRamMb));
        }
        if (!ReferenceEquals(previous, _snapshot))
        {
            if ((previous.IsActive || previous.State == ServerState.RestartPending) != IsRunning) Changed(nameof(IsRunning));
            if ((!previous.IsBackingUp && previous.State != ServerState.Stopping) != IsSwitchEnabled) Changed(nameof(IsSwitchEnabled));
            if (previous.State != _snapshot.State) Changed(nameof(StatusColor));
            if (previous.State != _snapshot.State || previous.IsBackingUp != _snapshot.IsBackingUp)
                CommandManager.InvalidateRequerySuggested();
            if ((long)previous.Uptime.TotalSeconds != (long)_snapshot.Uptime.TotalSeconds) Changed(nameof(UptimeText));
            if (previous.Metrics.CpuPercent != _snapshot.Metrics.CpuPercent)
            {
                Changed(nameof(CpuPercent)); Changed(nameof(CpuUsageText));
            }
            if (previous.Metrics.ResidentMegabytes != _snapshot.Metrics.ResidentMegabytes) Changed(nameof(RamUsageText));
        }
        if (heapChanged || previous.Metrics.ResidentMegabytes != _snapshot.Metrics.ResidentMegabytes) Changed(nameof(RamPercent));
        if (!ConnectedPlayers.SequenceEqual(_snapshot.Players))
        {
            int previousCount = ConnectedPlayers.Count;
            ConnectedPlayers.Clear(); foreach (var player in _snapshot.Players) ConnectedPlayers.Add(player);
            if (previousCount != PlayerCount) Changed(nameof(PlayerCount));
        }
        // Language can change while a stopped session keeps the same snapshot.
        string statusText = ServerStatusText, ramDetailText = RamDetailText;
        if (_lastStatusText != statusText) { _lastStatusText = statusText; Changed(nameof(ServerStatusText)); }
        if (_lastRamDetailText != ramDetailText) { _lastRamDetailText = ramDetailText; Changed(nameof(RamDetailText)); }
    }
    private void ProfileChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ServerProfile.DisplayName)) Changed(nameof(DisplayName));
        if (args.PropertyName == nameof(ServerProfile.ServerIcon)) Changed(nameof(ServerIconSource));
    }
    private void ForwardLog(string text) => LogEntryReceived?.Invoke(text);
    private void ClearLogs() => System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ClearLogsRequested?.Invoke());
    private static void ShowError(Exception ex) => CustomMessageBox.Show(ex.Message, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
    public void Cleanup() => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _timer.Stop(); _timer.Tick -= Tick;
        _session.LogReceived -= ForwardLog; _session.LogsCleared -= ClearLogs;
        ServerProfile.PropertyChanged -= ProfileChanged;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
