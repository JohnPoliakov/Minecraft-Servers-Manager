using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.Utils;
using Minecraft_Server_Manager.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Minecraft_Server_Manager
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private ToolStripMenuItem _exitMenuItem;
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

            _notifyIcon = new NotifyIcon();

            try
            {
                Uri iconUri = new Uri("pack://application:,,,/Resources/MSM.ico");

                System.Windows.Resources.StreamResourceInfo info = System.Windows.Application.GetResourceStream(iconUri);

                if (info != null)
                {
                    _notifyIcon.Icon = new Icon(info.Stream);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.Text = ResourceHelper.GetString("Loc_TrayTooltip");

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new ContextMenuStrip();
            _exitMenuItem = new ToolStripMenuItem(ResourceHelper.GetString("Loc_TrayExit"));
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

                _notifyIcon.ShowBalloonTip(1000,
                    ResourceHelper.GetString("Loc_TrayBackgroundTitle"),
                    ResourceHelper.GetString("Loc_TrayBackgroundMsg"),
                    ToolTipIcon.Info);
                return;
            }

            if (!_isCleanedUp)
            {
                e.Cancel = true;

                this.Hide();

                _notifyIcon.ShowBalloonTip(2500,
                    ResourceHelper.GetString("Loc_TrayClosingTitle"),
                    ResourceHelper.GetString("Loc_TrayClosingMsg"),
                    ToolTipIcon.Info);

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