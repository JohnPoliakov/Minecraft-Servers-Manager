using Minecraft_Server_Manager.Utils;
using System.Windows;
using System.Windows.Input;

namespace Minecraft_Server_Manager.Views
{
    public enum MessageBoxType
    {
        Confirmation,
        Error,
        Info,
        Loading,
        Input
    }

    public partial class CustomMessageBox : Window
    {
        public string InputValue { get; private set; } = string.Empty;

        public CustomMessageBox(string title, string message, MessageBoxType type)
        {
            InitializeComponent();

            TitleText.Text = title?.ToUpper();
            MessageText.Text = message;

            switch (type)
            {
                case MessageBoxType.Confirmation:
                    BtnYes.Content = ResourceHelper.GetString("Loc_MsgBox_Yes").ToUpper();
                    BtnNo.Content = ResourceHelper.GetString("Loc_MsgBox_No").ToUpper();
                    BtnNo.Visibility = Visibility.Visible;
                    break;

                case MessageBoxType.Error:
                    BtnYes.Content = "OK";
                    BtnYes.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#e74c3c");
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

                case MessageBoxType.Input:
                    BtnYes.Content = ResourceHelper.GetString("Loc_MsgBox_Validate").ToUpper(); // VALIDER
                    BtnNo.Content = ResourceHelper.GetString("Loc_MsgBox_Cancel").ToUpper();    // ANNULER
                    BtnNo.Visibility = Visibility.Visible;
                    InputTextBox.Visibility = Visibility.Visible;
                    InputTextBox.Focus();
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
            InputValue = InputTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        public static bool? Show(string message, string title = null, MessageBoxType type = MessageBoxType.Info)
        {
            string actualTitle = title ?? ResourceHelper.GetString("Loc_MsgBox_DefaultTitle");

            var msgBox = new CustomMessageBox(actualTitle, message, type);
            if (System.Windows.Application.Current.MainWindow != null)
                msgBox.Owner = System.Windows.Application.Current.MainWindow;

            return msgBox.ShowDialog();
        }

        public static CustomMessageBox ShowLoading(string message, string title = null)
        {
            string actualTitle = title ?? ResourceHelper.GetString("Loc_MsgBox_LoadingTitle");

            var msgBox = new CustomMessageBox(actualTitle, message, MessageBoxType.Loading);
            if (System.Windows.Application.Current.MainWindow != null)
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
            msgBox.Show();
            return msgBox;
        }

        public static string ShowInput(string message, string title = null)
        {
            string actualTitle = title ?? ResourceHelper.GetString("Loc_MsgBox_InputTitle");

            var msgBox = new CustomMessageBox(actualTitle, message, MessageBoxType.Input);

            if (System.Windows.Application.Current.MainWindow != null)
                msgBox.Owner = System.Windows.Application.Current.MainWindow;

            bool? result = msgBox.ShowDialog();

            if (result == true)
            {
                return msgBox.InputValue;
            }
            return null;
        }
    }
}