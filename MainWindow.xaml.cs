using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Services;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Minecraft_Server_Manager
{
    public partial class MainWindow : Window
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private System.Windows.Forms.ToolStripMenuItem _exitMenuItem;
        private bool _isReallyExiting = false;
        private bool _isCleanedUp = false;
        public MainWindow()
        {
            InitializeComponent();

            ConfigManager.Load();
            var lang = ConfigManager.Settings.Language;
            var dict = new ResourceDictionary();
            dict.Source = new Uri($"pack://application:,,,/Resources/Lang/{lang}.xaml", UriKind.Absolute);
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);

            InitializeSystemTray();
            this.DataContext = new MainViewModel();
        }

        private void InitializeSystemTray()
        {

            _notifyIcon = new System.Windows.Forms.NotifyIcon();

            try
            {
                // Charger l'icône depuis l'exécutable lui-même (ApplicationIcon du .csproj)
                // Cela garantit la même icône que le .exe et la barre des tâches
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    System.Drawing.Icon? exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (exeIcon != null)
                    {
                        _notifyIcon.Icon = new System.Drawing.Icon(exeIcon, System.Windows.Forms.SystemInformation.SmallIconSize);
                    }
                    else
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }
                else
                {
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = ResourceHelper.GetString("Loc_TrayTooltip");

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            _exitMenuItem = new System.Windows.Forms.ToolStripMenuItem(ResourceHelper.GetString("Loc_TrayExit"));
            _exitMenuItem.Click += (s, e) =>
            {
                _isReallyExiting = true;
                this.Close();
            };
            contextMenu.Items.Add(_exitMenuItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        public void UpdateTrayTexts()
        {
            if (_notifyIcon != null)
            {
                string tooltip = ResourceHelper.GetString("Loc_TrayTooltip");
                if (tooltip.Length >= 63) tooltip = tooltip.Substring(0, 60) + "...";
                _notifyIcon.Text = tooltip;
            }

            if (_exitMenuItem != null)
            {
                _exitMenuItem.Text = ResourceHelper.GetString("Loc_TrayExit");
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        protected override async void OnClosing(CancelEventArgs e)
        {

            if (!_isReallyExiting)
            {
                e.Cancel = true;
                this.Hide();

                // Point 14 : Toast notification native au lieu de BalloonTip
                ToastNotificationService.Show(
                    ResourceHelper.GetString("Loc_TrayBackgroundTitle"),
                    ResourceHelper.GetString("Loc_TrayBackgroundMsg"));
                return;
            }

            if (!_isCleanedUp)
            {
                e.Cancel = true;

                this.Hide();

                // Point 14 : Toast notification native au lieu de BalloonTip
                ToastNotificationService.Show(
                    ResourceHelper.GetString("Loc_TrayClosingTitle"),
                    ResourceHelper.GetString("Loc_TrayClosingMsg"));

                if (this.DataContext is MainViewModel vm)
                {
                    await vm.StopAllServersAsync();
                }

                _isCleanedUp = true;

                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();

                this.Close();
            }
            else
            {
                _notifyIcon.Dispose();
                base.OnClosing(e);
            }
        }
    }
}