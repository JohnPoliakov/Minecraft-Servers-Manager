using Microsoft.Win32;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.UserControls;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Minecraft_Server_Manager.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private object? _currentView;
    private readonly ServerCatalog _catalog = new();
    private readonly ServerRegistry _registry;
    public ObservableCollection<ServerProfile> Servers { get; } = [];
    public bool ShowSidebar => true;
    public object? CurrentView
    {
        get => _currentView;
        set
        {
            if (ReferenceEquals(_currentView, value)) return;
            if (_currentView is IDisposable disposable) disposable.Dispose();
            _currentView = value; Changed();
        }
    }
    public ICommand EditServerCommand { get; }
    public ICommand MonitorServerCommand { get; }
    public ICommand AddServerCommand { get; }
    public ICommand ShowHomeCommand { get; }
    public ICommand ShowBrowserCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public MainViewModel()
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        _registry = new ServerRegistry(async () => await dispatcher.InvokeAsync(() =>
            CustomMessageBox.Show(ResourceHelper.GetString("Loc_EulaMsg"), ResourceHelper.GetString("Loc_EulaTitle"), MessageBoxType.Confirmation) == true),
            action => { if (!dispatcher.HasShutdownStarted) dispatcher.InvokeAsync(action); });
        ThemeResources.ApplySaved();
        EditServerCommand = new RelayCommand(p => { if (p is ServerProfile profile) EditServer(profile); });
        MonitorServerCommand = new RelayCommand(p => { if (p is ServerProfile profile) MonitorServer(profile); });
        AddServerCommand = new RelayCommand(_ => AddServer());
        ShowHomeCommand = new RelayCommand(_ => ShowHome());
        ShowBrowserCommand = new RelayCommand(_ => CurrentView = new CurseForgeView());
        ShowSettingsCommand = new RelayCommand(_ => CurrentView = new SettingsViewModel());
        var errors = new List<string>();
        foreach (var profile in _catalog.Load(errors.Add)) Servers.Add(profile);
        if (errors.Count > 0) CustomMessageBox.Show(string.Join(Environment.NewLine, errors), ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
        ShowHome();
    }
    private void ShowHome() => CurrentView = new HomeViewModel(Servers, MonitorServer);
    public void MonitorServer(ServerProfile profile) => CurrentView = new ServerMonitorViewModel(_registry.Get(profile));
    public void EditServer(ServerProfile profile)
    {
        var editor = new ServerEditorViewModel(profile.FolderPath, profile, _registry.Get(profile), _catalog);
        editor.OnConfigurationSaved += MonitorServer;
        editor.OnConfigurationDeleted += DeleteServerAsync;
        CurrentView = editor;
    }
    public void RegisterServer(ServerProfile profile)
    {
        if (Servers.Any(existing => ServerCatalog.SameFolder(existing.FolderPath, profile.FolderPath)))
            throw new InvalidOperationException("Ce dossier est déjà enregistré.");
        _catalog.Save(profile);
        Servers.Add(profile);
    }
    private void AddServer()
    {
        var dialog = new OpenFolderDialog { Title = ResourceHelper.GetString("Loc_SelectRootFolder") };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var profile = new ServerProfile
            {
                DisplayName = new DirectoryInfo(dialog.FolderName).Name, FolderPath = dialog.FolderName,
                JvmArguments = $"-Xmx{ConfigManager.Settings.DefaultRam}G -Xms1G"
            };
            ServerInstaller.DetectLaunch(profile);
            RegisterServer(profile);
            EditServer(profile);
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task DeleteServerAsync(ServerProfile profile)
    {
        try
        {
            await _registry.RemoveAsync(profile);
            _catalog.Delete(profile);
            Servers.Remove(profile);
            ShowHome();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    public async Task StopAllServersAsync()
    {
        var browser = CurrentView as CurseForgeView;
        CurrentView = null;
        await Task.WhenAll(_registry.ShutdownAsync(), browser?.DisposeAsync().AsTask() ?? Task.CompletedTask);
    }
    private static void ShowError(Exception ex) => CustomMessageBox.Show(ex.Message, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
