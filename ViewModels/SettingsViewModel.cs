using Microsoft.VisualBasic.Devices;
using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Minecraft_Server_Manager.ViewModels
{
    public class LanguageOption
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";

        public string FlagPath { get; set; } = "";

    }

    public class ThemeOption
    {
        public string Name { get; set; } = "";
        public string PrimaryColor { get; set; } = "";
        public string SecondaryColor { get; set; } = "";

        public System.Windows.Media.Brush PreviewBrush => (System.Windows.Media.Brush?)new BrushConverter().ConvertFromString(PrimaryColor) ?? System.Windows.Media.Brushes.Transparent;
    }

    public class SettingsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ThemeOption> AvailableThemes { get; set; }

        private ThemeOption _selectedTheme;
        public ThemeOption SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        ConfigManager.Settings.SelectedTheme = value.Name;
                        ConfigManager.Save();

                        ApplyTheme(value);
                    }
                }
            }
        }

        public ObservableCollection<LanguageOption> AvailableLanguages { get; set; }

        private LanguageOption _selectedLanguage;
        public LanguageOption SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (_selectedLanguage != value && value != null)
                {
                    _selectedLanguage = value;
                    OnPropertyChanged();

                    ConfigManager.Settings.Language = value.Code;
                    ConfigManager.Save();

                    ApplyLanguage(value.Code);

                    if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateTrayTexts();
                    }
                }
            }
        }

        private int _defaultRam;
        public int DefaultRam
        {
            get => _defaultRam;
            set
            {
                if (_defaultRam != value)
                {
                    _defaultRam = value;
                    OnPropertyChanged();
                    ConfigManager.Settings.DefaultRam = value;
                    ConfigManager.Save();
                }
            }
        }

        private string _defaultPath;
        public string DefaultPath
        {
            get => _defaultPath;
            set
            {
                _defaultPath = value;
                OnPropertyChanged();
                ConfigManager.Settings.DefaultServerPath = value;
                ConfigManager.Save();
            }
        }

        public int MaxSystemRam { get; set; }

        public ICommand BrowseFolderCommand { get; }
        public ICommand ClearCacheCommand { get; }

        public SettingsViewModel()
        {
            AvailableThemes = new ObservableCollection<ThemeOption>(ThemeResources.Themes.Select(theme =>
                new ThemeOption { Name = theme.Key, PrimaryColor = theme.Value.Primary, SecondaryColor = theme.Value.Secondary }));

            try
            {
                var computerInfo = new ComputerInfo();
                ulong totalBytes = computerInfo.TotalPhysicalMemory;

                MaxSystemRam = (int)Math.Ceiling(totalBytes / (1024.0 * 1024 * 1024));
            }
            catch
            {
                MaxSystemRam = 16;
            }

            _defaultPath = ConfigManager.Settings.DefaultServerPath;

            var savedTheme = AvailableThemes.FirstOrDefault(t => t.Name == ConfigManager.Settings.SelectedTheme)
                             ?? AvailableThemes.First();

            _selectedTheme = savedTheme;

            if (ConfigManager.Settings.DefaultRam > MaxSystemRam)
            {
                ConfigManager.Settings.DefaultRam = MaxSystemRam;
                ConfigManager.Save();
            }

            _defaultRam = ConfigManager.Settings.DefaultRam;

            BrowseFolderCommand = new RelayCommand(o => SelectFolder());
            ClearCacheCommand = new RelayCommand(o => ClearBrowserCache());

            AvailableLanguages = new ObservableCollection<LanguageOption>
            {
                new LanguageOption { Name = "Français", Code = "fr-FR", FlagPath = "/Resources/Flags/flag-fr.png" },
                new LanguageOption { Name = "English", Code = "en-US", FlagPath = "/Resources/Flags/flag-uk.png" },
                new LanguageOption { Name = "Español", Code = "es-ES", FlagPath = "/Resources/Flags/flag-es.png" },
                new LanguageOption { Name = "Deutsch", Code = "de-DE", FlagPath = "/Resources/Flags/flag-de.png" }
            };

            _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == ConfigManager.Settings.Language)
                               ?? AvailableLanguages.First();
        }

        private void ApplyLanguage(string cultureCode) => ThemeResources.ApplyLanguage(cultureCode);

        private void ClearBrowserCache()
        {
            string cachePath = AppPaths.BrowserData;

            if (!Directory.Exists(cachePath))
            {
                CustomMessageBox.Show(ResourceHelper.GetString("Loc_CacheEmpty"), "Information", MessageBoxType.Info);
                return;
            }

            var result = CustomMessageBox.Show(
                ResourceHelper.GetString("Loc_ConfirmCacheMsg"),
                ResourceHelper.GetString("Loc_ConfirmCacheTitle"),
                MessageBoxType.Confirmation);

            if (result == true)
            {
                try
                {
                    Directory.Delete(cachePath, true);
                    CustomMessageBox.Show(ResourceHelper.GetString("Loc_CacheCleared"), ResourceHelper.GetString("Loc_Success"), MessageBoxType.Info);
                }
                catch (Exception ex)
                {
                    string msg = string.Format(ResourceHelper.GetString("Loc_CacheError"), ex.Message);
                    CustomMessageBox.Show(msg, ResourceHelper.GetString("Loc_Error"), MessageBoxType.Error);
                }
            }
        }

        public void ApplyTheme(ThemeOption theme) => ThemeResources.Apply(theme.Name);

        private void SelectFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = ResourceHelper.GetString("Loc_SelectInstallFolderTitle"),
                InitialDirectory = Directory.Exists(DefaultPath) ? DefaultPath : null
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultPath = dialog.FolderName;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}