using System.Windows;
using System.Windows.Controls;

namespace Minecraft_Server_Manager.UserControls
{
    public partial class ServerMonitor : UserControl
    {
        public ServerMonitor()
        {
            InitializeComponent();
        }

        private void ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ConsoleOutput.ScrollToEnd();
        }
    }
}
