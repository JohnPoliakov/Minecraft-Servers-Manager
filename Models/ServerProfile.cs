using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;
using Minecraft_Server_Manager.Utils;

namespace Minecraft_Server_Manager.Models;

/// <summary>Editable definition plus the small WPF projection used by server cards.</summary>
public class ServerProfile : INotifyPropertyChanged
{
    private string _displayName = "";
    private string? _iconBase64;
    private ImageSource? _icon;
    private bool _running, _busy, _autoRestart;
    private int _players;
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get => _displayName; set { _displayName = value; Changed(); } }
    public string FolderPath { get; set; } = "";
    public string JdkPath { get; set; } = "java";
    public string JvmArguments { get; set; } = "-Xmx4G -Xms1G";
    public string JarName { get; set; } = "server.jar";
    public string LaunchMode { get; set; } = "Java";
    public string BatchFilename { get; set; } = "start.bat";
    public string? IconBase64
    {
        get => _iconBase64;
        set { _iconBase64 = value; _icon = null; Changed(); Changed(nameof(ServerIcon)); }
    }
    public bool AutoRestartEnabled { get => _autoRestart; set { _autoRestart = value; Changed(); } }
    public string AutoRestartTime { get; set; } = "04:00";
    public string DiscordWebhookUrl { get; set; } = "";
    public bool ClearLogsOnStart { get; set; }
    [JsonIgnore] public bool IsRunning { get => _running; set { if (_running != value) { _running = value; Changed(); } } }
    [JsonIgnore] public bool IsBusy { get => _busy; set { if (_busy != value) { _busy = value; Changed(); } } }
    [JsonIgnore] public int PlayerCount { get => _players; set { if (_players != value) { _players = value; Changed(); } } }
    [JsonIgnore] public ImageSource? ServerIcon => _icon ??= string.IsNullOrEmpty(IconBase64) ? null : ResourceHelper.CreateImageFromBase64(IconBase64);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
