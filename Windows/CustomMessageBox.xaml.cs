using System.Windows;
using System.Windows.Input;

namespace Minecraft_Server_Manager.Views
{
    // Enum pour définir le type de boite (Oui/Non ou juste OK)
    public enum MessageBoxType
    {
        Confirmation, // Boutons OUI/NON
        Error,        // Bouton OK (Rouge)
        Info          // Bouton OK (Vert)
    }

    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string title, string message, MessageBoxType type)
        {
            InitializeComponent();

            TitleText.Text = title.ToUpper();
            MessageText.Text = message;

            // Gestion de l'affichage des boutons selon le type
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
            }
        }

        // Permet de bouger la fenêtre sans barre de titre standard
        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true; // Renvoie TRUE
            this.Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; // Renvoie FALSE
            this.Close();
        }

        // === MÉTHODE STATIQUE POUR L'APPELER FACILEMENT ===
        public static bool? Show(string message, string title = "NOTIFICATION", MessageBoxType type = MessageBoxType.Info)
        {
            var msgBox = new CustomMessageBox(title, message, type);
            return msgBox.ShowDialog();
        }
    }
}