using Minecraft_Server_Manager.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace Minecraft_Server_Manager
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _notifyIcon;
        private bool _isReallyExiting = false;
        private bool _isCleanedUp = false;
        public MainWindow()
        {
            InitializeComponent();
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
            _notifyIcon.Text = "Minecraft Server Manager (Double-clic pour ouvrir)";

            _notifyIcon.DoubleClick += (s, e) => ShowWindow();

            var contextMenu = new ContextMenuStrip();
            var exitItem = new ToolStripMenuItem("Quitter définitivement");
            exitItem.Click += (s, e) =>
            {
                _isReallyExiting = true;
                this.Close();
            };
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
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

                _notifyIcon.ShowBalloonTip(3000, "Manager en arrière-plan", "Les serveurs continuent de tourner. Clic droit sur l'icône pour quitter.", ToolTipIcon.Info);
                return;
            }

            if (!_isCleanedUp)
            {
                e.Cancel = true;

                this.Hide();

                _notifyIcon.ShowBalloonTip(5000, "Fermeture en cours...", "Arrêt propre des serveurs Minecraft...\nVeuillez patienter.", ToolTipIcon.Info);

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