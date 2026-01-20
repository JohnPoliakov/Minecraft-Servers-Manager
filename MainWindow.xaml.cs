using Minecraft_Server_Manager.Models;
using Minecraft_Server_Manager.ViewModels; // Important
using System.Windows;

namespace Minecraft_Server_Manager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
        }
    }
}