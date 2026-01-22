using System.Windows;
using System.Windows.Input;

namespace Minecraft_Server_Manager.Views
{
    public enum MessageBoxType
    {
        Confirmation,
        Error,
        Info,
        Loading
    }

    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string title, string message, MessageBoxType type)
        {
            InitializeComponent();

            TitleText.Text = title.ToUpper();
            MessageText.Text = message;

            switch (type)
            {
                case MessageBoxType.Confirmation:
                    BtnYes.Content = "OUI";
                    BtnNo.Visibility = Visibility.Visible;
                    break;

                case MessageBoxType.Error:
                    BtnYes.Content = "OK";
                    BtnYes.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#e74c3c"); // Rouge
                    BtnNo.Visibility = Visibility.Collapsed;
                    break;

                case MessageBoxType.Info:
                    BtnYes.Content = "OK";
                    BtnNo.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxType.Loading:
                    BtnYes.Visibility = Visibility.Collapsed;
                    BtnNo.Visibility = Visibility.Collapsed;
                    LoadingBar.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public static bool? Show(string message, string title = "NOTIFICATION", MessageBoxType type = MessageBoxType.Info)
        {
            var msgBox = new CustomMessageBox(title, message, type);
            if (System.Windows.Application.Current.MainWindow != null)
                msgBox.Owner = System.Windows.Application.Current.MainWindow;

            return msgBox.ShowDialog();
        }

        public static CustomMessageBox ShowLoading(string message, string title = "TÉLÉCHARGEMENT")
        {
            var msgBox = new CustomMessageBox(title, message, MessageBoxType.Loading);

            if (System.Windows.Application.Current.MainWindow != null)
            {
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
            }

            msgBox.Show();
            return msgBox;
        }
    }
}